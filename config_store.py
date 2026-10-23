"""Shared configuration and runtime-state storage for the bot and manager."""

from __future__ import annotations

import json
import math
import os
import re
import sys
from copy import deepcopy
from dataclasses import dataclass
from datetime import UTC, datetime
from pathlib import Path
from typing import Any, Iterable

try:
    import emoji as emoji_library
except ImportError:
    emoji_library = None


def _application_dir() -> Path:
    if getattr(sys, "frozen", False):
        executable_dir = Path(sys.executable).resolve().parent
        source_project = executable_dir.parent
        if (
            (source_project / ".discord-auto-reaction-app").exists()
            or (source_project / "bot.py").exists()
        ):
            return source_project
        return executable_dir
    return Path(__file__).resolve().parent


PROJECT_DIR = _application_dir()
BOT_CONFIG_PATH = PROJECT_DIR / ".bot-config.json"
RUNTIME_STATE_PATH = PROJECT_DIR / ".bot-runtime.json"
CONFIG_VERSION = 3


@dataclass(frozen=True)
class ReactionPlan:
    common_emoji_ids: tuple[int | str, ...]
    dedicated_emoji_ids: tuple[int | str, ...]
    target_label: str
    random_enabled: bool
    weights: tuple[float, ...]
    common_random_enabled: bool = False
    common_weights: tuple[float, ...] = ()
    no_reaction_enabled: bool = False
    no_reaction_probability: float = 0.0
    common_no_reaction_enabled: bool = False
    common_no_reaction_probability: float = 0.0

    @property
    def emoji_ids(self) -> tuple[int | str, ...]:
        """Return the complete non-randomized list for compatibility helpers."""
        return self.common_emoji_ids + self.dedicated_emoji_ids


def empty_bot_config() -> dict[str, Any]:
    return {"version": CONFIG_VERSION, "guilds": {}}


def _looks_like_unicode_emoji(value: str) -> bool:
    if not value or len(value) > 32 or any(character.isspace() for character in value):
        return False
    if value[-1:] == "\u20e3" and value[0] in "#*0123456789":
        return True

    emoji_ranges = (
        (0x1F1E6, 0x1F1FF),
        (0x1F300, 0x1FAFF),
        (0x2190, 0x21FF),
        (0x2300, 0x23FF),
        (0x2460, 0x24FF),
        (0x25A0, 0x25FF),
        (0x2600, 0x27BF),
        (0x2B00, 0x2BFF),
    )
    extra_codepoints = {0x00A9, 0x00AE, 0x203C, 0x2049, 0x2122, 0x2139, 0x3030, 0x303D, 0x3297, 0x3299}
    return any(
        codepoint in extra_codepoints
        or any(start <= codepoint <= end for start, end in emoji_ranges)
        for codepoint in map(ord, value)
    )


def normalize_reaction_token(value: str) -> str | None:
    value = value.strip()
    if value.isdigit() and int(value) > 0:
        return value
    if emoji_library is not None:
        converted = emoji_library.emojize(value, language="alias")
        if emoji_library.is_emoji(converted):
            canonical_name = emoji_library.demojize(converted, language="en")
            return emoji_library.emojize(canonical_name, language="en")
    if _looks_like_unicode_emoji(value):
        return value
    return None


def is_valid_reaction_token(value: str) -> bool:
    return normalize_reaction_token(value) is not None


def reaction_token_for_display(value: object) -> str:
    text = str(value).strip()
    if text.isdigit() or emoji_library is None:
        return text
    displayed = emoji_library.demojize(text, language="en")
    return displayed if displayed != text else text


def normalize_id_list(value: str | Iterable[object], *, field_name: str) -> list[str]:
    if isinstance(value, str):
        parts = [part for part in re.split(r"[\s,]+", value.strip()) if part]
    else:
        parts = [str(part).strip() for part in value if str(part).strip()]

    normalized: list[str] = []
    seen: set[str] = set()
    for position, reaction in enumerate(parts, start=1):
        normalized_reaction = normalize_reaction_token(reaction)
        if normalized_reaction is None:
            dependency_hint = (
                "请先安装 requirements.txt 中的 emoji 依赖。"
                if reaction.startswith(":") and emoji_library is None
                else ""
            )
            raise ValueError(
                f"{field_name} 的第 {position} 项格式错误：{reaction}。"
                "请填写 Custom Emoji 的纯数字 ID、直接粘贴普通 Emoji，"
                f"或使用 :emoji_name: 英文别名。{dependency_hint}"
            )
        if normalized_reaction not in seen:
            normalized.append(normalized_reaction)
            seen.add(normalized_reaction)
    return normalized


def normalize_channel_ids(value: object) -> list[str]:
    if not isinstance(value, (list, tuple, set)):
        return []
    result: list[str] = []
    seen: set[str] = set()
    for item in value:
        channel_id = str(item).strip()
        if channel_id.isdigit() and int(channel_id) > 0 and channel_id not in seen:
            result.append(channel_id)
            seen.add(channel_id)
    return result


def normalize_probability(value: object) -> float:
    try:
        probability = float(value)
    except (TypeError, ValueError):
        return 0.0
    if not math.isfinite(probability):
        return 0.0
    return min(100.0, max(0.0, probability))


def normalize_weight_map(value: object) -> dict[str, float]:
    result: dict[str, float] = {}
    if not isinstance(value, dict):
        return result
    for raw_reaction, raw_weight in value.items():
        normalized_reaction = normalize_reaction_token(str(raw_reaction).strip())
        if normalized_reaction is None:
            continue
        try:
            weight = float(raw_weight)
        except (TypeError, ValueError):
            continue
        if math.isfinite(weight) and weight >= 0:
            result[normalized_reaction] = weight
    return result


def normalize_guild_config(raw: object, *, guild_name: str = "") -> dict[str, Any]:
    source = raw if isinstance(raw, dict) else {}
    targets_source = source.get("targets", [])
    targets: list[dict[str, Any]] = []
    seen_users: set[str] = set()

    if isinstance(targets_source, list):
        for target in targets_source:
            if not isinstance(target, dict):
                continue
            user_id = str(target.get("user_id", "")).strip()
            if not user_id.isdigit() or int(user_id) <= 0 or user_id in seen_users:
                continue
            seen_users.add(user_id)
            try:
                emoji_ids = normalize_id_list(
                    target.get("emoji_ids", []),
                    field_name=f"用户 {user_id} 的 Emoji ID",
                )
            except ValueError:
                emoji_ids = []
            targets.append(
                {
                    "user_id": user_id,
                    "label": str(target.get("label", "")).strip(),
                    "emoji_ids": emoji_ids,
                    "random_enabled": bool(target.get("random_enabled", False)),
                    "reaction_weights": normalize_weight_map(target.get("reaction_weights", {})),
                    "channel_ids": normalize_channel_ids(target.get("channel_ids", [])),
                    "no_reaction_enabled": bool(target.get("no_reaction_enabled", False)),
                    "no_reaction_probability": normalize_probability(target.get("no_reaction_probability", 0)),
                }
            )

    try:
        common_emoji_ids = normalize_id_list(
            source.get("common_emoji_ids", []),
            field_name="公共 Emoji ID",
        )
    except ValueError:
        common_emoji_ids = []

    return {
        "name": str(source.get("name", guild_name)).strip() or guild_name,
        "enabled": bool(source.get("enabled", True)),
        "common_emoji_ids": common_emoji_ids,
        "common_channel_ids": normalize_channel_ids(source.get("common_channel_ids", [])),
        "common_random_enabled": bool(source.get("common_random_enabled", False)),
        "common_reaction_weights": normalize_weight_map(source.get("common_reaction_weights", {})),
        "common_no_reaction_enabled": bool(source.get("common_no_reaction_enabled", False)),
        "common_no_reaction_probability": normalize_probability(source.get("common_no_reaction_probability", 0)),
        "targets": targets,
    }


def load_bot_config() -> dict[str, Any]:
    if not BOT_CONFIG_PATH.exists():
        return empty_bot_config()

    data = json.loads(BOT_CONFIG_PATH.read_text(encoding="utf-8"))
    if not isinstance(data, dict):
        raise ValueError("Bot 配置文件的根节点必须是对象。")

    guilds_source = data.get("guilds", {})
    guilds: dict[str, dict[str, Any]] = {}
    if isinstance(guilds_source, dict):
        for guild_id, guild_config in guilds_source.items():
            guild_id_text = str(guild_id).strip()
            if not guild_id_text.isdigit() or int(guild_id_text) <= 0:
                continue
            guilds[guild_id_text] = normalize_guild_config(guild_config)

    return {"version": CONFIG_VERSION, "guilds": guilds}


def save_bot_config(config: dict[str, Any]) -> None:
    normalized = empty_bot_config()
    guilds_source = config.get("guilds", {})
    if isinstance(guilds_source, dict):
        for guild_id, guild_config in guilds_source.items():
            guild_id_text = str(guild_id).strip()
            if not guild_id_text.isdigit() or int(guild_id_text) <= 0:
                continue
            normalized["guilds"][guild_id_text] = normalize_guild_config(guild_config)

    _atomic_write_json(BOT_CONFIG_PATH, normalized)


def ensure_guild(
    config: dict[str, Any],
    guild_id: str,
    guild_name: str,
    *,
    legacy_user_id: str = "",
    legacy_emoji_ids: Iterable[object] = (),
) -> tuple[dict[str, Any], bool]:
    guilds = config.setdefault("guilds", {})
    if not isinstance(guilds, dict):
        guilds = {}
        config["guilds"] = guilds

    if guild_id in guilds:
        guilds[guild_id] = normalize_guild_config(guilds[guild_id], guild_name=guild_name)
        guilds[guild_id]["name"] = guild_name
        return guilds[guild_id], False

    common_ids = normalize_id_list(legacy_emoji_ids, field_name="旧 Emoji ID")
    targets: list[dict[str, Any]] = []
    if legacy_user_id.isdigit() and int(legacy_user_id) > 0:
        targets.append(
            {
                "user_id": legacy_user_id,
                "label": "旧配置",
                "emoji_ids": [],
                "random_enabled": False,
                "reaction_weights": {},
                "channel_ids": [],
                "no_reaction_enabled": False,
                "no_reaction_probability": 0.0,
            }
        )

    guild_config = {
        "name": guild_name,
        "enabled": True,
        "common_emoji_ids": common_ids,
        "common_channel_ids": [],
        "common_random_enabled": False,
        "common_reaction_weights": {},
        "common_no_reaction_enabled": False,
        "common_no_reaction_probability": 0.0,
        "targets": targets,
    }
    guilds[guild_id] = guild_config
    return guild_config, True


def reaction_plan_for_message(
    config: dict[str, Any], guild_id: int, user_id: int, channel_id: int | None = None
) -> ReactionPlan | None:
    guilds = config.get("guilds", {})
    if not isinstance(guilds, dict):
        return None
    guild_config = guilds.get(str(guild_id))
    if not isinstance(guild_config, dict) or not guild_config.get("enabled", True):
        return None

    targets = guild_config.get("targets", [])
    if not isinstance(targets, list):
        return None
    target = next(
        (
            item
            for item in targets
            if isinstance(item, dict) and str(item.get("user_id", "")) == str(user_id)
        ),
        None,
    )
    if target is None:
        return None

    def channel_allowed(raw_channel_ids: object) -> bool:
        configured = normalize_channel_ids(raw_channel_ids)
        return not configured or channel_id is None or str(channel_id) in configured

    common_allowed = channel_allowed(guild_config.get("common_channel_ids", []))
    dedicated_allowed = channel_allowed(target.get("channel_ids", []))
    if not common_allowed and not dedicated_allowed:
        return None

    common_emoji_ids: list[int | str] = []
    dedicated_emoji_ids: list[int | str] = []

    def append_reactions(source: object, destination: list[int | str]) -> None:
        if not isinstance(source, list):
            return
        seen: set[str] = set()
        for raw_reaction in source:
            normalized = normalize_reaction_token(str(raw_reaction).strip())
            if normalized is None or normalized in seen:
                continue
            destination.append(int(normalized) if normalized.isdigit() else normalized)
            seen.add(normalized)

    if common_allowed:
        append_reactions(guild_config.get("common_emoji_ids", []), common_emoji_ids)
    if dedicated_allowed:
        append_reactions(target.get("emoji_ids", []), dedicated_emoji_ids)

    raw_weights = target.get("reaction_weights", {})
    weight_map = raw_weights if isinstance(raw_weights, dict) else {}
    weights: list[float] = []
    for emoji_id in dedicated_emoji_ids:
        try:
            weight = float(weight_map.get(str(emoji_id), 1.0))
        except (TypeError, ValueError):
            weight = 1.0
        weights.append(weight if math.isfinite(weight) and weight >= 0 else 1.0)

    raw_common_weights = guild_config.get("common_reaction_weights", {})
    common_weight_map = raw_common_weights if isinstance(raw_common_weights, dict) else {}
    common_weights: list[float] = []
    for emoji_id in common_emoji_ids:
        try:
            weight = float(common_weight_map.get(str(emoji_id), 1.0))
        except (TypeError, ValueError):
            weight = 1.0
        common_weights.append(weight if math.isfinite(weight) and weight >= 0 else 1.0)

    return ReactionPlan(
        common_emoji_ids=tuple(common_emoji_ids),
        dedicated_emoji_ids=tuple(dedicated_emoji_ids),
        target_label=str(target.get("label", "")).strip(),
        random_enabled=bool(target.get("random_enabled", False)),
        weights=tuple(weights),
        common_random_enabled=bool(guild_config.get("common_random_enabled", False)),
        common_weights=tuple(common_weights),
        no_reaction_enabled=bool(target.get("no_reaction_enabled", False)),
        no_reaction_probability=normalize_probability(target.get("no_reaction_probability", 0)),
        common_no_reaction_enabled=bool(guild_config.get("common_no_reaction_enabled", False)),
        common_no_reaction_probability=normalize_probability(guild_config.get("common_no_reaction_probability", 0)),
    )


def reaction_ids_for_message(
    config: dict[str, Any], guild_id: int, user_id: int
) -> tuple[list[int | str], str] | None:
    """Compatibility helper returning the complete, non-randomized reaction pool."""
    plan = reaction_plan_for_message(config, guild_id, user_id)
    if plan is None:
        return None
    return list(plan.emoji_ids), plan.target_label


def load_runtime_state() -> dict[str, Any]:
    if not RUNTIME_STATE_PATH.exists():
        return {}
    data = json.loads(RUNTIME_STATE_PATH.read_text(encoding="utf-8"))
    return data if isinstance(data, dict) else {}


def write_runtime_state(
    *,
    running: bool,
    bot_id: int | None,
    bot_username: str,
    guilds: Iterable[dict[str, object]],
) -> None:
    state = {
        "running": running,
        "updated_at": datetime.now(UTC).isoformat(),
        "bot": {"id": str(bot_id) if bot_id is not None else "", "username": bot_username},
        "guilds": list(guilds),
    }
    _atomic_write_json(RUNTIME_STATE_PATH, state)


def clone_config(config: dict[str, Any]) -> dict[str, Any]:
    return deepcopy(config)


def _atomic_write_json(path: Path, data: object) -> None:
    temp_path = path.with_suffix(path.suffix + ".tmp")
    temp_path.write_text(
        json.dumps(data, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    os.replace(temp_path, path)

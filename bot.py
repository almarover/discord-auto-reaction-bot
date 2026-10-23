"""Add server-specific custom emoji reactions to configured users' messages."""

from __future__ import annotations

import math
import os
import random
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import discord
from dotenv import load_dotenv

from config_store import (
    BOT_CONFIG_PATH,
    PROJECT_DIR,
    ReactionPlan,
    empty_bot_config,
    load_bot_config,
    reaction_plan_for_message,
    write_runtime_state,
)


def configure_console_encoding() -> None:
    """Keep Discord names and emoji from crashing redirected Windows logs."""
    for stream in (sys.stdout, sys.stderr):
        reconfigure = getattr(stream, "reconfigure", None)
        if callable(reconfigure):
            reconfigure(encoding="utf-8", errors="replace")


class ConfigurationError(ValueError):
    """Raised when a required environment variable is missing or invalid."""


@dataclass(frozen=True)
class Config:
    token: str
    legacy_target_user_id: int | None
    legacy_emoji_ids: tuple[int, ...]


@dataclass(frozen=True)
class PoolSelection:
    source: str
    reactions: tuple[int | str, ...]
    pool_size: int
    selected_probability: float | None = None
    used_weight_fallback: bool = False
    skipped: bool = False
    no_reaction_probability: float = 0.0


@dataclass(frozen=True)
class ReactionSelection:
    common: PoolSelection
    dedicated: PoolSelection

    @property
    def reactions(self) -> tuple[int | str, ...]:
        result: list[int | str] = []
        seen: set[int | str] = set()
        for reaction in self.common.reactions + self.dedicated.reactions:
            if reaction not in seen:
                result.append(reaction)
                seen.add(reaction)
        return tuple(result)


def select_reaction_pool(
    reactions: tuple[int | str, ...],
    *,
    source: str,
    random_enabled: bool,
    weights: tuple[float, ...],
    no_reaction_enabled: bool,
    no_reaction_probability: float,
    chooser: Any,
    random_value: Any,
) -> PoolSelection:
    if not reactions:
        return PoolSelection(source=source, reactions=(), pool_size=0)

    probability = min(100.0, max(0.0, no_reaction_probability))
    if no_reaction_enabled and random_value() * 100 < probability:
        return PoolSelection(
            source=source,
            reactions=(),
            pool_size=len(reactions),
            skipped=True,
            no_reaction_probability=probability,
        )

    if not random_enabled:
        return PoolSelection(source=source, reactions=reactions, pool_size=len(reactions))

    normalized_weights = list(weights)
    total_weight = sum(normalized_weights)
    used_fallback = (
        len(normalized_weights) != len(reactions)
        or not math.isfinite(total_weight)
        or total_weight <= 0
    )
    if used_fallback:
        normalized_weights = [1.0] * len(reactions)

    selected = chooser(reactions, weights=normalized_weights, k=1)[0]
    selected_index = reactions.index(selected)
    selected_probability = normalized_weights[selected_index] / sum(normalized_weights) * 100
    return PoolSelection(
        source=source,
        reactions=(selected,),
        pool_size=len(reactions),
        selected_probability=selected_probability,
        used_weight_fallback=used_fallback,
    )


def select_reactions(
    plan: ReactionPlan,
    *,
    chooser: Any = random.choices,
    random_value: Any = random.random,
) -> ReactionSelection:
    """Apply independent public and dedicated random/skip policies."""
    common = select_reaction_pool(
        plan.common_emoji_ids,
        source="公共",
        random_enabled=plan.common_random_enabled,
        weights=plan.common_weights,
        no_reaction_enabled=plan.common_no_reaction_enabled,
        no_reaction_probability=plan.common_no_reaction_probability,
        chooser=chooser,
        random_value=random_value,
    )
    dedicated = select_reaction_pool(
        plan.dedicated_emoji_ids,
        source="专属",
        random_enabled=plan.random_enabled,
        weights=plan.weights,
        no_reaction_enabled=plan.no_reaction_enabled,
        no_reaction_probability=plan.no_reaction_probability,
        chooser=chooser,
        random_value=random_value,
    )
    return ReactionSelection(common=common, dedicated=dedicated)


def load_config() -> Config:
    """Load the token and optional legacy single-user configuration."""
    load_dotenv(dotenv_path=PROJECT_DIR / ".env")

    token = os.getenv("DISCORD_TOKEN", "").strip()
    if not token:
        raise ConfigurationError(
            "DISCORD_TOKEN is missing. Add the bot token to the .env file."
        )

    legacy_user_text = os.getenv("TARGET_USER_ID", "").strip()
    legacy_user_id = (
        int(legacy_user_text)
        if legacy_user_text.isdigit() and int(legacy_user_text) > 0
        else None
    )

    legacy_emoji_ids: list[int] = []
    for raw_emoji_id in os.getenv("TARGET_EMOJI_IDS", "").split(","):
        emoji_id_text = raw_emoji_id.strip()
        if emoji_id_text.isdigit() and int(emoji_id_text) > 0:
            emoji_id = int(emoji_id_text)
            if emoji_id not in legacy_emoji_ids:
                legacy_emoji_ids.append(emoji_id)

    return Config(
        token=token,
        legacy_target_user_id=legacy_user_id,
        legacy_emoji_ids=tuple(legacy_emoji_ids),
    )


class AutoReactionBot(discord.Client):
    def __init__(self, config: Config) -> None:
        intents = discord.Intents.none()
        intents.guilds = True
        intents.guild_messages = True
        super().__init__(intents=intents)
        self.config = config
        self.routing_config: dict[str, Any] = empty_bot_config()
        self.routing_config_mtime_ns: int | None = None
        self.using_multi_server_config = False
        self.guild_emoji_catalog: dict[int, tuple[discord.Emoji, ...]] = {}
        self.guild_emoji_errors: dict[int, str] = {}

    def _refresh_routing_config(self, *, force: bool = False) -> None:
        try:
            mtime_ns = BOT_CONFIG_PATH.stat().st_mtime_ns
        except FileNotFoundError:
            self.using_multi_server_config = False
            self.routing_config_mtime_ns = None
            return

        if not force and mtime_ns == self.routing_config_mtime_ns:
            return

        self.routing_config_mtime_ns = mtime_ns
        try:
            loaded = load_bot_config()
        except (OSError, ValueError) as error:
            print(f"[Configuration Error] Could not reload .bot-config.json: {error}")
            return

        self.routing_config = loaded
        self.using_multi_server_config = True
        print("[Configuration Reloaded] Multi-server settings are now active.")

    def _configured_reactions(
        self, guild_id: int, user_id: int, channel_id: int
    ) -> ReactionPlan | None:
        self._refresh_routing_config()
        if self.using_multi_server_config:
            return reaction_plan_for_message(
                self.routing_config,
                guild_id,
                user_id,
                channel_id,
            )

        if (
            self.config.legacy_target_user_id is not None
            and user_id == self.config.legacy_target_user_id
        ):
            return ReactionPlan(
                common_emoji_ids=(),
                dedicated_emoji_ids=self.config.legacy_emoji_ids,
                target_label="旧配置",
                random_enabled=False,
                weights=tuple(1.0 for _ in self.config.legacy_emoji_ids),
            )
        return None

    def _runtime_guilds(self) -> list[dict[str, object]]:
        runtime_guilds: list[dict[str, object]] = []
        for guild in sorted(self.guilds, key=lambda item: item.name.casefold()):
            emojis = self.guild_emoji_catalog.get(guild.id, tuple(guild.emojis))
            bot_member = guild.me
            visible_channels = [
                channel
                for channel in guild.text_channels
                if bot_member is None or channel.permissions_for(bot_member).view_channel
            ]
            runtime_guilds.append(
                {
                "id": str(guild.id),
                "name": guild.name,
                "channels": [
                    {"id": str(channel.id), "name": channel.name}
                    for channel in sorted(
                        visible_channels,
                        key=lambda item: (item.position, item.name.casefold(), item.id),
                    )
                ],
                "emoji_count": len(emojis),
                "emoji_error": self.guild_emoji_errors.get(guild.id, ""),
                "emojis": [
                    {
                        "id": str(emoji.id),
                        "name": emoji.name,
                        "url": str(emoji.url),
                        "animated": emoji.animated,
                        "available": emoji.available,
                        "role_restricted": bool(emoji.roles),
                        "usable_by_bot": emoji.is_usable(),
                    }
                    for emoji in sorted(
                        emojis,
                        key=lambda item: (item.name.casefold(), item.id),
                    )
                ],
                }
            )
        return runtime_guilds

    async def _refresh_guild_emoji_catalog(
        self,
        guilds: list[discord.Guild] | None = None,
    ) -> None:
        """Fetch full guild emoji catalogs without requiring privileged intents."""
        for guild in guilds or list(self.guilds):
            try:
                fetched = await guild.fetch_emojis()
            except discord.Forbidden as error:
                self.guild_emoji_catalog[guild.id] = tuple(guild.emojis)
                self.guild_emoji_errors[guild.id] = f"Discord denied emoji sync: {error}"
                print(f"[Emoji Sync Warning] {guild.name} ({guild.id}): {error}")
            except discord.HTTPException as error:
                self.guild_emoji_catalog[guild.id] = tuple(guild.emojis)
                self.guild_emoji_errors[guild.id] = f"Discord API error: {error}"
                print(f"[Emoji Sync Warning] {guild.name} ({guild.id}): {error}")
            else:
                self.guild_emoji_catalog[guild.id] = tuple(fetched)
                self.guild_emoji_errors.pop(guild.id, None)
                print(
                    f"[Emoji Sync] {guild.name} ({guild.id}): "
                    f"{len(fetched)} custom emoji loaded."
                )

    async def _resolve_custom_emoji(
        self,
        emoji_id: int,
        target_guild: discord.Guild,
    ) -> tuple[discord.Emoji | None, str]:
        cached = self.get_emoji(emoji_id)
        if cached is not None:
            return cached, ""

        lookup_errors: list[str] = []
        guilds = [target_guild] + [
            guild for guild in self.guilds if guild.id != target_guild.id
        ]
        for source_guild in guilds:
            try:
                emoji = await source_guild.fetch_emoji(emoji_id)
            except discord.NotFound:
                continue
            except discord.Forbidden as error:
                lookup_errors.append(f"{source_guild.name}: access denied ({error})")
            except discord.HTTPException as error:
                lookup_errors.append(f"{source_guild.name}: API error ({error})")
            else:
                return emoji, ""

        if lookup_errors:
            return None, "; ".join(lookup_errors)
        return None, "Emoji was not found in any Discord server connected to this bot."

    @staticmethod
    def _emoji_source_label(
        emoji: discord.Emoji,
        target_guild: discord.Guild,
    ) -> tuple[str, bool]:
        source_guild = emoji.guild
        is_external = source_guild.id != target_guild.id
        source_label = f"{source_guild.name} ({source_guild.id})"
        return source_label, is_external

    @staticmethod
    def _channel_allows_external_emoji(
        message: discord.Message,
        emoji: discord.Emoji,
    ) -> bool:
        if emoji.guild.id == message.guild.id:
            return True
        bot_member = message.guild.me
        permissions_for = getattr(message.channel, "permissions_for", None)
        if bot_member is None or not callable(permissions_for):
            return True
        return bool(permissions_for(bot_member).external_emojis)

    def _write_runtime_state(self, *, running: bool) -> None:
        user = self.user
        try:
            write_runtime_state(
                running=running,
                bot_id=user.id if user is not None else None,
                bot_username=str(user) if user is not None else "",
                guilds=self._runtime_guilds(),
            )
        except OSError as error:
            print(f"[Runtime State Error] {error}")

    async def on_ready(self) -> None:
        if self.user is None:
            return

        self._refresh_routing_config(force=True)
        await self._refresh_guild_emoji_catalog()
        self._write_runtime_state(running=True)
        server_lines = (
            [f"  - {guild.name} ({guild.id})" for guild in self.guilds]
            or ["  - None"]
        )
        print("\n[Bot Ready]")
        print(f"Bot username: {self.user}")
        print(f"Bot User ID: {self.user.id}")
        print("Connected Discord Servers:")
        print("\n".join(server_lines))

        if self.using_multi_server_config:
            guild_configs = self.routing_config.get("guilds", {})
            configured_server_count = len(guild_configs) if isinstance(guild_configs, dict) else 0
            target_count = 0
            if isinstance(guild_configs, dict):
                for guild_config in guild_configs.values():
                    if isinstance(guild_config, dict):
                        targets = guild_config.get("targets", [])
                        if isinstance(targets, list):
                            target_count += len(targets)
            print(f"Configured Servers: {configured_server_count}")
            print(f"Configured Target Users: {target_count}")
        else:
            print("Configuration Mode: legacy .env (waiting for manager migration)")
            print(f"Legacy TARGET_USER_ID: {self.config.legacy_target_user_id}")
            print(f"Legacy Emoji count: {len(self.config.legacy_emoji_ids)}")

        print("Listening for new messages.\n", flush=True)

    async def on_guild_join(self, guild: discord.Guild) -> None:
        await self._refresh_guild_emoji_catalog([guild])
        self._write_runtime_state(running=True)

    async def on_guild_remove(self, guild: discord.Guild) -> None:
        self.guild_emoji_catalog.pop(guild.id, None)
        self.guild_emoji_errors.pop(guild.id, None)
        self._write_runtime_state(running=True)

    async def on_guild_update(
        self, before: discord.Guild, after: discord.Guild
    ) -> None:
        self._write_runtime_state(running=True)

    async def on_guild_channel_create(self, channel: discord.abc.GuildChannel) -> None:
        self._write_runtime_state(running=True)

    async def on_guild_channel_delete(self, channel: discord.abc.GuildChannel) -> None:
        self._write_runtime_state(running=True)

    async def on_guild_channel_update(
        self,
        before: discord.abc.GuildChannel,
        after: discord.abc.GuildChannel,
    ) -> None:
        self._write_runtime_state(running=True)

    async def on_guild_emojis_update(
        self,
        guild: discord.Guild,
        before: tuple[discord.Emoji, ...],
        after: tuple[discord.Emoji, ...],
    ) -> None:
        self.guild_emoji_catalog[guild.id] = tuple(after)
        self.guild_emoji_errors.pop(guild.id, None)
        self._write_runtime_state(running=True)

    async def on_message(self, message: discord.Message) -> None:
        if message.guild is None or message.author.bot:
            return

        plan = self._configured_reactions(
            message.guild.id,
            message.author.id,
            message.channel.id,
        )
        if plan is None:
            return
        selection = select_reactions(plan)
        emoji_ids = list(selection.reactions)

        channel_name = getattr(message.channel, "name", str(message.channel))
        print("\n[Target Message]")
        print(f"Server: {message.guild.name} ({message.guild.id})")
        print(f"User: {message.author} ({message.author.id})")
        if plan.target_label:
            print(f"Target label: {plan.target_label}")
        print(f"Channel: {channel_name}")
        print(f"Message ID: {message.id}")

        for pool in (selection.common, selection.dedicated):
            if pool.skipped:
                print(
                    f"[No Reaction Draw] Source: {pool.source} | "
                    f"No-reaction probability: {pool.no_reaction_probability:.6f}%"
                )
                continue
            if pool.selected_probability is None:
                continue
            if pool.used_weight_fallback:
                print(
                    f"[Random Draw Warning] Source: {pool.source} | Invalid or zero total weight; "
                    "using equal probabilities."
                )
            selected = pool.reactions[0]
            selected_label = (
                f"Emoji ID: {selected}"
                if isinstance(selected, int)
                else f"Unicode Emoji: {selected}"
            )
            print(
                f"[Random Draw] Source: {pool.source} | Selected {selected_label} | "
                f"Probability: {pool.selected_probability:.6f}% | Pool size: {pool.pool_size}"
            )

        added_count = 0
        for configured_reaction in emoji_ids:
            reaction: discord.Emoji | str
            reaction_label = (
                f"Emoji ID: {configured_reaction}"
                if isinstance(configured_reaction, int)
                else f"Unicode Emoji: {configured_reaction}"
            )
            if isinstance(configured_reaction, str):
                reaction = configured_reaction
            else:
                reaction, lookup_error = await self._resolve_custom_emoji(
                    configured_reaction,
                    message.guild,
                )
                if reaction is None:
                    print(
                        f"[Reaction Failed] {reaction_label} | Reason: {lookup_error}"
                    )
                    continue

                source_label, is_external = self._emoji_source_label(
                    reaction,
                    message.guild,
                )
                reaction_label += f" | Source: {source_label}"
                if not reaction.is_usable():
                    print(
                        f"[Reaction Failed] {reaction_label} | Reason: "
                        "Emoji is unavailable or restricted to roles the bot does not have."
                    )
                    continue
                if is_external and not self._channel_allows_external_emoji(
                    message,
                    reaction,
                ):
                    print(
                        f"[Reaction Failed] {reaction_label} | Reason: "
                        "The bot lacks Use External Emojis in the target channel."
                    )
                    continue

            try:
                await message.add_reaction(reaction)
            except discord.Forbidden as error:
                print(
                    f"[Reaction Failed] {reaction_label} | Reason: "
                    f"Discord denied access (check View Channel, Read Message "
                    f"History, Add Reactions, and emoji access): {error}"
                )
            except discord.NotFound as error:
                print(
                    f"[Reaction Failed] {reaction_label} | Reason: "
                    f"The message or emoji no longer exists: {error}"
                )
            except discord.HTTPException as error:
                print(
                    f"[Reaction Failed] {reaction_label} | Reason: "
                    f"Discord API request failed: {error}"
                )
            except Exception as error:
                print(
                    f"[Reaction Failed] {reaction_label} | Reason: "
                    f"Unexpected {type(error).__name__}: {error}"
                )
            else:
                added_count += 1
                print(f"[Reaction Added] {reaction_label}")

        print(
            f"[Completed] Added {added_count}/{len(emoji_ids)} reactions",
            flush=True,
        )

    async def close(self) -> None:
        self._write_runtime_state(running=False)
        await super().close()


def main() -> None:
    configure_console_encoding()
    try:
        config = load_config()
    except ConfigurationError as error:
        print(f"[Configuration Error] {error}", file=sys.stderr)
        raise SystemExit(1) from error

    bot = AutoReactionBot(config)
    try:
        bot.run(config.token)
    except discord.LoginFailure as error:
        print(
            "[Login Error] Discord rejected DISCORD_TOKEN. Copy a fresh bot token "
            "from the Developer Portal and update .env.",
            file=sys.stderr,
        )
        raise SystemExit(1) from error
    except KeyboardInterrupt:
        print("\n[Stopped] Bot stopped by user.")


if __name__ == "__main__":
    main()

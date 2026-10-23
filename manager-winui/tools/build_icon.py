from __future__ import annotations

import io
import struct
from collections import deque
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "Assets"
QA = ROOT / "qa"

SMALL_SOURCE = ASSETS / "RobotIconSmall.png"
SMALL_CLEAN = ASSETS / "RobotIconSmallTransparent.png"
ICON_PATH = ASSETS / "AppIcon.ico"


def remove_connected_checkerboard(image: Image.Image) -> Image.Image:
    """Remove a bright neutral checkerboard connected to the canvas edge."""
    rgb = image.convert("RGB")
    width, height = rgb.size
    pixels = rgb.load()
    background = bytearray(width * height)
    queue: deque[tuple[int, int]] = deque()

    def is_background_candidate(x: int, y: int) -> bool:
        red, green, blue = pixels[x, y]
        return min(red, green, blue) >= 218 and max(red, green, blue) - min(red, green, blue) <= 24

    def enqueue(x: int, y: int) -> None:
        index = y * width + x
        if not background[index] and is_background_candidate(x, y):
            background[index] = 1
            queue.append((x, y))

    for x in range(width):
        enqueue(x, 0)
        enqueue(x, height - 1)
    for y in range(height):
        enqueue(0, y)
        enqueue(width - 1, y)

    while queue:
        x, y = queue.popleft()
        for next_x, next_y in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if 0 <= next_x < width and 0 <= next_y < height:
                enqueue(next_x, next_y)

    result = rgb.convert("RGBA")
    alpha = Image.new("L", (width, height), 255)
    alpha_pixels = alpha.load()
    for y in range(height):
        row = y * width
        for x in range(width):
            if background[row + x]:
                alpha_pixels[x, y] = 0
    result.putalpha(alpha)
    return result


def fit_to_square(image: Image.Image, size: int, fill: float) -> Image.Image:
    alpha = image.getchannel("A")
    bounds = alpha.getbbox()
    if bounds is None:
        raise ValueError("Icon source has no visible pixels")

    cropped = image.crop(bounds)
    max_size = max(1, round(size * fill))
    ratio = min(max_size / cropped.width, max_size / cropped.height)
    resized_size = (
        max(1, round(cropped.width * ratio)),
        max(1, round(cropped.height * ratio)),
    )

    # Premultiplied-alpha resizing prevents hidden background pixels from
    # creating a light fringe around the icon.
    resized = cropped.convert("RGBa").resize(resized_size, Image.Resampling.LANCZOS).convert("RGBA")
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    position = ((size - resized.width) // 2, (size - resized.height) // 2)
    canvas.alpha_composite(resized, position)
    return canvas


def png_bytes(image: Image.Image) -> bytes:
    output = io.BytesIO()
    image.save(output, format="PNG", optimize=True)
    return output.getvalue()


def write_png_ico(path: Path, frames: list[tuple[int, bytes]]) -> None:
    header_size = 6 + 16 * len(frames)
    offset = header_size
    entries: list[bytes] = []
    for size, payload in frames:
        dimension = 0 if size == 256 else size
        entries.append(
            struct.pack(
                "<BBBBHHII",
                dimension,
                dimension,
                0,
                0,
                1,
                32,
                len(payload),
                offset,
            )
        )
        offset += len(payload)

    path.write_bytes(struct.pack("<HHH", 0, 1, len(frames)) + b"".join(entries) + b"".join(data for _, data in frames))


def create_preview(frames: dict[int, Image.Image]) -> None:
    QA.mkdir(parents=True, exist_ok=True)
    scale = 12
    sizes = (16, 24, 32)
    widths = [size * scale for size in sizes]
    preview = Image.new("RGB", (sum(widths) + 80, 32 * scale + 60), "#202020")
    draw = ImageDraw.Draw(preview)
    x = 20
    for size, width in zip(sizes, widths):
        enlarged = frames[size].resize((width, width), Image.Resampling.NEAREST)
        preview.paste(enlarged, (x, 20), enlarged)
        draw.text((x, 28 + width), f"{size} px", fill="#ffffff")
        x += width + 20
    preview.save(QA / "AppIcon-small-frames-preview.png")


def main() -> None:
    simplified = remove_connected_checkerboard(Image.open(SMALL_SOURCE))
    simplified.save(SMALL_CLEAN, optimize=True)

    existing = Image.open(ICON_PATH)
    frames: dict[int, Image.Image] = {}
    for size in (32, 48, 64, 128, 256):
        frames[size] = existing.ico.getimage((size, size)).convert("RGBA")
    frames[16] = fit_to_square(simplified, 16, 0.90)
    frames[24] = fit_to_square(simplified, 24, 0.90)

    ordered_sizes = (16, 24, 32, 48, 64, 128, 256)
    write_png_ico(ICON_PATH, [(size, png_bytes(frames[size])) for size in ordered_sizes])
    create_preview(frames)

    print(f"Updated: {ICON_PATH}")
    print(f"Transparent small source: {SMALL_CLEAN}")
    print(f"Preview: {QA / 'AppIcon-small-frames-preview.png'}")


if __name__ == "__main__":
    main()

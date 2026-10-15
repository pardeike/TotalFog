"""Compose a labelled review board and transparent film titles from native shots."""

from pathlib import Path
import json
import sys
from PIL import Image, ImageDraw, ImageFont

recipe = json.loads(Path(sys.argv[1]).read_text())
output, work = map(Path, sys.argv[2:4])
board = Image.new("RGB", (1920, 840), "#171b20")
font = ImageFont.load_default(size=21)
large = ImageFont.load_default(size=32)
for i, shot in enumerate(recipe["shots"]):
    x, y = (i % 3) * 640, (i // 3) * 420
    with Image.open(output / (shot["id"] + ".png")) as image:
        board.paste(image.resize((640, 360), Image.Resampling.LANCZOS), (x, y))
    ImageDraw.Draw(board).text((x + 12, y + 374), shot["title"], font=font, fill="white")
    title = Image.new("RGBA", (recipe["width"], recipe["height"]))
    draw = ImageDraw.Draw(title)
    width = draw.textlength(shot["title"], font=large) + 36
    draw.rounded_rectangle((24, 108, 24 + width, 168), radius=7, fill=(15, 20, 25, 224))
    draw.text((42, 123), shot["title"], font=large, fill="white")
    title.save(work / (shot["id"] + "-title.png"))
board.save(output / "contact-sheet.png")

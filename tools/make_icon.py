#!/usr/bin/env python3
"""Generate a 256x256 icon.png for the Mjolnir mod (hammer + lightning bolt)."""
from PIL import Image, ImageDraw

S = 256
img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

# stormy background
d.ellipse([10, 10, 246, 246], fill=(26, 30, 46, 255))
d.ellipse([20, 20, 236, 236], outline=(52, 58, 82, 255), width=3)

# lightning bolt (behind the hammer)
bolt = [(176, 22), (118, 128), (150, 128), (128, 234), (212, 112), (176, 112), (198, 26)]
d.polygon(bolt, fill=(255, 213, 64, 255), outline=(255, 244, 168, 255))

# hammer handle
d.rounded_rectangle([88, 96, 110, 216], radius=10, fill=(110, 76, 46, 255), outline=(70, 46, 26, 255), width=3)
# handle grip wraps
for y in (120, 140, 160, 180, 200):
    d.line([(86, y), (112, y + 4)], fill=(150, 108, 66, 255), width=5)

# hammer head (silver block)
d.rounded_rectangle([42, 62, 156, 126], radius=12, fill=(196, 204, 222, 255), outline=(120, 128, 150, 255), width=4)
d.rounded_rectangle([50, 70, 148, 96], radius=8, fill=(224, 232, 248, 255))
# glowing rune on the head
d.rounded_rectangle([90, 74, 108, 92], radius=4, fill=(90, 200, 255, 255), outline=(40, 120, 200, 255), width=2)

img.save("icon.png")
print("icon.png written")

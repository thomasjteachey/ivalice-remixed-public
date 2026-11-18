"""Contact sheet of PNGs, on the PC (needs Pillow): python sheet.py OUT.png COLUMNS SCALE files..."""
import sys
from PIL import Image, ImageDraw
out, cols, scale, files = sys.argv[1], int(sys.argv[2]), float(sys.argv[3]), sys.argv[4:]
ims = [Image.open(f).convert("RGB") for f in files]
w = max(i.width for i in ims); h = max(i.height for i in ims)
tw, th = int(w * scale), int(h * scale)
rows = (len(ims) + cols - 1) // cols
sheet = Image.new("RGB", (cols * tw, rows * (th + 14)), "black")
d = ImageDraw.Draw(sheet)
for n, (f, im) in enumerate(zip(files, ims)):
    x, y = (n % cols) * tw, (n // cols) * (th + 14)
    sheet.paste(im.resize((int(im.width * scale), int(im.height * scale))), (x, y + 14))
    d.text((x + 2, y + 1), f.replace("\\", "/").rsplit("/", 1)[-1][:-4], fill="yellow")
sheet.save(out)
print(out, sheet.size)

"""
Meta Receipts Lite — Icon & Logo Generator Script
Renders assets/logo.svg into:
- assets/logo-512.png
- assets/logo-256.png
- assets/AppIcon.ico (multi-resolution Windows icon)
- MetaReceiptsLite/AppIcon.ico
"""

import os
import shutil
import fitz
from PIL import Image

def generate_icons():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    svg_path = os.path.join(root, "assets", "logo.svg")
    assets_dir = os.path.join(root, "assets")
    app_icon_target = os.path.join(root, "MetaReceiptsLite", "AppIcon.ico")

    if not os.path.exists(svg_path):
        raise FileNotFoundError(f"SVG not found at {svg_path}")

    print(f"==> Rendering {svg_path}...")
    doc = fitz.open(svg_path)
    page = doc[0]

    # Render at 2x supersampling (1024x1024)
    zoom = 2.0
    mat = fitz.Matrix(zoom, zoom)
    pix = page.get_pixmap(matrix=mat, alpha=True)
    temp_1024 = os.path.join(assets_dir, "temp_1024.png")
    pix.save(temp_1024)

    img = Image.open(temp_1024)

    # 1. logo-512.png
    out_512 = os.path.join(assets_dir, "logo-512.png")
    img_512 = img.resize((512, 512), Image.Resampling.LANCZOS)
    img_512.save(out_512, optimize=True)
    print(f"Created: {out_512}")

    # 2. logo-256.png
    out_256 = os.path.join(assets_dir, "logo-256.png")
    img_256 = img.resize((256, 256), Image.Resampling.LANCZOS)
    img_256.save(out_256, optimize=True)
    print(f"Created: {out_256}")

    # 3. assets/AppIcon.ico
    out_ico = os.path.join(assets_dir, "AppIcon.ico")
    icon_sizes = [(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (16, 16)]
    img_512.save(out_ico, format="ICO", sizes=icon_sizes)
    print(f"Created: {out_ico}")

    # 4. Copy to MetaReceiptsLite/AppIcon.ico
    shutil.copy(out_ico, app_icon_target)
    print(f"Updated: {app_icon_target}")

    # Cleanup temp
    if os.path.exists(temp_1024):
        os.remove(temp_1024)

    print("==> All icons successfully generated.")

if __name__ == "__main__":
    generate_icons()

"""
Meta Receipts Lite — Icon & Logo Generator Script
Renders assets/logo.svg into:
- assets/logo-512.png
- assets/logo-256.png
- assets/AppIcon.ico (multi-resolution Windows icon)
- MetaReceiptsLite/AppIcon.ico

Uses Edge/Chrome headless for 100% SVG gradient, shadow, and antialiasing fidelity.
"""

import os
import shutil
import subprocess
import sys
from PIL import Image

def find_browser():
    candidates = [
        r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
        r"C:\Program Files\Google\Chrome\Application\chrome.exe",
        r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
    ]
    for path in candidates:
        if os.path.exists(path):
            return path
    # Try PATH
    for name in ["msedge.exe", "chrome.exe"]:
        found = shutil.which(name)
        if found:
            return found
    return None

def render_svg_to_png(svg_path, out_png_path, size=512):
    browser = find_browser()
    if not browser:
        raise RuntimeError("No headless browser (Edge or Chrome) found to render SVG.")

    # HTML harness for pixel-perfect SVG rendering with transparency
    html_content = f"""<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<style>
  * {{ margin: 0; padding: 0; box-sizing: border-box; }}
  html, body {{
    width: {size}px;
    height: {size}px;
    background: transparent !important;
    overflow: hidden;
  }}
  img {{
    width: {size}px;
    height: {size}px;
    display: block;
  }}
</style>
</head>
<body>
  <img src="{os.path.basename(svg_path)}" />
</body>
</html>"""

    temp_dir = os.path.dirname(svg_path)
    harness_path = os.path.join(temp_dir, "_render_harness.html")
    with open(harness_path, "w", encoding="utf-8") as f:
        f.write(html_content)

    url = "file:///" + os.path.abspath(harness_path).replace("\\", "/")

    cmd = [
        browser,
        "--headless=new",
        "--disable-gpu",
        "--force-device-scale-factor=1",
        f"--window-size={size},{size}",
        "--hide-scrollbars",
        "--default-background-color=00000000",
        f"--screenshot={os.path.abspath(out_png_path)}",
        url
    ]

    try:
        res = subprocess.run(cmd, capture_output=True, text=True, check=True)
    finally:
        if os.path.exists(harness_path):
            try:
                os.remove(harness_path)
            except Exception:
                pass

    if not os.path.exists(out_png_path) or os.path.getsize(out_png_path) == 0:
        raise RuntimeError(f"Browser screenshot failed: {res.stderr}")

def generate_icons():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    svg_path = os.path.join(root, "assets", "logo.svg")
    assets_dir = os.path.join(root, "assets")
    app_icon_target = os.path.join(root, "MetaReceiptsLite", "AppIcon.ico")

    if not os.path.exists(svg_path):
        raise FileNotFoundError(f"SVG not found at {svg_path}")

    # Render at 512x512
    out_512 = os.path.join(assets_dir, "logo-512.png")
    print(f"==> Rendering {svg_path} at 512x512 via headless browser...")
    render_svg_to_png(svg_path, out_512, size=512)
    print(f"Created: {out_512}")

    # Inspect image
    img_512 = Image.open(out_512).convert("RGBA")
    print(f"Image verified: {img_512.size}, mode: {img_512.mode}")

    # Render 256x256
    out_256 = os.path.join(assets_dir, "logo-256.png")
    img_256 = img_512.resize((256, 256), Image.Resampling.LANCZOS)
    img_256.save(out_256, optimize=True)
    print(f"Created: {out_256}")

    # Generate multi-resolution ICO
    out_ico = os.path.join(assets_dir, "AppIcon.ico")
    icon_sizes = [(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (16, 16)]
    img_512.save(out_ico, format="ICO", sizes=icon_sizes)
    print(f"Created: {out_ico}")

    # Copy to WPF project
    shutil.copy(out_ico, app_icon_target)
    print(f"Updated: {app_icon_target}")

    print("==> All icons successfully generated with vibrant colors.")

if __name__ == "__main__":
    generate_icons()

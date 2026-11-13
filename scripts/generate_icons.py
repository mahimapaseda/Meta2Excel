import os
import fitz  # PyMuPDF
from PIL import Image

def generate_icons(svg_path, output_dir):
    os.makedirs(output_dir, exist_ok=True)
    
    # Target dimensions for Windows ICO
    ico_sizes = [(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (16, 16)]
    
    print(f"Reading SVG: {svg_path}")
    doc = fitz.open(svg_path)
    page = doc[0]
    
    # High-res 512x512 master render
    scale_512 = 512.0 / page.rect.width
    matrix_512 = fitz.Matrix(scale_512, scale_512)
    pix_512 = page.get_pixmap(matrix=matrix_512, alpha=True)
    
    png_512_path = os.path.join(output_dir, "logo-512.png")
    pix_512.save(png_512_path)
    print(f"Saved: {png_512_path}")
    
    # 256x256 render
    scale_256 = 256.0 / page.rect.width
    matrix_256 = fitz.Matrix(scale_256, scale_256)
    pix_256 = page.get_pixmap(matrix=matrix_256, alpha=True)
    png_256_path = os.path.join(output_dir, "logo-256.png")
    pix_256.save(png_256_path)
    print(f"Saved: {png_256_path}")

    # Generate multi-res images with Pillow for crisp sampling
    master_img = Image.open(png_512_path).convert("RGBA")
    
    rendered_images = []
    for w, h in ico_sizes:
        resized = master_img.resize((w, h), Image.Resampling.LANCZOS)
        rendered_images.append(resized)
    
    # Save Windows multi-resolution ICO
    ico_path = os.path.join(output_dir, "AppIcon.ico")
    # In Pillow, passing append_images saves multi-page icons
    rendered_images[0].save(
        ico_path,
        format="ICO",
        sizes=[(img.width, img.height) for img in rendered_images]
    )
    print(f"Saved multi-resolution ICO ({len(ico_sizes)} layers): {ico_path}")
    
    return ico_path, png_256_path, png_512_path

if __name__ == "__main__":
    svg = os.path.abspath("assets/logo.svg")
    generate_icons(svg, "assets")

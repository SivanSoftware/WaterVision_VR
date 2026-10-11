"""
Generates WristMenuButton_Icon.png — a 256x256 transparent-background sprite for the
VR wrist watch button. Design philosophy: Restrained Dimension (see sibling .md).

Rendered at 4x supersample then downscaled with LANCZOS for crisp anti-aliased edges
that hold up in a VR headset at any sampling.
"""

from PIL import Image, ImageDraw, ImageFilter
import os

OUT = r"C:\Users\User\VR Multiplayer Sample Project\Assets\Prefabs\WristMenu\WristMenuButton_Icon.png"
FINAL = 256
SS = 4  # supersample factor
SIZE = FINAL * SS

img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
draw = ImageDraw.Draw(img)

# ── Disc geometry ────────────────────────────────────────────────────
cx = cy = SIZE / 2
# Disc radius leaves a small margin so the highlight ring + outer edge sit cleanly
# inside the 256px frame even after downscale.
r_outer = SIZE * 0.48        # outer edge of disc
r_inner_ring = SIZE * 0.475  # inner edge of highlight ring
r_disc = SIZE * 0.46         # actual disc surface (slightly inside the ring)

# 1) Soft outer shadow — gives the disc real volume without being literal.
# A radial-blurred dark ellipse just outside the disc edge.
shadow = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
shadow_draw = ImageDraw.Draw(shadow)
shadow_draw.ellipse(
    [cx - r_outer - SS * 6, cy - r_outer - SS * 6,
     cx + r_outer + SS * 6, cy + r_outer + SS * 6],
    fill=(0, 0, 0, 100),
)
shadow = shadow.filter(ImageFilter.GaussianBlur(radius=SS * 4))
img = Image.alpha_composite(img, shadow)
draw = ImageDraw.Draw(img)

# 2) Highlight ring — the hairline of luminance the philosophy calls for.
# Drawn slightly larger than the disc so the disc fill covers its inner half,
# leaving only the outer edge visible as a 1–2px crisp glint.
draw.ellipse(
    [cx - r_outer, cy - r_outer, cx + r_outer, cy + r_outer],
    fill=(255, 255, 255, 90),
)

# 3) Disc surface — dark charcoal, slightly translucent so it has presence on
# any background without being a hard black puck.
draw.ellipse(
    [cx - r_disc, cy - r_disc, cx + r_disc, cy + r_disc],
    fill=(24, 26, 32, 235),
)

# 4) Inner subtle vignette — a darker ring inside the disc to give the centre
# a quiet recessed feel.
vignette = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
v_draw = ImageDraw.Draw(vignette)
v_draw.ellipse(
    [cx - r_disc, cy - r_disc, cx + r_disc, cy + r_disc],
    outline=(0, 0, 0, 90), width=int(SS * 3),
)
vignette = vignette.filter(ImageFilter.GaussianBlur(radius=SS * 2))
img = Image.alpha_composite(img, vignette)
draw = ImageDraw.Draw(img)

# ── Hamburger icon ───────────────────────────────────────────────────
# Three short horizontal bars, white, with softened caps. The bar width is
# chosen so the icon reads at 32–64 px target downscale (the disc area is
# ~235 px at FINAL=256). Spacing tuned to a 1 : 1.6 bar-to-gap ratio.

bar_w = SIZE * 0.36         # bar length (horizontal extent)
bar_h = SIZE * 0.045        # bar thickness
gap   = SIZE * 0.07         # gap between bars (centre-to-centre)
cap_r = bar_h / 2           # cap radius (rounded ends)

bar_xL = cx - bar_w / 2
bar_xR = cx + bar_w / 2

ys = [cy - gap, cy, cy + gap]
for y in ys:
    # rounded rectangle = filled rect + two end-cap discs
    draw.rectangle(
        [bar_xL + cap_r, y - bar_h / 2, bar_xR - cap_r, y + bar_h / 2],
        fill=(255, 255, 255, 255),
    )
    draw.ellipse(
        [bar_xL, y - bar_h / 2, bar_xL + bar_h, y + bar_h / 2],
        fill=(255, 255, 255, 255),
    )
    draw.ellipse(
        [bar_xR - bar_h, y - bar_h / 2, bar_xR, y + bar_h / 2],
        fill=(255, 255, 255, 255),
    )

# ── Downscale ────────────────────────────────────────────────────────
final = img.resize((FINAL, FINAL), Image.LANCZOS)
os.makedirs(os.path.dirname(OUT), exist_ok=True)
final.save(OUT, "PNG")
print(f"Wrote {OUT}  ({FINAL}x{FINAL})")

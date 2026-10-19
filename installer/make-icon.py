"""Kilit Bul asma kilit ikonunu (çok boyutlu .ico) üretir.

Kullanım: python3 installer/make-icon.py [önizleme.png]

Üretilenler (depo kökünden çalıştırılır):
  src/KilitBul/Assets/KilitBul.ico     exe / pencere / menü ikonu
  src/KilitBul/Assets/Lock64.png       pencere başlığındaki kilit
  installer/msix/Assets/*.png          sparse MSIX paketi logoları
"""
import sys
from PIL import Image, ImageDraw

SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]
SS = 8  # süper örnekleme katsayısı (kenar yumuşatma için)

SHACKLE = (84, 96, 108, 255)
SHACKLE_HI = (150, 162, 174, 255)
BODY = (245, 176, 20, 255)
BODY_DARK = (196, 128, 0, 255)
OUTLINE = (120, 72, 0, 255)
KEYHOLE = (70, 42, 0, 255)


def draw_lock(size: int) -> Image.Image:
    s = size * SS
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    small = size <= 24
    # Küçük boyutlarda kalın hatlar ve kenar boşluğu az; büyüklerde daha zarif oranlar.
    pad = s * (0.06 if small else 0.08)
    body_top = s * 0.44
    body = (pad, body_top, s - pad, s - pad * 0.6)
    shackle_w = s * (0.13 if small else 0.11)
    sx0, sx1 = s * 0.24, s * 0.76
    sy0 = pad * 0.6

    # Kilit halkası (üst kemer + iki bacak)
    d.arc((sx0, sy0, sx1, sy0 + (sx1 - sx0)), 180, 360, fill=SHACKLE, width=int(shackle_w))
    mid_y = sy0 + (sx1 - sx0) / 2
    d.rectangle((sx0, mid_y, sx0 + shackle_w, body_top + s * 0.02), fill=SHACKLE)
    d.rectangle((sx1 - shackle_w, mid_y, sx1, body_top + s * 0.02), fill=SHACKLE)
    if not small:
        hl = shackle_w * 0.28
        d.arc((sx0 + hl, sy0 + hl, sx1 - hl, sy0 + (sx1 - sx0) - hl), 200, 260, fill=SHACKLE_HI, width=int(hl))

    # Gövde
    radius = s * (0.08 if small else 0.10)
    outline_w = max(SS, int(s * (0.05 if small else 0.035)))
    d.rounded_rectangle(body, radius=radius, fill=OUTLINE)
    inner = (body[0] + outline_w, body[1] + outline_w, body[2] - outline_w, body[3] - outline_w)
    d.rounded_rectangle(inner, radius=max(1, radius - outline_w), fill=BODY)
    if not small:
        # Alt yarıda hafif koyu bant (hacim hissi)
        shade_top = inner[1] + (inner[3] - inner[1]) * 0.62
        d.rounded_rectangle((inner[0], shade_top, inner[2], inner[3]), radius=max(1, radius - outline_w), fill=BODY_DARK)
        d.rectangle((inner[0], shade_top, inner[2], shade_top + radius), fill=BODY_DARK)

    # Anahtar deliği
    cx = s / 2
    cy = body_top + (body[3] - body_top) * 0.42
    r = s * (0.085 if small else 0.07)
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=KEYHOLE)
    stem_w = r * 0.9
    d.polygon([(cx - stem_w / 2, cy), (cx + stem_w / 2, cy),
               (cx + stem_w * 0.75, cy + r * 2.6), (cx - stem_w * 0.75, cy + r * 2.6)], fill=KEYHOLE)

    return img.resize((size, size), Image.LANCZOS)


def logo(canvas: int, lock: int) -> Image.Image:
    """Saydam kare zemin ortasında kilit (MSIX logoları için kenar boşluklu)."""
    img = Image.new("RGBA", (canvas, canvas), (0, 0, 0, 0))
    off = (canvas - lock) // 2
    img.alpha_composite(draw_lock(lock), (off, off))
    return img


def main() -> None:
    frames = [draw_lock(sz) for sz in SIZES]
    frames[-1].save("src/KilitBul/Assets/KilitBul.ico", format="ICO",
                    sizes=[(sz, sz) for sz in SIZES], append_images=frames[:-1])
    draw_lock(64).save("src/KilitBul/Assets/Lock64.png")

    for name, canvas, lock in (("StoreLogo", 50, 44), ("Square44x44Logo", 44, 40), ("Square150x150Logo", 150, 100)):
        logo(canvas, lock).save(f"installer/msix/Assets/{name}.png")

    if len(sys.argv) > 1:
        # Önizleme: tüm boyutlar yan yana, açık ve koyu zemin üzerinde
        w = sum(SIZES) + 10 * (len(SIZES) + 1)
        sheet = Image.new("RGBA", (w, 2 * 256 + 30), (255, 255, 255, 255))
        ImageDraw.Draw(sheet).rectangle((0, 256 + 20, w, 2 * 256 + 30), fill=(32, 32, 32, 255))
        x = 10
        for sz, fr in zip(SIZES, frames):
            sheet.alpha_composite(fr, (x, 10))
            sheet.alpha_composite(fr, (x, 256 + 20 + 5))
            x += sz + 10
        sheet.save(sys.argv[1])


if __name__ == "__main__":
    main()

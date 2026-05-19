"""
Flat hospital plaster + linoleum texture generator.

The previous decay textures had heavy streaks/water drips that, when tiled,
read as wood grain - the user explicitly asked for FLAT plaster walls
(no vertical lines).

This script produces:
  - Plaster_Wall.png       RGB 225,220,205 base, very subtle noise, no streaks
  - Plaster_Wall_Green.png RGB ~115,140,110 wainscot variant
  - Floor_Linoleum.png     RGB 175,168,152 base linoleum, faint tile grout,
                           subtle wear - no streaks, no aggressive grain
  - Concrete_Basement.png  Darker damp concrete, still flat - no vertical streaks

All tiles are 2048², seamless. Frequency content is intentionally low so
that even when tiled 8× across a 32m wall, no repeating high-contrast
artifact reads as a column.
"""

import os
import numpy as np
from PIL import Image, ImageFilter

OUT = r"C:\Users\hvnes\YomawariByoin\Assets\Textures\Generated"
os.makedirs(OUT, exist_ok=True)

np.random.seed(42)
SZ = 2048


def _smoothstep(t):
    return t * t * (3 - 2 * t)


def tileable_value_noise(width, height, scale=64, octaves=4, persistence=0.5, seed=0):
    rng = np.random.default_rng(seed)
    field = np.zeros((height, width), dtype=np.float32)
    amp = 1.0
    total = 0.0
    for o in range(octaves):
        s = scale / (2 ** o)
        gx = max(1, int(round(width / s)))
        gy = max(1, int(round(height / s)))
        grid = rng.random((gy + 1, gx + 1)).astype(np.float32)
        grid[-1, :] = grid[0, :]
        grid[:, -1] = grid[:, 0]
        xs = (np.arange(width) / width) * gx
        ys = (np.arange(height) / height) * gy
        x0 = np.floor(xs).astype(np.int32)
        y0 = np.floor(ys).astype(np.int32)
        tx = _smoothstep(xs - x0)
        ty = _smoothstep(ys - y0)
        x1 = (x0 + 1) % gx
        y1 = (y0 + 1) % gy
        c00 = grid[y0[:, None], x0[None, :]]
        c10 = grid[y0[:, None], x1[None, :]]
        c01 = grid[y1[:, None], x0[None, :]]
        c11 = grid[y1[:, None], x1[None, :]]
        a = c00 + (c10 - c00) * tx[None, :]
        b = c01 + (c11 - c01) * tx[None, :]
        layer = a + (b - a) * ty[:, None]
        field += layer * amp
        total += amp
        amp *= persistence
    field /= total
    field -= field.min()
    field /= max(1e-6, field.max())
    return field


def save_rgb(arr, path):
    arr = np.clip(arr, 0, 1)
    Image.fromarray((arr * 255).astype(np.uint8)).save(path)
    print("  ", path)


def make_flat_plaster(base_rgb, out_name, noise_amp=0.05, age_amp=0.04, seed=10):
    """Almost flat plaster: tiny micro-noise + low-frequency aging blotches.
    NO streaks, NO directional patterns - radially isotropic."""
    base = np.asarray(base_rgb, dtype=np.float32) / 255.0
    arr = np.ones((SZ, SZ, 3), dtype=np.float32) * base[None, None, :]
    # Micro-grain (high-freq, low-amp)
    micro = tileable_value_noise(SZ, SZ, scale=8, octaves=2, seed=seed)
    arr *= (1.0 - noise_amp + noise_amp * 2 * micro[..., None])
    # Aging blotches (low-freq, slightly warmer)
    age = tileable_value_noise(SZ, SZ, scale=600, octaves=3, seed=seed + 1)
    age_warm = np.array([1.05, 1.02, 0.94], dtype=np.float32)
    age_mask = np.clip((age - 0.4) * 2.0, 0, 1) * age_amp
    arr = arr * (1 - age_mask[..., None]) + arr * age_warm[None, None, :] * age_mask[..., None]
    # Very subtle darker patches (dust)
    dust = tileable_value_noise(SZ, SZ, scale=300, octaves=2, seed=seed + 2)
    dust_mask = np.clip((dust - 0.55) * 2.5, 0, 1) * 0.06
    arr *= (1 - dust_mask[..., None] * 0.3)
    save_rgb(arr, os.path.join(OUT, out_name))


def make_flat_linoleum(base_rgb, out_name, seed=20):
    """Linoleum floor: base color + faint regular tile grout grid + subtle wear.
    Tile size ~ 60cm assuming 1 texture instance == 2.4m -> 4x4 grid."""
    base = np.asarray(base_rgb, dtype=np.float32) / 255.0
    arr = np.ones((SZ, SZ, 3), dtype=np.float32) * base[None, None, :]
    # 4x4 grid grout lines, very faint, 4 pixels wide
    grid = np.zeros((SZ, SZ), dtype=np.float32)
    tile = SZ // 4
    line_w = 4
    for i in range(5):
        x = (i * tile) % SZ
        grid[:, max(0, x - line_w // 2):x + line_w // 2] = 1.0
        grid[max(0, x - line_w // 2):x + line_w // 2, :] = 1.0
    grid = np.asarray(Image.fromarray((grid * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.5))) / 255.0
    grout_color = np.array([0.55, 0.52, 0.47], dtype=np.float32)
    arr = arr * (1 - grid[..., None] * 0.45) + grout_color[None, None, :] * (grid[..., None] * 0.45)
    # Per-tile gentle shade variance
    rng = np.random.default_rng(seed)
    for ix in range(4):
        for iy in range(4):
            shade = rng.uniform(0.96, 1.04)
            x0, y0 = ix * tile + line_w, iy * tile + line_w
            x1, y1 = (ix + 1) * tile - line_w, (iy + 1) * tile - line_w
            arr[y0:y1, x0:x1] *= shade
    # Low-freq wear (no streaks)
    wear = tileable_value_noise(SZ, SZ, scale=400, octaves=3, seed=seed + 1)
    wear_mask = np.clip((wear - 0.45) * 2.0, 0, 1) * 0.08
    arr *= (1 - wear_mask[..., None])
    # Micro grain
    micro = tileable_value_noise(SZ, SZ, scale=12, octaves=2, seed=seed + 2)
    arr *= (0.96 + 0.04 * micro[..., None])
    save_rgb(arr, os.path.join(OUT, out_name))


def make_flat_concrete(base_rgb, out_name, seed=30):
    """Basement concrete: still mostly flat, just darker with mild patches.
    No water streaks (those become decals instead)."""
    base = np.asarray(base_rgb, dtype=np.float32) / 255.0
    arr = np.ones((SZ, SZ, 3), dtype=np.float32) * base[None, None, :]
    # Slow undulation for damp patches (isotropic, no streaks)
    damp = tileable_value_noise(SZ, SZ, scale=700, octaves=3, seed=seed)
    damp_mask = np.clip((damp - 0.45) * 1.8, 0, 1) * 0.18
    arr *= (1 - damp_mask[..., None])
    # Speckle texture
    speck = tileable_value_noise(SZ, SZ, scale=16, octaves=2, seed=seed + 1)
    arr *= (0.92 + 0.10 * speck[..., None])
    # Dark patches (mold, NOT streaks)
    mold = tileable_value_noise(SZ, SZ, scale=250, octaves=3, seed=seed + 2)
    mold_mask = np.clip((mold - 0.60) * 4, 0, 1) * 0.20
    mold_color = np.array([0.45, 0.50, 0.42], dtype=np.float32)
    arr = arr * (1 - mold_mask[..., None]) + mold_color[None, None, :] * mold_mask[..., None]
    save_rgb(arr, os.path.join(OUT, out_name))


def make_ceiling(base_rgb, out_name, seed=40):
    """Ceiling: like wall but with faint water-stain bloom (subtle)."""
    arr = np.ones((SZ, SZ, 3), dtype=np.float32) * (np.asarray(base_rgb, dtype=np.float32) / 255.0)
    age = tileable_value_noise(SZ, SZ, scale=400, octaves=3, seed=seed)
    yellow = np.clip((age - 0.4) * 2, 0, 1) * 0.10
    arr *= (1 - yellow[..., None] * 0.3)
    arr = arr * (1 - yellow[..., None]) + np.array([0.85, 0.78, 0.55])[None, None, :] * yellow[..., None]
    micro = tileable_value_noise(SZ, SZ, scale=10, octaves=2, seed=seed + 1)
    arr *= (0.97 + 0.03 * micro[..., None])
    save_rgb(arr, os.path.join(OUT, out_name))


def main():
    print("Generating flat plaster textures...")
    # Patient room / corridor upper: RGB 225,220,205 (cream plaster)
    make_flat_plaster((225, 220, 205), "Plaster_Wall.png", noise_amp=0.04, age_amp=0.05, seed=10)
    # Wainscot green
    make_flat_plaster((118, 145, 110), "Plaster_Wall_Green.png", noise_amp=0.04, age_amp=0.06, seed=11)
    # Linoleum floor: RGB 175,168,152
    make_flat_linoleum((175, 168, 152), "Floor_Linoleum_Flat.png", seed=20)
    # Slightly darker variant for older floors (3F)
    make_flat_linoleum((155, 148, 132), "Floor_Linoleum_Worn.png", seed=21)
    # Basement concrete
    make_flat_concrete((110, 105, 100), "Concrete_Basement_Flat.png", seed=30)
    # Ceiling
    make_ceiling((230, 225, 215), "Plaster_Ceiling.png", seed=40)
    print("Done.")


if __name__ == "__main__":
    main()

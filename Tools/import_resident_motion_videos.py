"""Prepare the three supplied resident videos as transparent, registered UI frames.

Requires OpenCV, NumPy and Pillow. Produces staging assets outside Assets; the
Editor installer creates clips and controllers after copying the staged folders.
No source video or existing frame is overwritten.
"""
import argparse
import hashlib
import json
import sys
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--source", type=Path, required=True)
parser.add_argument("--opencv-package-root", type=Path)
args = parser.parse_args()
if args.opencv_package_root:
    sys.path.insert(0, str(args.opencv_package_root))
import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont

cv2.setNumThreads(2)
PROJECT = Path(__file__).resolve().parents[1]
OUT = PROJECT / "Builds/Verification/NewResidentMotion"
OUT.mkdir(parents=True, exist_ok=True)
GROUPS = [("开心吃饭", "FoodHappy", "Food"), ("认真操作", "GameFocused", "Game"), ("展开报纸", "NewsUnfold", "News")]


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def matte(rgb):
    # Only remove near-white pixels connected to the outside. Cream body interiors
    # enclosed by the ink outline remain opaque, including eyes and paper panels.
    white = (rgb.min(2) >= 240) & (np.ptp(rgb.astype(np.int16), axis=2) <= 20)
    flood = np.pad(white.astype(np.uint8), 1, constant_values=1)
    cv2.floodFill(flood, None, (0, 0), 2)
    foreground = (flood[1:-1, 1:-1] != 2).astype(np.uint8)
    count, labels, cc, _ = cv2.connectedComponentsWithStats(foreground, 8)
    main = 1 + np.argmax(cc[1:, 4])
    # Keep detached hands/food grains near the subject; discard isolated codec dust.
    x, y, w, h = cc[main, :4]
    nearby = [i for i in range(1, count) if cc[i, 4] >= 12 and
              cc[i, 0] >= x - 24 and cc[i, 1] >= y - 24 and
              cc[i, 0] + cc[i, 2] <= x + w + 24 and cc[i, 1] + cc[i, 3] <= y + h + 24]
    mask = np.isin(labels, nearby)
    alpha = mask.astype(np.uint8) * 255
    clean = rgb.copy()
    eroded = cv2.erode(mask.astype(np.uint8), np.ones((5, 5), np.uint8)) > 0
    dark = mask & (rgb.max(2) < 80)
    near_ink = cv2.dilate(dark.astype(np.uint8), np.ones((7, 7), np.uint8)) > 0
    edge = mask & ~eroded & near_ink & (rgb.min(2) > 95) & (np.ptp(rgb.astype(np.int16), axis=2) < 30)
    if edge.any() and dark.any():
        ink = np.median(rgb[dark], axis=0)
        opacity = np.clip((255 - rgb[edge].astype(float).mean(1)) / (255 - ink.mean()), 1/255, 1)
        av = np.clip(np.rint(opacity * 255), 1, 255).astype(np.uint8)
        alpha[edge] = av
        restored = (rgb[edge].astype(float) - 255 * (1 - av[:, None] / 255)) / (av[:, None] / 255)
        clean[edge] = np.clip(np.rint(restored), 0, 255).astype(np.uint8)
    assert np.array_equal(clean[mask & ~edge], rgb[mask & ~edge])
    clean[alpha == 0] = 0
    return np.dstack((clean, alpha))


def register(rgba, transform):
    # Resample premultiplied colour to prevent a white or dark fringe on transparent edges.
    alpha = rgba[:, :, 3:4].astype(np.float32) / 255
    premultiplied = np.concatenate((rgba[:, :, :3] * alpha, alpha * 255), axis=2)
    fitted = cv2.warpAffine(premultiplied, transform, (960, 960), flags=cv2.INTER_LINEAR,
                           borderMode=cv2.BORDER_CONSTANT, borderValue=(0, 0, 0, 0))
    a = fitted[:, :, 3:4] / 255
    rgb = fitted[:, :, :3] / np.maximum(a, 1/255)
    rgb[a[:, :, 0] == 0] = 0
    return np.clip(np.rint(np.concatenate((rgb, fitted[:, :, 3:4]), axis=2)), 0, 255).astype(np.uint8)


records = []
sheet = Image.new("RGB", (1500, 1050), "#e7dcc8")
draw = ImageDraw.Draw(sheet)
font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 21)
for row, (name, identifier, base) in enumerate(GROUPS):
    source = args.source / (name + ".mp4")
    source_hash = digest(source)
    cap = cv2.VideoCapture(str(source))
    fps, expected = cap.get(cv2.CAP_PROP_FPS), int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
    native = []
    while True:
        ok, bgr = cap.read()
        if not ok:
            break
        native.append(matte(cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB)))
    cap.release()
    assert len(native) == expected == 121 and fps == 24
    bounds = np.array([Image.fromarray(frame).getbbox() for frame in native])
    union = np.array([bounds[:, 0].min(), bounds[:, 1].min(), bounds[:, 2].max(), bounds[:, 3].max()])
    reference = Image.open(PROJECT / f"Assets/EveryoneRecommends/ResidentMotion/{base}/Frames/{base}_0000.png")
    reference_box = reference.getbbox()
    scale = (reference_box[3] - reference_box[1]) / (union[3] - union[1])
    dx = (reference_box[0] + reference_box[2]) / 2 - scale * (union[0] + union[2]) / 2
    dy = reference_box[3] - scale * union[3]
    transform = np.array([[scale, 0, dx], [0, scale, dy]], dtype=np.float32)
    destination = OUT / "StagedVariants" / identifier / "Frames"
    destination.mkdir(parents=True, exist_ok=True)
    frames = []
    frame_records = []
    for index, frame in enumerate(native):
        image = Image.fromarray(register(frame, transform))
        box = image.getbbox()
        assert box and min(box[0], box[1], 960 - box[2], 960 - box[3]) >= 4, (identifier, index, box)
        path = destination / f"{identifier}_{index:04d}.png"
        image.save(path, compress_level=4)
        frame_records.append(dict(frame=index, bbox=box, sha256=digest(path)))
        frames.append(image)
    previews = []
    for frame in frames:
        background = Image.new("RGBA", frame.size, "#d5bc95")
        background.alpha_composite(frame)
        background.thumbnail((384, 384), Image.Resampling.LANCZOS)
        previews.append(background.convert("RGB"))
    previews[0].save(OUT / (name + "_透明动效预览.png"), save_all=True, append_images=previews[1:],
                     duration=1000/fps, loop=0, disposal=0, blend=0)
    for column, index in enumerate([0, 30, 60, 90, 120]):
        # Checker background makes both leftover white rectangles and missing cream visible.
        tile = Image.new("RGBA", (960, 960), "#bdc7b8")
        td = ImageDraw.Draw(tile)
        for y in range(0, 960, 60):
            for x in range(0, 960, 60):
                if (x//60 + y//60) % 2:
                    td.rectangle((x, y, x+59, y+59), fill="#e8dfc9")
        tile.alpha_composite(frames[index]); tile.thumbnail((296, 296), Image.Resampling.LANCZOS)
        sheet.paste(tile.convert("RGB"), (column*300+2, row*350+45))
    draw.text((8, row*350+6), f"{name}  /  {identifier}  121帧 · 24fps · 固定脚底位置", fill="#211c19", font=font)
    assert digest(source) == source_hash
    records.append(dict(name=name, id=identifier, source=str(source), source_sha256=source_hash,
                        native_size=list(native[0].shape[:2][::-1]), canvas=[960, 960], fps=fps,
                        frames=expected, seconds=expected/fps, source_union=union.tolist(),
                        original_reference_bounds=reference_box, fixed_scale=scale, fixed_offset=[dx, dy],
                        loop_end_mean_rgba_difference=float(np.mean(np.abs(np.asarray(frames[0]).astype(float)-np.asarray(frames[-1])))),
                        source_unchanged=True, all_frames_have_transparent_border=True, frame_records=frame_records))
    del native
    print(f"{identifier}: {len(frames)} transparent frames ready", flush=True)
sheet.save(OUT / "新增三组动效_透明与尺寸检查.png")
(OUT / "ConversionAudit.json").write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding="utf8")
print("CONVERSION_COMPLETE", flush=True)

"""Add a quiet, original drum arrangement to existing music without rebuilding SFX.

Always read an unmodified backup: rendering twice must not stack drum tracks.
Example: python Tools/add_soft_groove.py --source-folder Design/DemoBackup_20261014_SoftGroove/Assets/Audio/Music
"""
import argparse
import hashlib
import json
import math
import wave
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parents[1]
RATE = 44100
RECIPES = (
    ("MenuCourtyardLoop.wav", 84, 16, -13.5, 218414),
    ("ResidentsWindowLoop.wav", 88, 24, -10.0, 6100614),
)


def read(path):
    with wave.open(str(path), "rb") as f:
        assert (f.getnchannels(), f.getsampwidth(), f.getframerate()) == (2, 2, RATE)
        return np.frombuffer(f.readframes(f.getnframes()), "<i2").reshape(-1, 2) / 32768.0


def write(path, audio):
    path.parent.mkdir(parents=True, exist_ok=True)
    pcm = np.round(np.clip(audio, -1, 1) * 32767).astype("<i2")
    with wave.open(str(path), "wb") as f:
        f.setparams((2, 2, RATE, len(pcm), "NONE", "not compressed"))
        f.writeframes(pcm.tobytes())


def rms(audio):
    return float(np.sqrt(np.mean(audio * audio)))


def db(value):
    return float(20 * np.log10(max(float(value), 1e-12)))


def kit(kind, rng):
    duration = {"kick": .29, "rim": .12, "shaker": .065, "brush": .15}[kind]
    t = np.arange(round(duration * RATE)) / RATE
    if kind == "kick":
        # Rounded attack, audible low harmonics, no bright click or sub-bass thump.
        phase = 2 * np.pi * (57 * t + 54 * .027 * (1 - np.exp(-t / .027)))
        voice = (np.sin(phase) + .23 * np.sin(2 * phase)) * np.exp(-t / .070)
        envelope = 1 - np.exp(-t / .0038)
    elif kind == "rim":
        voice = (np.sin(2 * np.pi * 392 * t) * np.exp(-t / .018)
                 + .34 * np.sin(2 * np.pi * 863 * t) * np.exp(-t / .012))
        noise = rng.normal(size=len(t))
        voice += .10 * np.convolve(noise, np.ones(5) / 5, mode="same") * np.exp(-t / .015)
        envelope = 1 - np.exp(-t / .0018)
    else:
        noise = rng.normal(size=len(t))
        # Band-shaped paper/brush grain matches the game's existing soft interface sounds.
        voice = (np.convolve(noise, np.ones(4) / 4, mode="same")
                 - np.convolve(noise, np.ones(25) / 25, mode="same"))
        voice *= np.exp(-t / (.013 if kind == "shaker" else .032))
        envelope = 1 - np.exp(-t / .002)
    signal = voice * envelope * np.minimum(1, (duration - t) / .012)
    return signal / max(float(np.max(np.abs(signal))), 1e-9)


def smooth_loop(audio):
    # Only six milliseconds across the join; keep the room tails, no fade to silence.
    n = round(.003 * RATE)
    common = (audio[0] + audio[-1]) / 2
    ramp = np.linspace(0, 1, n)
    ramp = ramp * ramp * (3 - 2 * ramp)
    audio[:n] = common + (audio[:n] - common) * ramp[:, None]
    audio[-n:] = common + (audio[-n:] - common) * ramp[::-1, None]


def render(source_folder, output_folder, report_folder):
    reports = []
    for filename, bpm, bars, relative_db, seed in RECIPES:
        source = source_folder / filename
        output = output_folder / filename
        if source.resolve() == output.resolve():
            raise ValueError("Source must be an unmodified backup, not the active output.")
        dry = read(source)
        assert abs(len(dry) / RATE - bars * 4 * 60 / bpm) < 1 / RATE
        rng = np.random.default_rng(seed)
        beat = 60 / bpm
        drums = np.zeros_like(dry)
        game = filename.startswith("Residents")

        def hit(kind, position, gain, pan=0):
            signal = kit(kind, rng) * gain
            indices = (round(position * beat * RATE) + np.arange(len(signal))) % len(drums)
            drums[indices, 0] += signal * math.sqrt((1 - pan) / 2)
            drums[indices, 1] += signal * math.sqrt((1 + pan) / 2)

        for bar in range(bars):
            at = bar * 4
            quieter = game and 8 <= bar < 16
            # Clear pulse with a small offbeat pickup. No busy fills on every bar.
            hit("kick", at, 1.0)
            hit("kick", at + (2.5 if game else 2), .68 if game else .57)
            if game and bar % 4 == 2 and not quieter:
                hit("kick", at + 1.75, .36)
            for step in (1, 3):
                hit("rim", at + step + .012, .40 if game else .25, -.10)
                hit("brush", at + step + .025, .12 if game else .09, .20)
            for eighth in range(8):
                if (quieter or not game) and eighth % 2 == 0:
                    continue
                swing = .038 if eighth % 2 else 0
                timing = float(rng.uniform(-.006, .006))
                gain = (.105 if eighth % 2 else .060) * float(rng.uniform(.85, 1.1))
                hit("shaker", at + eighth * .5 + swing + timing, gain, -.28 if eighth % 2 else .28)
            if bar % 8 == 7 and not quieter:
                hit("rim", at + 3.76, .12 if game else .07, .10)

        drums -= drums.mean(axis=0)
        drums *= rms(dry) * 10 ** (relative_db / 20) / rms(drums)
        mixed = dry + drums
        # Preserve average listening level and headroom instead of making the track louder.
        gain = min(rms(dry) / rms(mixed), .85 / float(np.max(np.abs(mixed))))
        mixed *= gain
        smooth_loop(mixed)
        write(output, mixed)
        actual = read(output)
        preview = actual[:round(24 * RATE)].copy()
        fade = np.linspace(1, 0, round(.35 * RATE))
        preview[-len(fade):] *= fade[:, None]
        write(report_folder / (output.stem + "_试听24秒.wav"), preview)
        write(report_folder / (output.stem + "_DrumsOnly.wav"), drums * gain)
        checks = {
            "duration_and_channels_preserved": actual.shape == dry.shape,
            "unclipped": float(np.max(np.abs(actual))) < .85,
            "rms_change_under_0_2_db": abs(db(rms(actual)) - db(rms(dry))) < .2,
            "loop_join_under_one_pcm_step": float(np.max(np.abs(actual[0] - actual[-1]))) <= 1 / 32768,
            "original_melody_correlation_above_0_94": float(np.corrcoef(dry.ravel(), actual.ravel())[0, 1]) > .94,
        }
        reports.append(dict(file=filename, bpm=bpm, bars=bars, seconds=len(actual) / RATE,
                            original_rms_dbfs=db(rms(dry)), output_rms_dbfs=db(rms(actual)),
                            peak_dbfs=db(np.max(np.abs(actual))), drum_to_original_rms_db=relative_db,
                            original_melody_gain=float(gain),
                            correlation=float(np.corrcoef(dry.ravel(), actual.ravel())[0, 1]),
                            seam_step=float(np.max(np.abs(actual[0] - actual[-1]))),
                            source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                            output_sha256=hashlib.sha256(output.read_bytes()).hexdigest(), checks=checks))
        assert all(checks.values()), reports[-1]
    report_folder.mkdir(parents=True, exist_ok=True)
    (report_folder / "AudioGrooveAudit.json").write_text(
        json.dumps(dict(recipe="soft-groove-v1", sources="original synthesized percussion; existing music stems retained",
                        tracks=reports), ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(reports, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-folder", type=Path, required=True)
    parser.add_argument("--output-folder", type=Path, default=ROOT / "Assets/Audio/Music")
    parser.add_argument("--report-folder", type=Path, default=ROOT / "Builds/Verification/AudioGroove")
    args = parser.parse_args()
    render(args.source_folder, args.output_folder, args.report_folder)

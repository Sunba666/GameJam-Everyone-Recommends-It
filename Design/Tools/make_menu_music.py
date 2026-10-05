"""Original, deterministic 16-bar instrumental loop; no external recordings."""
from pathlib import Path
import json
import wave
import numpy as np

RATE = 44100
BPM = 84
BEAT = 60 / BPM
LENGTH = 16 * 4 * BEAT
COUNT = round(LENGTH * RATE)
mix = np.zeros((COUNT, 2), np.float64)
rng = np.random.default_rng(2184)

def note(midi, beat, duration, gain, pan=0, kind="keys"):
    f = 440 * 2 ** ((midi - 69) / 12)
    t = np.arange(round(duration * RATE)) / RATE
    if kind == "pad":
        env = (1-np.exp(-t/.28)) * np.minimum(1, (duration-t)/.75)
        sound = (np.sin(2*np.pi*f*t) + .22*np.sin(2*np.pi*(f*1.001)*t)
                 + .12*np.sin(2*np.pi*2*f*t)) * env
    elif kind == "bass":
        env = (1-np.exp(-t/.022)) * np.exp(-t/1.0) * np.minimum(1,(duration-t)/.12)
        sound = (np.sin(2*np.pi*f*t)+.17*np.sin(2*np.pi*2*f*t)) * env
    else:
        # Soft electric-piano/wooden key timbre, with a rounded attack and decaying overtones.
        sound = np.zeros_like(t)
        for harmonic, weight, decay in [(1,1,1.5),(2,.32,.85),(3,.10,.42),(4,.025,.2)]:
            sound += weight * np.sin(2*np.pi*f*harmonic*t) * np.exp(-t/decay)
        sound *= (1-np.exp(-t/.012)) * np.minimum(1,(duration-t)/.08)
    mono = sound * gain
    stereo = np.column_stack([mono*np.sqrt((1-pan)/2), mono*np.sqrt((1+pan)/2)])
    start = round(beat*BEAT*RATE)
    ids = (start + np.arange(len(t))) % COUNT
    mix[ids] += stereo

# Cmaj9 / Am7 / Fmaj9 / G6. Two eight-bar phrases with a small answering variation.
chords = [(48,[60,64,67,71]),(45,[60,64,67,69]),(41,[57,60,64,67]),(43,[59,62,67,69])]
melodies = [
 [(0,76,1.2),(1.5,74,.7),(2.5,72,1.4)],
 [(.5,71,1),(2,72,1.8)],
 [(0,72,1.1),(1.5,76,.7),(2.5,79,1.3)],
 [(0,74,1.7),(2.5,71,1.2)],
 [(0,76,.8),(1,79,1.2),(2.5,76,1.1)],
 [(.5,74,1),(2,72,1.7)],
 [(0,69,1),(1.5,72,.8),(2.5,76,1.2)],
 [(0,74,1.4),(2,71,.9),(3,67,.8)],
]
for bar in range(16):
    root, chord = chords[bar % 4]
    at = bar*4
    note(root,at,2.6,.115,-.06,"bass")
    note(root+12,at+2,1.55,.040,.08,"bass")
    for k,pitch in enumerate(chord):
        note(pitch-12,at,4*BEAT+.5,.018,(k-1.5)*.25,"pad")
    for k in range(8):
        pitch = chord[[0,2,1,3,0,2,1,2][k]]
        note(pitch,at+k*.5+.015,1.8,.033 if k%2 else .044,(k%3-1)*.28)
    melody = melodies[bar%8]
    for offset,pitch,dur in melody:
        if bar>=8 and bar%4==0: pitch += 0 if offset==0 else 2
        note(pitch,at+offset, max(1.8,dur*BEAT+.65),.102+float(rng.uniform(-.006,.006)),.12)

# Circular room reflections keep the reverb tail continuous at the loop seam.
dry = mix.copy()
for delay, level in [(.071,.14),(.127,.095),(.213,.065),(.337,.04)]:
    mix += np.roll(dry[:,::-1],round(delay*RATE),axis=0)*level
mix -= mix.mean(axis=0)
peak = np.max(np.abs(mix))
mix *= .52/peak
out = Path(__file__).resolve().parents[2] / "Assets/Audio/Music/MenuCourtyardLoop.wav"
out.parent.mkdir(parents=True,exist_ok=True)
pcm = np.round(np.clip(mix,-1,1)*32767).astype('<i2')
with wave.open(str(out),'wb') as w:
    w.setparams((2,2,RATE,COUNT,'NONE','not compressed')); w.writeframes(pcm.tobytes())
report = dict(file=out.name,seconds=COUNT/RATE,bpm=BPM,bars=16,channels=2,
              peak_dbfs=float(20*np.log10(np.max(np.abs(mix)))),
              rms_dbfs=float(20*np.log10(np.sqrt(np.mean(mix**2)))),
              seam_delta=float(np.max(np.abs(mix[0]-mix[-1]))),clipped_samples=int(np.sum(np.abs(pcm)==32767)))
(out.parents[3]/"Design/menu-music-audit.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
print(json.dumps(report))

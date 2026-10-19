"""Original apartment music and five short earcons; no downloaded samples."""
import json, math, wave
from pathlib import Path
import numpy as np

ROOT=Path(__file__).resolve().parents[1]
RATE=44100
beat=60/88
bars=24
length=round(bars*4*beat*RATE)
mix=np.zeros((length,2),dtype=np.float64)
rng=np.random.default_rng(20261006)

def hz(note): return 440*2**((note-69)/12)
def voice(note,duration,kind):
    t=np.arange(round(duration*RATE))/RATE
    phase=2*np.pi*hz(note)*t
    attack=1-np.exp(-t/(.012 if kind=='flute' else .006))
    release=np.minimum(1,(duration-t)/.07)
    if kind=='flute':
        phase+=.045*np.sin(2*np.pi*4.6*t)
        signal=.78*np.sin(phase)+.17*np.sin(2*phase)+.05*np.sin(3*phase)
        envelope=attack*release*(.8+.2*np.exp(-t))
    elif kind=='keys':
        signal=np.sin(phase)+.24*np.sin(2*phase)*np.exp(-t*3)+.1*np.sin(3.01*phase)*np.exp(-t*5)
        envelope=attack*release*np.exp(-t*1.5)
    elif kind=='wood':
        signal=np.sin(phase)+.22*np.sin(2.76*phase)*np.exp(-t*6)+.08*np.sin(4.05*phase)*np.exp(-t*9)
        envelope=attack*release*np.exp(-t*3.8)
    elif kind=='pad':
        signal=.6*np.sin(phase)+.2*np.sin(phase*1.002)+.2*np.sin(phase*.998)
        envelope=(1-np.exp(-t/.24))*np.minimum(1,(duration-t)/.35)
    else:
        signal=np.sin(phase)+.15*np.sin(2*phase)
        envelope=attack*release*np.exp(-t*2.3)
    return signal*envelope

def add(signal,at,gain,pan=0):
    indices=(round(at*RATE)+np.arange(len(signal)))%length
    mix[indices,0]+=signal*gain*math.sqrt((1-pan)/2)
    mix[indices,1]+=signal*gain*math.sqrt((1+pan)/2)

chords=[[48,55,59,64,67],[45,52,55,60,64],[50,57,60,64,69],[43,53,57,59,64],
        [52,59,62,67],[45,55,59,64],[41,53,57,60,64],[43,55,59,62,69]]
# A twelve-bar phrase, then a lighter answering phrase. MIDI pitches and rests are explicit.
melodies=[[72,76,79,76,74,72],[71,72,76,74,72,69],[69,72,77,76,74,72],[71,74,76,74,71,67],
          [71,74,79,76,74,71],[72,76,74,72,69,67],[69,72,76,77,76,72],[74,76,79,77,74,71],
          [76,79,81,79,76,74],[72,76,79,76,72,69],[74,77,76,74,72,69],[71,74,76,74,71,67]]
for bar in range(bars):
    at=bar*4*beat;chord=chords[bar%8];quiet=8<=bar<16
    for j,note in enumerate(chord):
        add(voice(note+12,4.45*beat,'pad'),at,.012 if quiet else .016,(j-2)*.18)
        add(voice(note+12,2.3*beat,'keys'),at+(j*.045),.045,(j-2)*.16)
        if not quiet:add(voice(note+12,1.7*beat,'keys'),at+2.5*beat+j*.03,.022,(2-j)*.16)
    add(voice(chord[0]-12,1.8*beat,'bass'),at,.105)
    add(voice(chord[0]-5,1.4*beat,'bass'),at+2*beat,.07)
    for j,note in enumerate(melodies[bar%12]):
        if quiet and j in (1,4):continue
        onset=at+(.35+j*.55)*beat
        add(voice(note,.62*beat,'flute' if bar<12 else 'wood'),onset,.055 if quiet else .075,.18 if bar<12 else -.22)
    # Soft brushed taps, with less movement in the middle eight bars.
    for step in (1,3):
        if quiet and step==3:continue
        t=np.arange(round(.09*RATE))/RATE
        noise=rng.normal(0,1,len(t));brush=np.convolve(noise,np.ones(7)/7,mode='same')
        add(brush*np.exp(-t*42)*(1-np.exp(-t*600)),at+step*beat,.018,-.35)

# Circular delays keep the tails that cross the end at the start of the next loop.
original=mix.copy()
for delay,gain in ((.173,.17),(.291,.12),(.437,.065)):
    mix+=np.roll(original,round(delay*RATE),axis=0)[:,::-1]*gain
mix-=mix.mean(axis=0)
mix=np.tanh(mix*1.6)
mix*=.73/max(np.max(np.abs(mix)),.01)

def write(path,samples):
    path.parent.mkdir(parents=True,exist_ok=True)
    samples=np.asarray(samples)
    with wave.open(str(path),'wb') as output:
        output.setnchannels(1 if samples.ndim==1 else samples.shape[1]);output.setsampwidth(2);output.setframerate(RATE)
        output.writeframes((np.clip(samples,-1,1)*32767).astype('<i2').tobytes())

music=ROOT/'Assets/Audio/Music/ResidentsWindowLoop.wav'
write(music,mix)
sound_names=['Cat','Food','Game','News','Music']
sounds=[]
for topic,name in enumerate(sound_names):
    duration=.58;t=np.arange(round(duration*RATE))/RATE;signal=np.zeros_like(t)
    if topic==0:
        f=520+220*np.sin(np.minimum(t/.31,1)*np.pi)
        phase=2*np.pi*np.cumsum(f)/RATE
        signal=(np.sin(phase)+.17*np.sin(2*phase))*(1-np.exp(-t*90))*np.exp(-t*7)
    elif topic==1:
        for start,note in ((0,79),(.14,84)):
            v=voice(note,.23,'wood');offset=round(start*RATE);signal[offset:offset+len(v)]+=v
        signal+=rng.normal(0,1,len(t))*.04*np.exp(-t*48)
    elif topic==2:
        for start,note in ((0,72),(.095,79),(.19,84)):
            v=voice(note,.22,'keys');offset=round(start*RATE);signal[offset:offset+len(v)]+=v*.8
    elif topic==3:
        noise=rng.normal(0,1,len(t));paper=noise-np.convolve(noise,np.ones(21)/21,mode='same')
        signal=paper*.18*(1-np.exp(-t*120))*np.exp(-t*10)*(1+.3*np.sin(t*55))
        v=voice(76,.20,'wood');signal[round(.23*RATE):round(.23*RATE)+len(v)]+=v*.18
    else:
        for start,note in ((0,72),(.08,76),(.16,79)):
            v=voice(note,.36,'wood');offset=round(start*RATE);signal[offset:offset+len(v)]+=v*.55
    signal*=np.minimum(1,(duration-t)/.04)
    signal-=signal.mean();signal*=.68/max(np.max(np.abs(signal)),.01)
    path=ROOT/f'Assets/Audio/SFX/Recommend_{name}.wav';write(path,signal);sounds.append(str(path.relative_to(ROOT)))

report={'title':'楼间晚风','tempo_bpm':88,'bars':bars,'seconds':length/RATE,
        'channels':2,'sample_rate':RATE,'peak':float(np.max(np.abs(mix))),
        'loop_boundary_step':float(np.max(np.abs(mix[-1]-mix[0]))),
        'music':str(music.relative_to(ROOT)),'sounds':sounds,
        'instruments':['soft electric piano','breathy flute','wooden mallets','plucked bass','brushed taps'],
        'sources':'original synthesized score, no third-party recordings'}
out=ROOT/'Builds/Verification/Playability';out.mkdir(parents=True,exist_ok=True)
(out/'AudioComposition.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False))

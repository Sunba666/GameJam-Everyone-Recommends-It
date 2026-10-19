"""Extract the supplied cats and floating notes into registered transparent UI layers.

Only white video backing is removed. Sources and existing resident frames are
never changed. Geometry is fixed per sequence so breathing does not recenter it.
"""
import hashlib
import json
import sys
from pathlib import Path
sys.path.insert(0, r'C:\Users\asus\.codex\art-work\menu-animations-20261003\dependencies')
import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont

cv2.setNumThreads(2)
PROJECT = Path(__file__).resolve().parents[1]
SOURCE = Path(r'E:\《大家都推荐》-GameJam')
OUT = PROJECT / 'Builds/Verification/TopicPropMotion'
ROOT = PROJECT / 'Assets/EveryoneRecommends/ResidentMotion/Props'
GROUPS = [('坐姿可拥抱待机', 'CatSitting', (512,512)),
          ('完整趴卧待机', 'CatLying', (512,320)),
          ('音符组合 A 上浮动效', 'MusicNotes', (512,512))]


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def transparent_frame(rgb):
    # Both outside backing and white negative spaces (inside notes/tail) are
    # transparent. The cats' cream fur is coloured and survives this criterion.
    white = (rgb.min(2) >= 240) & (np.ptp(rgb.astype(np.int16),axis=2) <= 20)
    count,labels,stats,_ = cv2.connectedComponentsWithStats((~white).astype(np.uint8),8)
    # Keep every substantive component: notes are three disconnected glyphs.
    selected = [i for i in range(1,count) if stats[i,4]>=18]
    mask = np.isin(labels,selected)
    alpha = mask.astype(np.uint8)*255
    clean=rgb.copy()
    eroded=cv2.erode(mask.astype(np.uint8),np.ones((5,5),np.uint8))>0
    dark=mask & (rgb.max(2)<80)
    near_ink=cv2.dilate(dark.astype(np.uint8),np.ones((7,7),np.uint8))>0
    edge=mask & ~eroded & near_ink & (rgb.min(2)>95) & (np.ptp(rgb.astype(np.int16),axis=2)<30)
    if edge.any() and dark.any():
        ink=np.median(rgb[dark],axis=0)
        opacity=np.clip((255-rgb[edge].astype(float).mean(1))/(255-ink.mean()),1/255,1)
        av=np.clip(np.rint(opacity*255),1,255).astype(np.uint8)
        alpha[edge]=av
        clean[edge]=np.clip(np.rint((rgb[edge].astype(float)-255*(1-av[:,None]/255))/(av[:,None]/255)),0,255).astype(np.uint8)
    clean[alpha==0]=0
    return np.dstack((clean,alpha))


def fit(frame, matrix, canvas):
    alpha=frame[:,:,3:4].astype(np.float32)/255
    premult=np.concatenate((frame[:,:,:3]*alpha,alpha*255),axis=2)
    fitted=cv2.warpAffine(premult,matrix,canvas,flags=cv2.INTER_LINEAR,borderMode=cv2.BORDER_CONSTANT,borderValue=(0,0,0,0))
    a=fitted[:,:,3:4]/255
    rgb=fitted[:,:,:3]/np.maximum(a,1/255)
    rgb[a[:,:,0]==0]=0
    return np.clip(np.rint(np.concatenate((rgb,fitted[:,:,3:4]),axis=2)),0,255).astype(np.uint8)


OUT.mkdir(parents=True,exist_ok=True)
sheet=Image.new('RGB',(1500,1040),'#e4d9c2')
draw=ImageDraw.Draw(sheet)
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',21)
records=[]
for row,(name,identifier,canvas) in enumerate(GROUPS):
    source=SOURCE/(name+'.mp4');source_hash=digest(source)
    cap=cv2.VideoCapture(str(source));fps=cap.get(cv2.CAP_PROP_FPS);expected=int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
    frames=[]
    while True:
        ok,bgr=cap.read()
        if not ok:break
        frames.append(transparent_frame(cv2.cvtColor(bgr,cv2.COLOR_BGR2RGB)))
    cap.release()
    assert len(frames)==expected==121 and fps==24
    bounds=np.array([Image.fromarray(frame).getbbox() for frame in frames])
    union=[int(bounds[:,0].min()),int(bounds[:,1].min()),int(bounds[:,2].max()),int(bounds[:,3].max())]
    if identifier=='MusicNotes':
        # Retain native off-screen entry/exit and all three notes. Padding protects
        # the texture edge; this does not attempt to invent clipped source pixels.
        scale=(canvas[0]-16)/720;dx=dy=8
    else:
        scale=min((canvas[0]-24)/(union[2]-union[0]),(canvas[1]-24)/(union[3]-union[1]))
        dx=canvas[0]/2-scale*(union[0]+union[2])/2
        dy=canvas[1]-12-scale*union[3]
    matrix=np.array([[scale,0,dx],[0,scale,dy]],np.float32)
    destination=ROOT/identifier/'Frames'
    if destination.exists() and list(destination.glob('*.png')):
        raise RuntimeError('Refusing to overwrite prior extracted frames: '+str(destination))
    destination.mkdir(parents=True,exist_ok=True)
    output=[];frame_records=[]
    for index,frame in enumerate(frames):
        result=Image.fromarray(fit(frame,matrix,canvas));bbox=result.getbbox()
        assert bbox and min(bbox[0],bbox[1],canvas[0]-bbox[2],canvas[1]-bbox[3])>=5,(identifier,index,bbox)
        file=destination/f'{identifier}_{index:04d}.png';result.save(file,compress_level=5)
        frame_records.append(dict(frame=index,bbox=bbox,sha256=digest(file)))
        output.append(result)
    for col,index in enumerate([0,30,60,90,120]):
        tile=Image.new('RGBA',canvas,'#45434d');td=ImageDraw.Draw(tile)
        for y in range(0,canvas[1],32):
            for x in range(0,canvas[0],32):
                if (x//32+y//32)%2:td.rectangle((x,y,x+31,y+31),fill='#dcd0b5')
        tile.alpha_composite(output[index]);tile.thumbnail((290,285),Image.Resampling.LANCZOS)
        sheet.paste(tile.convert('RGB'),(col*300+5,row*345+50))
    draw.text((8,row*345+7),f'{name} / {identifier} · 121帧 24fps · {canvas[0]}×{canvas[1]}',font=font,fill='#251e1c')
    preview=[]
    for frame in output:
        tile=Image.new('RGBA',canvas,'#e6d7b6');tile.alpha_composite(frame);tile.thumbnail((384,384),Image.Resampling.LANCZOS);preview.append(tile.convert('RGB'))
    preview[0].save(OUT/(identifier+'_Preview.png'),save_all=True,append_images=preview[1:],duration=1000/fps,loop=0,disposal=0,blend=0)
    assert digest(source)==source_hash
    record=dict(id=identifier,source=str(source),source_sha256=source_hash,source_unchanged=True,canvas=canvas,
                frames=expected,fps=fps,seconds=expected/fps,source_union=union,fixed_scale=scale,offset=[dx,dy],
                all_frames_have_transparent_border=True,preserves_source_offscreen_motion=identifier=='MusicNotes',frame_records=frame_records)
    (ROOT/identifier/'Conversion.json').write_text(json.dumps({k:v for k,v in record.items() if k!='frame_records'},ensure_ascii=False,indent=2),encoding='utf8')
    records.append(record)
    print(f'{identifier}: 121 frames, {canvas}, union={union}',flush=True)
sheet.save(OUT/'TransparentContactSheet.jpg')
(OUT/'ConversionAudit.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf8')
print('CONVERSION_COMPLETE',flush=True)

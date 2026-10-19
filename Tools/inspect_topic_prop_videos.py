"""Read source geometry and produce a contact sheet for the supplied prop videos."""
import sys
import json
from pathlib import Path
sys.path.insert(0, r'C:\Users\asus\.codex\art-work\menu-animations-20261003\dependencies')
import cv2
from PIL import Image, ImageDraw, ImageFont
cv2.setNumThreads(2)
root = Path(r'E:\《大家都推荐》-GameJam')
out = Path(__file__).resolve().parents[1] / 'Builds/Verification/TopicPropMotion'
out.mkdir(parents=True, exist_ok=True)
sheet = Image.new('RGB', (1500, 1020), '#d9d0bb')
draw = ImageDraw.Draw(sheet)
font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 22)
records=[]
for row, name in enumerate(['坐姿可拥抱待机', '完整趴卧待机', '音符组合 A 上浮动效']):
    cap = cv2.VideoCapture(str(root/(name+'.mp4')))
    count=int(cap.get(cv2.CAP_PROP_FRAME_COUNT)); fps=cap.get(cv2.CAP_PROP_FPS)
    size=[int(cap.get(cv2.CAP_PROP_FRAME_WIDTH)),int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))]
    records.append(dict(name=name,frames=count,fps=fps,size=size))
    draw.text((8,row*340+4),f'{name} | {count} frames / {fps} fps / {size}',font=font,fill='#201d18')
    for col, index in enumerate([0,count//4,count//2,count*3//4,count-1]):
        cap.set(cv2.CAP_PROP_POS_FRAMES,index);ok,bgr=cap.read();assert ok
        frame=Image.fromarray(cv2.cvtColor(bgr,cv2.COLOR_BGR2RGB));frame.thumbnail((296,296))
        sheet.paste(frame,(col*300+2,row*340+40))
    cap.release()
sheet.save(out/'SourceContactSheet.jpg')
(out/'SourceGeometry.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps(records,ensure_ascii=False))

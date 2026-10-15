from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,hashlib,collections
root=Path('Resources/Textures/_Forge/KIAS'); rows=[]; errors=[]
for f in sorted(root.rglob('meta.json')):
 m=json.loads(f.read_text()); size=m['size']; w,h=size['x'],size['y']; seen=set()
 for st in m['states']:
  name=st['name']; png=f.parent/(name+'.png')
  if name in seen:errors.append(f'duplicate: {f} {name}')
  seen.add(name)
  if not png.exists(): errors.append(f'missing: {png}'); continue
  with Image.open(png) as im:
   im.load(); width,height=im.size;frames=width//w*(height//h)
   if width%w or height%h:errors.append(f'nonintegral: {png}')
   if not im.convert('RGBA').getchannel('A').getbbox():errors.append(f'empty: {png}')
   need=sum(len(d) for d in st.get('delays',[[1] for _ in range(st.get('directions',1))]))
   if frames<need:errors.append(f'frames: {png} {frames}<{need}')
   rows.append({'file':png.as_posix(),'width':width,'height':height,'frames':frames,'required':need,'sha256':hashlib.sha256(png.read_bytes()).hexdigest()})
 thumb=f.parent/((next((s['name'] for s in m['states'] if s['name'] in ['base','core','controller','rack','empty']),m['states'][0]['name']))+'.png')
 print(f'{f.parent.as_posix()} {w}x{h} states={len(m["states"])} license={m.get("license")} copyright={m.get("copyright")}')
metas=sorted(root.rglob('meta.json')); sheet=Image.new('RGB',(1200,((len(metas)+7)//8)*130),'#d6d6d6'); draw=ImageDraw.Draw(sheet)
for i,f in enumerate(metas):
 m=json.loads(f.read_text()); st=next((s['name'] for s in m['states'] if s['name'] in ['base','core','controller','rack','empty']),m['states'][0]['name']); im=Image.open(f.parent/(st+'.png')).convert('RGBA').crop((0,0,m['size']['x'],m['size']['y'])); im.thumbnail((96,96)); im=im.resize((im.width*1,im.height*1)); x=(i%8)*150;y=(i//8)*130; sheet.paste(im,(x+(150-im.width)//2,y+10),im);draw.text((x+3,y+105),f.parent.name[:23],fill='black')
sheet.save('KIAS_REVIEW/rsi-contact-sheet.png')
Path('KIAS_REVIEW/rsi-validation.json').write_text(json.dumps({'rsiCount':len(metas),'stateCount':len(rows),'errors':errors,'states':rows},indent=2),encoding='utf-8'); print('ERRORS',errors)

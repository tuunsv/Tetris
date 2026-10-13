"""Nhạc/SFX tự soạn cho bài tập; không dùng audio rip của Elden Ring.
Chạy tùy chọn: python -m pip install numpy; python tools/make_boss_audio.py
Game đọc WAV có sẵn, không cần Python hay numpy trên máy người chơi.
"""
from pathlib import Path
import numpy as np
import wave,json
R=44100
ROOT=Path(__file__).resolve().parents[1]/'Assets'
ROOT.mkdir(exist_ok=True)
report=[]
def note(buf,start,dur,midi,amp=.1,kind='strings'):
 n=int(dur*R);t=np.arange(n)/R;f=440*2**((midi-69)/12);p=2*np.pi*f*t
 attack=.025;release=.12
 if kind=='strings':
  sig=(np.sin(p+.009*np.sin(2*np.pi*5*t))+.28*np.sin(p*2)+.15*np.sin(p*3)+.07*np.sin(p*4))/1.5;attack=.11;release=.24
 elif kind=='brass':sig=(np.sin(p)+.5*np.sin(2*p)+.2*np.sin(3*p)+.1*np.sin(4*p))/1.7;attack=.025
 elif kind=='bell':sig=(np.sin(p)+.32*np.sin(p*2.01)+.12*np.sin(p*3.98))*np.exp(-t*2.2);attack=.003;release=.08
 elif kind=='choir':sig=(np.sin(p)+.16*np.sin(p*3)+.08*np.sin(p*5))/1.24;attack=.22;release=.3
 else:sig=np.sin(p)*np.exp(-t*1.4);attack=.005
 env=np.minimum(1,t/attack)*np.minimum(1,np.maximum(0,dur-t)/release)
 idx=(int(start*R)+np.arange(n))%len(buf)
 np.add.at(buf,idx,(sig*env*amp).astype(np.float32))
def drum(buf,start,heavy=False,rng=None):
 rng=rng if rng is not None else np.random.default_rng(118)
 n=int(R*(.6 if heavy else .16));t=np.arange(n)/R
 noise=rng.normal(0,1,n);smooth=np.convolve(noise,np.ones(8)/8,mode='same')
 if heavy:sig=np.sin(2*np.pi*(44*t+8*(1-np.exp(-t*20))))*np.exp(-t*7)*.3+smooth*np.exp(-t*16)*.09
 else:sig=smooth*np.exp(-t*32)*.09
 sig*=np.minimum(1,t/.003)
 np.add.at(buf,(int(start*R)+np.arange(n))%len(buf),sig.astype(np.float32))
def save(name,buf,target=.72):
 # Echo quay vòng giữ cả đuôi reverb ở chỗ nối loop.
 peak=float(np.max(np.abs(buf)));buf=buf*(target/max(peak,1e-7))
 with wave.open(str(ROOT/(name+'.wav')),'wb') as w:
  w.setparams((1,2,R,0,'NONE','not compressed'));w.writeframes((np.clip(buf,-1,1)*32767).astype('<i2').tobytes())
 report.append({'name':name,'seconds':round(len(buf)/R,3),'peak':target,'rms':round(float(np.sqrt(np.mean(buf*buf))),4)})
# Từng boss có nhịp, giọng, câu giai điệu và phối khí khác nhau.
specs=[
 ('Godrick',96,45,[0,0,7,3,2,0,-2,0],'brass',[0,5,3,7]),
 ('Malenia',132,57,[0,2,3,7,10,7,3,2],'strings',[0,3,5,7]),
 ('Rennala',84,50,[12,7,10,5,7,3,2,0],'bell',[0,5,8,7]),
 ('Radagon',116,50,[0,7,12,10,7,5,3,7],'brass',[0,8,5,7]),
 ('Radahn',104,43,[0,0,5,7,3,2,7,0],'brass',[0,3,8,7]),
 ('Margit',100,47,[0,1,7,6,3,1,-1,0],'strings',[0,1,5,7]),
 ('Ranni',76,57,[12,10,7,3,5,7,2,0],'bell',[0,5,3,7]),
 ('Godfrey',110,45,[0,7,5,3,0,3,7,12],'brass',[0,5,8,7]),
 ('Gideon',112,48,[0,3,6,2,5,8,7,1],'bell',[0,3,1,7]),
 ('Maliketh',140,42,[0,1,7,6,0,3,1,-1],'strings',[0,1,8,7]),
 ('Mohg',92,46,[0,3,1,6,5,3,1,0],'choir',[0,1,5,7]),
 ('Morgott',108,49,[0,7,3,2,5,3,1,0],'strings',[0,5,1,7])]
for index,(name,bpm,root,motif,instrument,progression) in enumerate(specs):
 beat=60/bpm;length=16*4*beat;buf=np.zeros(round(length*R),np.float32);rng=np.random.default_rng(118+index)
 for bar in range(16):
  offset=progression[bar%4];base=root+offset;start=bar*4*beat
  # Basse et nappes. Les accords restent mineurs, avec dominante majeure au dernier degré.
  third=4 if offset==7 else 3
  for n in [base,base+7,base+12+third]:note(buf,start,4.3*beat,n,.048,'choir' if index in [2,6,10] else 'strings')
  note(buf,start,3.9*beat,base-12,.1,'bass')
  for step in range(8):
   pitch=root+12+motif[(step+(bar//4)*2)%8]
   if bar%4==3 and step>=6:pitch=root+12+[2,0][step-6]
   if bar>=8:pitch+=12 if index in [1,2,6] else 0
   note(buf,start+step*.5*beat,.46*beat,pitch,.09 if instrument!='bell' else .14,instrument)
  if index not in [2,6]:
   drum(buf,start,True,rng);drum(buf,start+2*beat,True,rng)
   for off in ([1,2.5,3] if index in [5,8,9] else [1,3]):drum(buf,start+off*beat,False,rng)
  else:
   note(buf,start+1.5*beat,2.5*beat,root+24+7,.075,'bell')
 dry=buf.copy();buf+=.17*np.roll(dry,int(.19*R))+.1*np.roll(dry,int(.37*R))+.055*np.roll(dry,int(.61*R))
 save('boss_'+str(index).zfill(2),buf,.55)
 print(name,round(length,1),'s',flush=True)
# SFX: nhiều tầng nhưng luôn chừa headroom trước khi trộn.
for name,pitches,duration in [
 ('single',[72,79],.3),('double',[72,76,84],.46),('triple',[72,76,79,88],.65),
 ('combo',[79,84,91],.4),('b2b',[67,74,79,86],.55),('perfect',[60,67,72,76,79,84,91],1.5),
 ('quest',[62,69,74,81],1.1),('warning',[70,70,70],.8),('bosshit',[45,38,33],1.2),
 ('bossintro',[38,45,50],2.4),('bossvictory',[50,57,62,66,69,74,81],6.5),('rushwin',[62,69,74,78,81,86],6.5)]:
 buf=np.zeros(int(duration*R),np.float32)
 victory=name in ('bossvictory','rushwin')
 for i,pitch in enumerate(pitches):
  start=i*(.19 if victory else min(.13,duration/(len(pitches)+1)))
  note(buf,start,min(duration-start-.04,3.8 if victory else .55),pitch,.2,'brass' if victory or name in ('bosshit','bossintro') else 'bell')
 if victory:
  for offset in (0,.8,1.6):drum(buf,offset,True)
  for pitch in [38,50,57,62,66]:note(buf,1.6,4.6,pitch,.1,'choir')
  for pitch in [74,78,81]:note(buf,2.7,3.4,pitch,.13,'bell')
  echo=buf.copy();buf+=.2*np.roll(echo,int(.17*R))+.1*np.roll(echo,int(.33*R))
 elif name=='bosshit':drum(buf,0,True)
 # One-shot: fondu final, pas de repli de fin sur le début.
 fade=min(int(.5*R),len(buf)//3);buf[-fade:]*=np.linspace(1,0,fade)
 save(name,buf,.86 if victory else .55)
(ROOT.parent/'docs'/'AUDIO-CHECK.json').write_text(json.dumps(report,indent=2))

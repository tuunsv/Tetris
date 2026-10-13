"""Tạo WAV gốc cho Tetris 118. Chỉ cần Python + numpy (game không cần Python).
Giai điệu dân ca Korobeiniki; bè/âm sắc/SFX tự tổng hợp, không lấy bản ghi Nintendo.
Tham khảo cao độ/nhịp: https://www.flutetunes.com/tunes.php?id=192
"""
from pathlib import Path
import math, wave
import numpy as np

ROOT = Path(__file__).resolve().parents[1] / 'Assets'
ROOT.mkdir(exist_ok=True)
RATE = 44100
BPM = 132
BEAT = 60 / BPM
PITCH = {'C':0,'C#':1,'D':2,'D#':3,'E':4,'F':5,'F#':6,'G':7,'G#':8,'A':9,'A#':10,'B':11}

def freq(note):
    return 440 * 2 ** ((12 * (int(note[-1]) + 1) + PITCH[note[:-1]] - 69) / 12)

def tone(note, duration, style='lead'):
    t = np.arange(max(1, int(duration * RATE))) / RATE
    f = freq(note) if isinstance(note, str) else note
    if style == 'lead':
        # Họa âm giảm nhanh: còn màu chiptune, nhưng không có cạnh vuông chói tai.
        phase = 2 * np.pi * f * t + .004 * np.sin(2 * np.pi * 4.8 * t)
        sound = np.sin(phase) + .18*np.sin(3*phase)/3 + .05*np.sin(5*phase)/5
        env = np.minimum(1, t/.009) * np.minimum(1, (duration-t)/.055) * (.65+.35*np.exp(-t*5))
    elif style == 'bass':
        sound = np.sin(2*np.pi*f*t) + .19*np.sin(4*np.pi*f*t)
        env = np.minimum(1,t/.014) * np.minimum(1,(duration-t)/.055) * np.exp(-t*1.8)
    else:
        sound = np.sin(2*np.pi*f*t) + .18*np.sin(2*np.pi*f*2*t)
        env = np.minimum(1,t/.008) * np.minimum(1,(duration-t)/.045) * np.exp(-t*4)
    return sound * np.maximum(0, env)

def write(name, data):
    data = np.clip(data, -.95, .95)
    with wave.open(str(ROOT / (name + '.wav')), 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE)
        w.writeframes((data * 32767).astype('<i2').tobytes())

def phrase(text):
    return [(n.split(':')[0], float(n.split(':')[1])) for n in text.split()]

A = phrase('''E5:1 B4:.5 C5:.5 D5:1 C5:.5 B4:.5
A4:1 A4:.5 C5:.5 E5:1 D5:.5 C5:.5
B4:1.5 C5:.5 D5:1 E5:1
C5:1 A4:1 A4:1 R:1
R:.5 D5:1 F5:.5 A5:1 G5:.5 F5:.5
E5:1.5 C5:.5 E5:1 D5:.5 C5:.5
B4:1 B4:.5 C5:.5 D5:1 E5:1
C5:1 A4:1 A4:1 R:1''')
B = phrase('''E5:2 C5:2 D5:2 B4:2 C5:2 A4:2 B4:4
E5:2 C5:2 D5:2 B4:2 C5:1 E5:1 A5:2 G#5:4''')
assert sum(d for _, d in A) == 32
assert sum(d for _, d in B) == 32
melody = A + A + B + A
length = int(sum(d for _, d in melody)*BEAT*RATE)
lead = np.zeros(length)

def add(dst, data, start, volume=1):
    begin = int(start*RATE)
    end = min(len(dst),begin+len(data))
    if end>begin: dst[begin:end] += data[:end-begin]*volume

beat = 0
for note, dur in melody:
    if note != 'R': add(lead, tone(note, dur*BEAT*.93), beat*BEAT, .29)
    beat += dur
music = lead.copy()
# Delay vòng (wrap) để vòng lặp không bị cắt đuôi hoặc tạo cú click.
for delay, gain in ((.17,.13),(.34,.065)):
    music += np.roll(lead,int(delay*RATE))*gain
chords = {'Am':('A2','E3','A3','C4','E4'), 'E':('E2','B2','G#3','B3','D4'), 'Dm':('D2','A2','A3','D4','F4')}
progressA = ['Am','Am','E','Am','Dm','Am','E','Am']
progressB = ['Am','E','Am','E','Am','E','Am','E']
for bar, chord in enumerate(progressA+progressA+progressB+progressA):
    ns = chords[chord]
    for k in range(4):
        add(music,tone(ns[k%2],BEAT*.88,'bass'), (bar*4+k)*BEAT, .14)
    for k in range(8):
        add(music,tone(ns[2+k%3],BEAT*.43,'bell'), (bar*4+k*.5)*BEAT, .043)
# Đảm bảo mẫu đầu/cuối nhẹ; vùng cuối giai điệu có khoảng nghỉ.
music[:200] *= np.linspace(0,1,200)
music[-200:] *= np.linspace(1,0,200)
write('korobeiniki', music)

def sequence(name, notes, durations, gain=.35, style='bell'):
    size = int((sum(durations)+.14)*RATE)
    output = np.zeros(size); start=0
    for n,d in zip(notes,durations):
        add(output,tone(n,d+.07,style),start,gain);start+=d
    write(name,output)

sequence('move',['A4'],[.025],.14)
sequence('rotate',['E5','A5'],[.032,.048],.24)
sequence('soft',['E4'],[.025],.16,'bass')
sequence('lock',['C4','G3'],[.035,.07],.28,'bass')
sequence('hold',['A4','E5','C5'],[.04,.04,.07],.26)
sequence('menu',['E5','G5'],[.04,.075],.23)
sequence('clear',['C5','E5','A5'],[.075,.075,.14],.33)
sequence('spin',['E5','G5','B5','E6'],[.06,.06,.06,.18],.3)
sequence('level',['A4','C5','E5','A5'],[.09,.09,.09,.28],.28)
sequence('over',['E4','D4','C4','B3','A3'],[.14,.14,.14,.14,.5],.28)
sequence('win',['A4','C5','E5','A5','B5','C6'],[.1,.1,.1,.22,.1,.48],.3)
# Hard drop: thấp, gọn, có đuôi click như chốt cartridge.
t = np.arange(int(.2*RATE))/RATE
rng=np.random.default_rng(118)
impact=.55*np.sin(2*np.pi*(140*t-170*t*t))*np.exp(-t*26)
impact+=.12*rng.normal(size=len(t))*np.exp(-t*75)
impact[:70]*=np.linspace(0,1,70)
write('hard',impact)
# TETRIS! có hợp âm mở rộng + arpeggio, dài hơn clear thường.
celebrate=np.zeros(int(1.05*RATE))
for n in ['A3','E4','A4','C5','E5']:
    add(celebrate,tone(n,.8,'bell'),.0,.07)
for i,n in enumerate(['A4','C5','E5','A5','C6','E6']):
    add(celebrate,tone(n,.29,'lead'),i*.07,.24)
for i,n in enumerate(['A5','E6']):
    add(celebrate,tone(n,.34,'bell'),.62+i*.035,.12)
write('tetris',celebrate)
print('Generated',len(list(ROOT.glob('*.wav'))),'WAV files; music',round(length/RATE,2),'seconds')

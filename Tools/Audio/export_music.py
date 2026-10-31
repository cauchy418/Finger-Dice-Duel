from pathlib import Path
import json, hashlib, subprocess, numpy as np, soundfile as sf, imageio_ffmpeg
from scipy.signal import butter,sosfilt
folder=Path('Art/Production/music-v1'); source_dir=folder/'source'; source_dir.mkdir(exist_ok=True)
runtime=Path('Assets/Resources/Audio/Music');runtime.mkdir(parents=True,exist_ok=True)
analysis=json.loads((folder/'analysis.json').read_text(encoding='utf-8'))
prior=json.loads((folder/'manifest.json').read_text(encoding='utf-8')) if (folder/'manifest.json').exists() else {'tracks':[]}
prior={t['key']:t for t in prior['tracks']}
manifest=[]; sr=48000; rng=np.random.default_rng(418)
def drum(kind):
 duration=.48 if kind=='tom' else .30 if kind=='kick' else .18
 t=np.arange(int(sr*duration))/sr
 noise=rng.normal(0,1,len(t));noise=sosfilt(butter(2,[450,3200],fs=sr,btype='bandpass',output='sos'),noise)
 if kind=='snare': y=noise*np.exp(-t*26)*.23+np.sin(2*np.pi*155*t)*np.exp(-t*40)*.12
 else:
  phase=2*np.pi*((48 if kind=='kick' else 72)*t+(5 if kind=='kick' else 7)*(1-np.exp(-t*28))/28)
  y=np.sin(phase)*np.exp(-t*(15 if kind=='kick' else 10))*.7+noise*np.exp(-t*80)*.05
 return y.astype(np.float32)
samples={x:drum(x) for x in ['kick','tom','snare']}
def write(path,data):
 temp=Path('Temp/MusicExport');temp.mkdir(parents=True,exist_ok=True)
 wav=temp/(path.stem+'.wav');encoded=temp/path.name
 sf.write(wav,data,sr,subtype='FLOAT')
 subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(),'-y','-v','error','-i',str(wav),'-c:a','libvorbis','-q:a','6',str(encoded)],check=True)
 encoded.replace(path);wav.unlink()
 return dict(path=path.as_posix(),samples=len(data),seconds=len(data)/sr,peak=float(np.max(np.abs(data))),sha256=hashlib.sha256(path.read_bytes()).hexdigest())
for a in analysis:
 p=Path(a['source']);original_exists=p.exists()
 if not original_exists:p=source_dir/(a['key']+'.flac')
 source_hash=hashlib.sha256(p.read_bytes()).hexdigest() if original_exists else prior[a['key']]['sourceWavSha256']
 x,readsr=sf.read(p,dtype='float32',always_2d=True);assert readsr==sr
 lossless=source_dir/(a['key']+'.flac');sf.write(lossless,x,sr,subtype='PCM_16')
 start=round(a['loopStart']*sr);end=round(a['loopEnd']*sr);fade=round(.16*sr)
 # Crossfade into the PRECEDING audio at loop start. The next first sample
 # then continues naturally, without shortening a bar or adding a silent gap.
 intro=x[:end].copy();w=np.linspace(0,1,fade,dtype=np.float32)[:,None];w=w*w*(3-2*w)
 intro[-fade:]=intro[-fade:]*(1-w)+x[start-fade:start]*w
 intro[:720]*=np.linspace(0,1,720)[:,None]
 loop=intro[start:end].copy();peak=max(np.abs(intro).max(),np.abs(loop).max());gain=min(1,.84/max(peak,1e-8));intro*=gain;loop*=gain
 files=[write(runtime/(a['key']+'_intro.ogg'),intro),write(runtime/(a['key']+'_loop.ogg'),loop)]
 if a['key']!='dusk':
  # Keep the entire intro and loop on exactly the same sample clock.
  # Beat tracker times follow the recording's small tempo variations.
  beat_times=np.array(a['beatTimes']); full=np.zeros_like(intro)
  for i,when in enumerate(beat_times):
   at=round(when*sr)
   if at>=end:break
   phase=(i-a['downbeatPhase'])%4
   kind='kick' if phase==0 else 'tom' if phase==2 else 'snare';hit=samples[kind];n=min(len(hit),end-at)
   velocity=1 if phase==0 else .70 if phase==2 else .45
   pan=-.18 if phase==1 else .18 if phase==3 else 0
   full[at:at+n,0]+=hit[:n]*velocity*(1-pan);full[at:at+n,1]+=hit[:n]*velocity*(1+pan)
  full*=min(1,.68/max(np.abs(full).max(),1e-8))
  # Exactly copy the matching preceding tail at the join as for the bed.
  full[-fade:]=full[-fade:]*(1-w)+full[start-fade:start]*w
  files +=[write(runtime/(a['key']+'_drums_intro.ogg'),full),write(runtime/(a['key']+'_drums_loop.ogg'),full[start:end])]
 # Auditable join quality, measured relative to the surrounding music.
 seam=np.abs(loop[0]-loop[-1]); local=np.sqrt(np.mean(loop[:2400]**2))
 manifest.append(dict(title=a['title'],key=a['key'],sourceWavSha256=source_hash,losslessSource=str(lossless),losslessSha256=hashlib.sha256(lossless.read_bytes()).hexdigest(),sampleRate=sr,loopStartSample=start,loopEndSample=end,loopStart=start/sr,loopEnd=end/sr,beats=a['beats'],estimatedBpm=a['bpm'],crossfadeSeconds=.16,sourceGain=float(gain),seamMaxDelta=float(seam.max()),seamContextRms=float(local),files=files))
 print(a['key'],start/sr,end/sr,'seam',round(float(seam.max()),6),flush=True)
(folder/'manifest.json').write_text(json.dumps(dict(date='2026-11-01',source='user-provided stereo WAVs; FLAC preserves original PCM',drums='Additional synthesized low kick, tom and soft snare; not isolated original stems. Fixed export seed 418. Actual detected beat positions; no gameplay RNG.',tracks=manifest),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

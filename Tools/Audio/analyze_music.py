from pathlib import Path
import json, numpy as np, librosa, soundfile as sf
out=Path('Art/Production/music-v1'); results=[]
for name,key in [('River at Dusk','dusk'),('River at Battlefield','battlefield'),('Dawn of Steel','dawn')]:
 p=Path('C:/Users/29391/Downloads')/(name+'.wav'); audio=p if p.exists() else out/'source'/(key+'.flac'); y,sr=librosa.load(audio,sr=12000,mono=True)
 onset=librosa.onset.onset_strength(y=y,sr=sr,hop_length=240)
 tempo,beats=librosa.beat.beat_track(onset_envelope=onset,sr=sr,hop_length=240,trim=False)
 times=librosa.frames_to_time(beats,sr=sr,hop_length=240); bpm=float(np.asarray(tempo).ravel()[0])
 # Fit the steady section so a loop contains whole four-beat bars.
 dt=np.diff(times); dt=dt[(dt>.4)&(dt<1.2)]; period=float(np.median(dt)) if len(dt) else 60/bpm
 # Refine grid to actual beat positions instead of rounded tempo bins.
 if len(times)>20:
  ids=np.arange(len(times)); fit=np.polyfit(ids,times,1)
  if abs(fit[0]-period)<.04: period=float(fit[0])
 bpm=60/period
 chroma=librosa.feature.chroma_stft(y=y,sr=sr,n_fft=2048,hop_length=240)
 energy=librosa.feature.rms(y=y,frame_length=2048,hop_length=240)[0]
 features=np.vstack([chroma,np.log(energy[None,:]+1e-5)])
 features=(features-features.mean(axis=1,keepdims=True))/(features.std(axis=1,keepdims=True)+1e-4)
 candidates=[]
 strengths=onset[beats]
 phase=int(np.argmax([np.mean(strengths[np.arange(len(beats))%4==n]) for n in range(4)]))
 for i,start in enumerate(times):
  if i%4!=phase:continue
  if start<12 or start>90:continue
  for length in [96,128,160,192,256]:
   if i+length>=len(times):continue
   end=float(times[i+length])
   if end>len(y)/sr-12 or end-start<50:continue
   a=int(start/.02);b=int(end/.02);w=40
   if a<w or b+w>=features.shape[1]:continue
   score=float(np.mean((features[:,a-w:a+w]-features[:,b-w:b+w])**2))
   score+=.15*abs(np.log((np.mean(energy[a:a+100])+1e-6)/(np.mean(energy[b-100:b])+1e-6)))
   candidates.append((score,float(start),float(end),length))
 candidates.sort(); chosen=candidates[0]
 entry=dict(title=name,key=key,source=str(p),sampleRate=sf.info(audio).samplerate,duration=len(y)/sr,bpm=bpm,beatPeriod=period,beatOrigin=float(times[0]),downbeatPhase=phase,beatTimes=[float(t) for t in times],loopStart=chosen[1],loopEnd=chosen[2],beats=chosen[3],candidates=[dict(score=float(s),start=a,end=b,beats=n) for s,a,b,n in candidates[:8]])
 results.append(entry); print(key,round(bpm,2),round(chosen[1],3),round(chosen[2],3),chosen[3],flush=True)
(out/'analysis.json').write_text(json.dumps(results,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

from pathlib import Path
import json,hashlib
from PIL import Image,ImageDraw,ImageFont
from concurrent.futures import ThreadPoolExecutor
folder=Path(__file__).resolve().parent;root=folder.parents[2];raw=root/'Temp/AnimationP23Frames'
clips=json.loads((folder/'clips.json').read_text(encoding='utf-8'));profiles=json.loads((folder/'profiles.json').read_text(encoding='utf-8'))
def convert(clip):
 result=[];dest=folder/clip['id'];dest.mkdir(exist_ok=True)
 for name,progress in [('windup',.28),('peak',.62)]:
  source=raw/clip['id']/(name+'.png');target=dest/(name+'.webp')
  with Image.open(source) as im:
   im=im.convert('RGB');assert im.size==(1280,720),source;pixels=im.tobytes();im.save(target,format='WEBP',lossless=True,quality=100,method=5)
  with Image.open(target) as check:assert check.convert('RGB').tobytes()==pixels,target
  result.append(dict(path=f"{clip['id']}/{name}.webp",progress=progress,sha256=hashlib.sha256(target.read_bytes()).hexdigest()))
 entry=dict(clip);entry['status']='final';entry['durationSeconds']=.38 if clip['tier']==3 else .56;entry['frames']=result;return entry
with ThreadPoolExecutor(max_workers=4) as pool:records=list(pool.map(convert,clips))
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',16)
for page in range((len(clips)+11)//12):
 group=clips[page*12:(page+1)*12];sheet=Image.new('RGB',(1280,((len(group)+3)//4)*206),(15,18,22));draw=ImageDraw.Draw(sheet)
 for i,c in enumerate(group):
  with Image.open(folder/c['id']/'peak.webp') as im:thumb=im.convert('RGB').resize((320,180))
  x=(i%4)*320;y=(i//4)*206;sheet.paste(thumb,(x,y));draw.text((x+4,y+181),c['skill']+(' '+c['element'] if c['element']!='None' else ''),font=font,fill=(222,190,133))
 sheet.save(folder/f'review-{page+1}.jpg',quality=92)
sources=['Assets/Scripts/Core/SecondarySkillAnimation.cs','Assets/Scripts/UI/CombatAnimationPlayer.SecondarySkills.cs','Assets/Scripts/UI/CombatAnimationPlayer.FinalSkills.cs','Assets/Scripts/UI/CombatStatusMotion.Secondary.cs']
manifest=dict(date='2026-10-31',renderer='Unity native Canvas / production DrawFinalPose and DrawSecondaryPose',capture='controlled review fixtures; actual rule-trigger and asynchronous queue validation are separate',size=[1280,720],format='lossless WebP, pixel-identical to native PNG captures',sourceSha256={p:hashlib.sha256((root/p).read_bytes()).hexdigest() for p in sources},profiles=len(profiles),motionFamilies=len(set(p['style'] for p in profiles)),clips=records)
(folder/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
lines=['# P2／P3动画定稿与截图','',f'**{len(profiles)}个语义入口、{len(set(p["style"] for p in profiles))}类动作、{len(clips)}种定稿变体，{len(clips)*2}张1280×720关键帧。** 每项保存起势0.28与高峰0.62；这是表现变体数量，不是角色数量。','', '截图由Unity原生Canvas与生产播放器的同一个绘制方法输出。WebP采用无损编码，导出逐像素核对与Unity原PNG一致；未后期绘画。总览仅缩放拼接原图。受控姿态样例用于审美、落点和缺图检查，不能混称为一次真实技能操作测试。','', '## 总览','']
for page in range((len(clips)+11)//12):lines += [f'![P2／P3总览{page+1}](review-{page+1}.jpg)','']
lines += ['## 每项最终稿','', '| 片段 | 技能／主要变体 | 起势 | 高峰 |','|---|---|---|---|']
for c in clips:
 label=c['skill']+(' · '+c['element'] if c['element']!='None' else '')+(' · '+c['variant'] if c['variant'] else '')
 lines.append(f"| {c['id']} | {label} | [截图]({c['id']}/windup.webp) | [截图]({c['id']}/peak.webp) |")
lines += ['','## 验证与复现','', '- [功能回归](functional-tests.txt)：1569项通过，新增13项针对性测试；独立胜率统计8项跳过，本轮未改玩法与AI策略。Unity UI与NUnit路径编译通过。','- [全变体及多人队列](live-playback/p23-queue-validation.txt)：1v1全变体、2v2、8人、跳过与中止，检查输入、数字和位姿清理。','- [真实技能场景](live-playback/runtime-validation.txt)：多段、格挡、闪避、取消、代挡、转移、末击胜负与多人起手。','- [持续资源验证](live-playback/resource-validation.txt)：气增减、0气、逸闻九数、醉拳及清理。','- [原生姿态审阅](native-review.txt)、[骰面复查](native-review-dice.txt)与[文案／到期复查](native-review-details.txt)：资源加载、可见效果、无旧数字残留、清理。','', '顺序执行[capture-native.csx](capture-native.csx)、[refresh-dice.csx](refresh-dice.csx)、[refresh-details.csx](refresh-details.csx)、[live-playback/validate-queue.csx](live-playback/validate-queue.csx)、[validate-runtime.csx](live-playback/validate-runtime.csx)、[validate-resources.csx](live-playback/validate-resources.csx)。通过项目现有Codely execute_csharp_script，以play模式运行；先编译脚本并确认场景有GameBootstrap。前两者把原PNG写入项目Temp/AnimationP23Frames；随后运行[build-index.py](build-index.py)无损归档并生成总览、索引与SHA256。结束恢复AI思考间隔、后台设置并返回选角，不保存场景或改写玩家存档。','', '[manifest.json](manifest.json)记录绘制源、技能数据、关键进度及文件SHA256；[六张新手绘素材与完整提示词](../../Production/combat-animation-p2-p3/manifest.json)由内置image_gen生成。P1临时数字清理修正的重拍脚本为[refresh-p1-numbers.csx](live-playback/refresh-p1-numbers.csx)。','', '范围与剩余P1项目见[进度说明](../../../Docs/animation-p2-p3-progress.md)。以后每项动画最终稿继续保存关键帧，修改后重新截图。','']
(folder/'README.md').write_text('\n'.join(lines),encoding='utf-8')
print(f'Archived {len(records)} variants / {len(records)*2} pixel-identical lossless frames.')

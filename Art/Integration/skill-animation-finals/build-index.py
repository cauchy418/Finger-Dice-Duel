from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
from concurrent.futures import ThreadPoolExecutor
import hashlib,json
folder=Path(__file__).resolve().parent
clips=json.loads((folder/'clips.json').read_text(encoding='utf-8'))
def compress(path):
 with Image.open(path) as im:
  im.load();assert im.size==(1280,720),path
  im.save(path,optimize=True)
with ThreadPoolExecutor(max_workers=4) as executor:list(executor.map(compress,folder.rglob('*.png')))
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',15)
for page in range((len(clips)+11)//12):
 group=clips[page*12:(page+1)*12];height=((len(group)+3)//4)*205
 sheet=Image.new('RGB',(1280,height),(15,18,22));draw=ImageDraw.Draw(sheet)
 for i,clip in enumerate(group):
  with Image.open(folder/clip['id']/'peak.png') as im:thumb=im.resize((320,180))
  x=(i%4)*320;y=(i//4)*205;sheet.paste(thumb,(x,y));draw.text((x+5,y+181),clip['id'],font=font,fill=(222,190,133))
 sheet.save(folder/f'review-{page+1}.jpg',quality=92)
root=folder.parents[2]
sources=['Assets/Scripts/UI/CombatAnimationPlayer.FinalSkills.cs','Assets/Scripts/Core/FinalSkillAnimation.cs']
manifest=dict(date='2026-10-31',renderer='Unity native Canvas / production DrawFinalPose',capture='controlled review fixtures; not a claim that every frame is a real rule-triggered skill',size=[1280,720],durationSeconds=.72,sourceSha256={p:hashlib.sha256((root/p).read_bytes()).hexdigest() for p in sources},clips=[])
for c in clips:
 entry=dict(c);entry['status']='final';entry['frames']=[]
 for name,progress in [('windup',.28),('peak',.62)]:
  path=folder/c['id']/(name+'.png');entry['frames'].append(dict(path=f"{c['id']}/{name}.png",progress=progress,sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
 manifest['clips'].append(entry)
(folder/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
lines=['# 专属动画定稿与截图','', '2026-10-31。**53种播放变体，106张1280×720 PNG**。每种包含起势（进度0.28）与高峰（进度0.62）；它们是播放变体，不是53名角色。','', '截图来自Unity原生Canvas与生产播放器的同一个绘制方法；使用受控角色及技能数据审阅外观、落点、可读性，未把静态姿态预览当成真实规则触发。实际规则数据另由功能测试验证，异步队列由现场播放脚本验证。截图无后期绘画；以下总览仅缩放拼接原图。','', '## 总览','']
for i in range(1,6):lines+= [f'![专属动画总览{i}](review-{i}.jpg)','']
lines+=['## 每项最终稿','', '| 片段 | 技能／变体 | 起势 | 高峰 |','|---|---|---|---|']
for c in clips:
 label=c['skill']+((' · '+c['variant']) if c['variant'] else '')
 lines.append(f"| {c['id']} | {label} | [截图]({c['id']}/windup.png) | [截图]({c['id']}/peak.png) |")
lines+=['','## 验证与复现','', '- [功能回归](functional-tests.txt)：1556项通过，8项独立胜率统计跳过；动画不改变AI或玩法，未重采样胜率。Unity UI程序集与NUnit路径编译通过。','- [实际现场播放](live-playback/runtime-validation.txt)：12种场景、56项检查，覆盖末击胜负、多段格挡、闪避／取消、代挡／转移、多人起手、跳过与输入恢复。','- [专属队列检查](queue-validation.txt)：53种变体顺序实际播放，另检查2v2和8人门户队列；临时数字清空、面板位置复原、输入解锁、路径锚点固定。','- [素材与清理检查](native-review.txt)：53个定稿样例的资源均加载，预览结束清理成功。','', '在Unity播放模式中，通过项目现有Codely `execute_csharp_script`执行[capture-native.csx](capture-native.csx)重拍；先完成脚本编译，并确保当前场景存在GameBootstrap。运行结束返回选角页，恢复AI思考间隔与后台运行设置。现场播放使用[live-playback/validate-runtime.csx](live-playback/validate-runtime.csx)，专属队列使用[validate-queue.csx](validate-queue.csx)，三个脚本须依次运行。','', '[manifest.json](manifest.json)登记每个片段、截图进度、SHA256和绘制源版本；[build-index.py](build-index.py)只负责无损压缩、缩略总览和索引。以后每个动画最终稿必须留截图，修改后重新截图并更新索引。','', '羊血池击破／到期恢复、幽灵消亡、猎人印记消耗、巨龙二次运算和圣杯额外回复沿用快照与状态框架，未作为本批独立最终稿登记。持续状态的后续专属循环仍见[完整制作清单](../../../Docs/skill-and-status-animation-list.md)。','']
(folder/'README.md').write_text('\n'.join(lines),encoding='utf-8')
print(f'Indexed {len(clips)} clips / {len(clips)*2} final screenshots.')

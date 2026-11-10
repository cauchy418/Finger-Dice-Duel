using FingerDiceDuel.Core;
var alternatives=new System.Collections.Generic.Dictionary<string,string>();
System.Action<string,string[]> group=(reason,names)=>{foreach(var name in names)alternatives[name]=reason;};
group("已有VowTransfer/Attack转移/闪避事件，不重复播放Skill",new[]{"仇敌誓言","守护之誓","憎恨灵光","妖精步伐·取消攻击","梦幻舞步"});
group("已有真实Attack事件包含类型、来源、落点、逐段结果及手值快照",new[]{"恐惧打击","惧怖收割","次级咒术","潇洒无畏","灭族者"});
group("CleanseAndHeal已记录维持生命及实际受疗者/回复量",new[]{"双九回复"});
group("Attack/状态/护盾/附魔快照及常驻图标，日志不重复声明成功",new[]{"伤害免疫","伤害易伤","施加状态","附加抵挡","耳鸣","油腻","附魔"});
group("剧情教学/敌人/整备提示，实际手值、生命、盾与状态由面板快照显示",new[]{"刻印","回廊重赋","折光外壳","推开路障","操演护持","整装迎战","架盾","架盾试验","注水铭刻","演练调律","稳弦","符文换装","训练整备"});
var events=new System.Collections.Generic.SortedDictionary<string,System.Collections.Generic.HashSet<string>>();
foreach(var file in System.IO.Directory.GetFiles("Assets/Scripts","*.cs",System.IO.SearchOption.AllDirectories)){
 if(file.Replace('\\','/').Contains("/UI/"))continue;
 foreach(System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(System.IO.File.ReadAllText(file),@"(?:NoteSkill|RecordSkillVisual)\([^\n]*?,\s*""([^""]+)""")){
  string name=match.Groups[1].Value;if(!events.ContainsKey(name))events[name]=new System.Collections.Generic.HashSet<string>();events[name].Add(file.Replace('\\','/'));
 }
}
System.Func<string,string> quote=value=>value==null?"null":"\""+value.Replace("\\","\\\\").Replace("\"","\\\"")+"\"";
var result=new System.Text.StringBuilder("{\n  \"sourceVersion\":\"87d54e5\",\n  \"events\":[\n");int index=0,keyed=0,alternate=0;
foreach(var pair in events){
 string key=SkillVisualProfiles.Key(pair.Key),reason=null;
 if(key==null&&!alternatives.TryGetValue(pair.Key,out reason))throw new System.Exception("Unclassified emitted skill: "+pair.Key);
 if(key!=null)keyed++;else alternate++;
 if(index++>0)result.Append(",\n");result.Append("    {\"skill\":").Append(quote(pair.Key)).Append(",\"resourceKey\":").Append(quote(key)).Append(",\"alternative\":").Append(quote(reason)).Append(",\"sources\":[");int source=0;
 foreach(var file in pair.Value){if(source++>0)result.Append(',');result.Append(quote(file));}result.Append("]}");
}
result.Append("\n  ]\n}\n");System.IO.File.WriteAllText("Art/Integration/skill-animation-rework/literal-event-audit.json",result.ToString(),new System.Text.UTF8Encoding(true));
return "Actual SkillVisualProfiles.Key audit: "+events.Count+" literal names; "+keyed+" registered, "+alternate+" explicit existing presentations; zero unclassified.";

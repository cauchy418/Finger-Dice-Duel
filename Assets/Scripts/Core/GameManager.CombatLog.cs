using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    public partial class GameManager
    {
        private readonly List<string> _skillLog = new List<string>();

        /// <summary>只在技能实际生效后调用；有界缓存也适用于不读取战报的模拟对局。</summary>
        public void NoteSkill(PlayerState source, string skill, string detail,PlayerState visualTarget=null,SkillVisualData visualData=null)
        {
            if(skill!="元素反应"&&!(detail??"").Contains("保留原加数"))RecordSkillVisual(source,skill,visualTarget,visualData);
            if (_skillLog.Count >= 128) _skillLog.RemoveAt(0);
            _skillLog.Add(CombatText.Skill(source, skill, detail));
        }

        public List<string> TakeSkillLog()
        {
            var copy = new List<string>(_skillLog);
            _skillLog.Clear();
            return copy;
        }
    }
}

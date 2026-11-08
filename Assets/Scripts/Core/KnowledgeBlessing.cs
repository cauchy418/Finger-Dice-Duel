namespace FingerDiceDuel.Core
{
    public partial class PlayerState
    {
        public int KnowledgeStacks {get;private set;}
        public void GainKnowledge(PlayerState source=null)
        {
            if(!IsAlive||HasStatus(StatusEffect.HolyLight))return;
            KnowledgeStacks++;ApplyStatus(StatusEffect.KnowledgeBlessing,int.MaxValue,source);
        }
        public int ConsumeKnowledge()
        {int layers=KnowledgeStacks;RemoveStatus(StatusEffect.KnowledgeBlessing);return layers;}
    }
}

using System;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>仅教学建局注入；界面与控制器共用同一约束，禁用按钮不能被其它入口绕开。</summary>
    public interface ITutorialInput
    {
        bool AllowsHand(Hand hand);
        bool AllowsOption(int value);
        bool AllowsDamageType(DamageType type);
        bool AllowsResistance(AttackDamageType type);
        bool AllowsAccept {get;}
        bool AllowsDecline {get;}
        bool AllowsSkip {get;}
        bool AllowsCastling {get;}
    }
    public sealed partial class BoardController
    {
        public ITutorialInput TutorialInput {get;set;}
    }
}

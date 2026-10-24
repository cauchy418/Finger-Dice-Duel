using System;

namespace FingerDiceDuel.Core
{
    public partial class GameManager
    {
        /// <summary>束缚和油腻使用的随机源；可注入以复验边界，预览不判定油腻。</summary>
        public Random StatusRandom { get; set; } = new Random();
        private int _statusActionSerial;

        internal void PrepareRestraint(PlayerState actor)
        {
            if (actor == null) return;
            actor.PrepareRestraint(_statusActionSerial, StatusRandom);
        }

        private void ResolveGreaseAfterOperation(PlayerState actor)
        {
            if (!actor.HasStatus(StatusEffect.Greased) || StatusRandom.Next(2) != 0) return;
            actor.ApplyStatus(StatusEffect.KnockedDown, PlayerState.StatusDurationTurns, actor.StatusSourceOf(StatusEffect.Greased));
            actor.DeferOilProneTick();
            NoteSkill(actor, "油腻", "运算后滑倒：倒伏2回合");
        }
    }

    public partial class PlayerState
    {
        private int _restraintSerial = -1;
        private bool _deferOilProneTick;
        internal void PrepareRestraint(int serial, Random rng)
        {
            if (!HasStatus(StatusEffect.Restrained)) { ClearRestraintHands(); return; }
            if (_restraintSerial == serial && (LeftHand.IsRestrained || RightHand.IsRestrained)) return;
            ClearRestraintHands();
            GetHand(rng.Next(2)).IsRestrained = true;
            _restraintSerial = serial;
        }
        private void ClearRestraintHands()
        {
            LeftHand.IsRestrained = RightHand.IsRestrained = false;
            _restraintSerial = -1;
        }
        internal void DeferOilProneTick() { _deferOilProneTick = true; }
        private bool SkipFreshOilProneTick(StatusEffect status)
        {
            if (status != StatusEffect.KnockedDown || !_deferOilProneTick) return false;
            _deferOilProneTick = false; return true;
        }
    }
}

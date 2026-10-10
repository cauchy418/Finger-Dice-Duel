using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>
    /// 脚本化出招选择器：按预设序列出招，队列耗尽后回退为"第一步合法运算"。
    /// 与 <see cref="ScriptedChoiceProvider"/> 配合，可精确复现一整局的每一步。
    /// </summary>
    public sealed class ScriptedMoveSelector : IMoveSelector
    {
        private readonly Queue<Func<GameManager, PlayerState, LegalMove>> _script
            = new Queue<Func<GameManager, PlayerState, LegalMove>>();

        public string Describe => "脚本出招";

        /// <summary>加入一步："用自己第 actedIndex 只手，打对方第 targetIndex 只手"。</summary>
        public ScriptedMoveSelector Add(int actedIndex, int targetIndex)
        {
            _script.Enqueue((game, actor) =>
            {
                PlayerState opponent = game.GetOpponent(actor);
                return new LegalMove(actor, actor.GetHand(actedIndex), opponent.GetHand(targetIndex));
            });
            return this;
        }

        /// <summary>加入一步自定义出招。</summary>
        public ScriptedMoveSelector Add(Func<GameManager, PlayerState, LegalMove> step)
        {
            if (step == null) throw new ArgumentNullException(nameof(step));
            _script.Enqueue(step);
            return this;
        }

        public LegalMove ChooseMove(GameManager game, PlayerState actor)
        {
            if (_script.Count > 0) return _script.Dequeue()(game, actor);

            IReadOnlyList<LegalMove> moves = game.GetLegalMoves(actor);
            return moves.Count > 0 ? moves[0] : default;
        }
    }
}

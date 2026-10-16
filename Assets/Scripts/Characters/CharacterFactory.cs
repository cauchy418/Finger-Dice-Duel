using System;
using System.Collections.Generic;
using FingerDiceDuel.Characters.SpecialLogics;

namespace FingerDiceDuel.Core
{
    /// <summary>
    /// 角色工厂：把 <see cref="SpecialLogicType"/> 映射为具体的 <see cref="ICharacterBehavior"/> 实例。
    ///
    /// 新增角色只需两步：
    ///   1. 在 <see cref="SpecialLogicType"/> 登记新的枚举值；
    ///   2. 在 <see cref="BuiltIn"/> 中注册 枚举值 -> 构造函数 的映射（或在启动时调用 Register）。
    /// </summary>
    public static class CharacterFactory
    {
        private static readonly Dictionary<SpecialLogicType, Func<ICharacterBehavior>> _registry
            = new Dictionary<SpecialLogicType, Func<ICharacterBehavior>>();

        static CharacterFactory()
        {
            RegisterDefaults();
        }

        /// <summary>注册（或覆盖）某个特殊逻辑的实现。</summary>
        public static void Register(SpecialLogicType logic, Func<ICharacterBehavior> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _registry[logic] = factory;
        }

        /// <summary>是否已登记该逻辑。</summary>
        public static bool IsRegistered(SpecialLogicType logic) => _registry.ContainsKey(logic);

        /// <summary>内置角色逻辑映射。</summary>
        public static void RegisterDefaults()
        {
            _registry[SpecialLogicType.None] = () => new DataDrivenCharacter();
            _registry[SpecialLogicType.Warrior] = () => new WarriorLogic();
            _registry[SpecialLogicType.Curseweaver] = () => new CurseweaverLogic();
            _registry[SpecialLogicType.Barbarian] = () => new BarbarianLogic();
            _registry[SpecialLogicType.Priest] = () => new PriestLogic();
            _registry[SpecialLogicType.Ranger] = () => new RangerLogic();
            _registry[SpecialLogicType.Werewolf] = () => new WerewolfLogic();
            _registry[SpecialLogicType.Swordsman] = () => new SwordsmanLogic();
            _registry[SpecialLogicType.Ninja] = () => new NinjaLogic();
            _registry[SpecialLogicType.Assassin] = () => new AssassinLogic();
            _registry[SpecialLogicType.Paladin] = () => new PaladinLogic();
            _registry[SpecialLogicType.Warlock] = () => new WarlockLogic();
            _registry[SpecialLogicType.ArcaneArcher] = () => new ArcaneArcherLogic();

            // 白板已从角色选择里移除，但这个映射要保留 ——
            // 未登记/拼错的逻辑类型会退化成它（见 CreateBehavior 的兜底注释）。
            _registry[SpecialLogicType.Soldier] = () => new SoldierLogic();
            _registry[SpecialLogicType.Knight] = () => new KnightLogic();
            _registry[SpecialLogicType.Gambler] = () => new GamblerLogic();
            _registry[SpecialLogicType.Monk] = () => new MonkLogic();
            _registry[SpecialLogicType.Giant] = () => new GiantLogic();
            _registry[SpecialLogicType.Thief] = () => new ThiefLogic();
            _registry[SpecialLogicType.Hunter] = () => new HunterLogic();
            _registry[SpecialLogicType.Magician] = () => new MagicianLogic();
            _registry[SpecialLogicType.Mage] = () => new MageLogic();
        }

        /// <summary>
        /// 按特殊逻辑类型创建行为实例。
        /// 未登记的类型回退为白板（<see cref="DataDrivenCharacter"/>），不抛异常，
        /// 以免一个配置笔误导致整局无法开始。
        /// </summary>
        public static ICharacterBehavior CreateBehavior(SpecialLogicType logic)
        {
            if (_registry.TryGetValue(logic, out Func<ICharacterBehavior> factory))
                return factory();

            return new DataDrivenCharacter(logic.ToString());
        }

        /// <summary>按角色定义创建行为实例（含数值注入）。</summary>
        public static ICharacterBehavior CreateBehavior(CharacterDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            ICharacterBehavior behavior = CreateBehavior(definition.Logic);
            if (behavior is CharacterTemplate template)
                template.ApplyDefinition(definition);

            return behavior;
        }

        /// <summary>
        /// 创建已绑定角色与最大生命的玩家。
        ///
        /// 最大生命 = 角色数据的 MaxHealth + 角色自身的 <see cref="CharacterTemplate.MaxHealthBonus"/>
        /// （狼人 +5 => 25）。
        /// </summary>
        public static PlayerState CreatePlayer(string name, CharacterDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            var behavior = CreateBehavior(definition);
            int maxHealth = definition.MaxHealth + HealthBonusOf(behavior);

            var player = new PlayerState(name, behavior, maxHealth);
            player.BindDefinition(definition);
            return player;
        }

        /// <summary>
        /// 该玩家打出的攻击会分成几段（忍者 = 2，其余 = 1）。
        ///
        /// 上层需要提前知道段数，才能为每一段各问一次闪避。
        /// </summary>
        public static int MaxAttackHits(PlayerState player)
            => (player?.Character as IMultiHitAttack)?.MaxHits ?? 1;

        /// <summary>取角色自身的生命上限加成（未重写则为 0）。</summary>
        public static int HealthBonusOf(ICharacterBehavior behavior)
            => (behavior as CharacterTemplate)?.MaxHealthBonus ?? 0;

        /// <summary>
        /// 某个角色定义的**实际**生命上限（含角色自带的加成，例如狼人 +5）。
        ///
        /// 供角色选择界面显示身材。注意加成不在数据层（<see cref="CharacterDefinition.MaxHealth"/>），
        /// 而是由角色逻辑提供，所以必须实例化一次才能拿到。
        /// </summary>
        public static int MaxHealthOf(CharacterDefinition definition)
        {
            if (definition == null) return PlayerState.DefaultMaxHealth;

            return definition.MaxHealth + HealthBonusOf(CreateBehavior(definition));
        }

        /// <summary>用角色定义配置一个已存在的玩家（换角色场景）。</summary>
        public static void Configure(PlayerState player, CharacterDefinition definition)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            var behavior = CreateBehavior(definition);
            player.BindCharacter(behavior);
            player.BindDefinition(definition);
            player.SetMaxHealth(definition.MaxHealth + HealthBonusOf(behavior));
        }

        /// <summary>
        /// 组装一局标准对战：两名玩家的名字取角色显示名。
        /// </summary>
        public static GameManager CreateMatch(CharacterDefinition a, CharacterDefinition b,
                                              IChoiceProvider providerA = null,
                                              IChoiceProvider providerB = null)
        {
            var playerA = CreatePlayer(a.DisplayName, a);
            var playerB = CreatePlayer(b.DisplayName, b);
            return new GameManager(playerA, playerB, providerA, providerB);
        }
    }
}

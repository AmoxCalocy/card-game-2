using System.Collections.Generic;

namespace OneJourney.Core
{
    /// <summary>卡牌效果类型（对应《MVP 配置表》40 张卡的效果分解）。</summary>
    public enum CardEffectType
    {
        Damage,             // P0=amount
        GainArmor,          // P0=amount
        Heal,               // P0=amount
        Draw,               // P0=count
        ApplyBleed,         // P0=stacks
        ApplyDisease,       // P0=stacks
        ApplyFatigue,       // P0=stacks
        AddMorale,          // P0=stacks
        RemoveBleed,        // P0=stacks（0=全部）
        RemoveDisease,      // P0=stacks
        RemoveFatigue,      // P0=stacks
        RemoveArmor,        // P0=amount（从敌人目标移除护甲）
        ReduceIntent,       // P0=amount（降低敌人意图效果）
        SelfArmor,          // P0=amount（自身护甲，与目标类型无关）
        PartnerArmor,       // P0=amount（存活上阵伙伴各获得护甲，不含主角）
        BonusDrawNextTurn,  // P0=count
        CostReduction,      // P0=amount
        Exhaust,            // 结算后消耗
        SupplyFood,         // P0=amount（战斗胜利时额外获得粮食）
        FocusFire,          // P0=extraDamage（目标本回合每次受普通伤害额外+N）
        Taunt,              // P0=intentReduction（目标意图改为主角，效果-N）
        RemoveCapture,      // P0=stacks（0=全部）
        RemoveInjury,       // 移除受伤
        PartnerDamage,      // P0=bonusDamage（选一名伙伴，造成指令伤害+bonus）
        AllPartnerDamage,   // P0=bonusDamage（所有伙伴造成指令伤害+bonus）
        DrawThenDiscard,    // P0=drawCount, P1=discardCount（抽后弃，自动弃末尾）
        DiscardThenDraw,    // P0=discardCount, P1=drawCount（弃后抽，自动弃首部）
        ExhaustThenDraw,    // P0=drawCount（消耗手牌最后一张，再抽）
        Choice,             // P0=choiceId（0=护甲5，1=抽1...）
    }

    /// <summary>效果触发条件。</summary>
    public enum EffectCondition
    {
        None,
        TargetBleedGE2,     // 目标流血≥2（C06 处决）
        SelfArmorGE10,      // 自身护甲≥10（C13 守势）
    }

    /// <summary>一条卡牌效果。</summary>
    public struct CardEffect
    {
        public CardEffectType Type;
        public int P0, P1;
        public EffectCondition Condition;

        public CardEffect(CardEffectType type, int p0 = 0, int p1 = 0, EffectCondition condition = EffectCondition.None)
        {
            Type = type;
            P0 = p0;
            P1 = p1;
            Condition = condition;
        }
    }

    /// <summary>卡牌数据定义（运行时只读，MVP 阶段由 CardCatalog 静态提供）。</summary>
    public class CardDef
    {
        public string Id;
        public string DisplayName;
        public int Cost;
        public TargetType TargetType;
        public CardRarity Rarity;
        public string SourceText;
        public string EffectText;
        public string Description;
        public List<CardEffect> Effects = new List<CardEffect>();
    }
}

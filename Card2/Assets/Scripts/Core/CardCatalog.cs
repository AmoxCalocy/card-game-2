using System.Collections.Generic;

namespace OneJourney.Core
{
    /// <summary>
    /// 40 张基础卡牌静态目录（实施计划 A1-13）。
    /// 按《MVP 配置表》§3 定义，每张卡包含费用、目标类型、稀有度和效果列表。
    /// </summary>
    public static class CardCatalog
    {
        private static Dictionary<string, CardDef> _byId;
        private static List<CardDef> _all;

        public static IReadOnlyList<CardDef> All
        {
            get
            {
                EnsureBuilt();
                return _all;
            }
        }

        public static CardDef Find(string id)
        {
            EnsureBuilt();
            _byId.TryGetValue(id, out var def);
            return def;
        }

        public static bool Exists(string id)
        {
            EnsureBuilt();
            return _byId.ContainsKey(id);
        }

        private static void EnsureBuilt()
        {
            if (_all != null) return;
            _all = new List<CardDef>(40);
            _byId = new Dictionary<string, CardDef>(40);
            BuildAll();
        }

        private static void Add(CardDef def)
        {
            _all.Add(def);
            _byId[def.Id] = def;
        }

        private static CardDef Card(string id, string name, int cost, TargetType target, CardRarity rarity,
            string source, string effectText, string desc)
        {
            return new CardDef
            {
                Id = id, DisplayName = name, Cost = cost, TargetType = target,
                Rarity = rarity, SourceText = source, EffectText = effectText, Description = desc
            };
        }

        private static void BuildAll()
        {
            // ===== 3.1 攻击卡 (C01–C08) =====

            var c01 = Card("C01", "剑击", 1, TargetType.SingleEnemy, CardRarity.Common,
                "初始", "造成 6 普通伤害", "基础攻击牌");
            c01.Effects.Add(new CardEffect(CardEffectType.Damage, 6));
            Add(c01);

            var c02 = Card("C02", "重斩", 2, TargetType.SingleEnemy, CardRarity.Common,
                "草原奖励", "造成 12 普通伤害", "高费高伤攻击");
            c02.Effects.Add(new CardEffect(CardEffectType.Damage, 12));
            Add(c02);

            var c03 = Card("C03", "连刺", 1, TargetType.SingleEnemy, CardRarity.Common,
                "草原奖励", "造成 3 点伤害两次", "多段攻击");
            c03.Effects.Add(new CardEffect(CardEffectType.Damage, 3));
            c03.Effects.Add(new CardEffect(CardEffectType.Damage, 3));
            Add(c03);

            var c04 = Card("C04", "破甲斩", 1, TargetType.SingleEnemy, CardRarity.Rare,
                "草原奖励、铁匠铺", "造成 5 伤害并移除目标 5 护甲", "破甲攻击");
            c04.Effects.Add(new CardEffect(CardEffectType.RemoveArmor, 5));
            c04.Effects.Add(new CardEffect(CardEffectType.Damage, 5));
            Add(c04);

            var c05 = Card("C05", "横扫", 2, TargetType.AllEnemies, CardRarity.Rare,
                "草原精英、密林奖励", "每名敌人受到 5 伤害", "AOE 攻击");
            c05.Effects.Add(new CardEffect(CardEffectType.Damage, 5));
            Add(c05);

            var c06 = Card("C06", "处决", 2, TargetType.SingleEnemy, CardRarity.Epic,
                "密林奖励", "造成 8 伤害；目标流血至少 2 层时额外造成 6 伤害", "条件爆发");
            c06.Effects.Add(new CardEffect(CardEffectType.Damage, 8));
            c06.Effects.Add(new CardEffect(CardEffectType.Damage, 6, 0, EffectCondition.TargetBleedGE2));
            Add(c06);

            var c07 = Card("C07", "突袭", 0, TargetType.SingleEnemy, CardRarity.Common,
                "草原奖励", "造成 3 伤害，结算后消耗", "零费消耗攻击");
            c07.Effects.Add(new CardEffect(CardEffectType.Damage, 3));
            c07.Effects.Add(new CardEffect(CardEffectType.Exhaust));
            Add(c07);

            var c08 = Card("C08", "协同斩", 1, TargetType.SingleEnemy, CardRarity.Rare,
                "伙伴加入、密林奖励", "选一名伙伴，其对目标造成指令伤害+2", "伙伴协同攻击");
            c08.Effects.Add(new CardEffect(CardEffectType.PartnerDamage, 2));
            Add(c08);

            // ===== 3.2 防御卡 (C09–C16) =====

            var c09 = Card("C09", "格挡", 1, TargetType.Self, CardRarity.Common,
                "初始", "获得 5 护甲", "基础防御");
            c09.Effects.Add(new CardEffect(CardEffectType.GainArmor, 5));
            Add(c09);

            var c10 = Card("C10", "固守", 1, TargetType.Self, CardRarity.Common,
                "草原奖励", "获得 8 护甲", "中等防御");
            c10.Effects.Add(new CardEffect(CardEffectType.GainArmor, 8));
            Add(c10);

            var c11 = Card("C11", "盾击", 1, TargetType.SingleEnemy, CardRarity.Rare,
                "草原奖励、铁匠铺", "自身获得 3 护甲，对目标造成 4 伤害", "攻防一体");
            c11.Effects.Add(new CardEffect(CardEffectType.SelfArmor, 3));
            c11.Effects.Add(new CardEffect(CardEffectType.Damage, 4));
            Add(c11);

            var c12 = Card("C12", "守护", 1, TargetType.SingleAlly, CardRarity.Common,
                "伙伴加入", "目标获得 7 护甲", "友方护甲");
            c12.Effects.Add(new CardEffect(CardEffectType.GainArmor, 7));
            Add(c12);

            var c13 = Card("C13", "守势", 1, TargetType.Self, CardRarity.Common,
                "草原奖励", "获得 5 护甲；若自身护甲已不低于 10，再抽 1 张", "条件抽牌");
            c13.Effects.Add(new CardEffect(CardEffectType.GainArmor, 5));
            c13.Effects.Add(new CardEffect(CardEffectType.Draw, 1, 0, EffectCondition.SelfArmorGE10));
            Add(c13);

            var c14 = Card("C14", "屏障", 2, TargetType.AllAllies, CardRarity.Rare,
                "密林奖励", "每名存活上阵单位获得 5 护甲", "群体护甲");
            c14.Effects.Add(new CardEffect(CardEffectType.GainArmor, 5));
            Add(c14);

            var c15 = Card("C15", "预备", 1, TargetType.Self, CardRarity.Rare,
                "草原精英、密林奖励", "获得 4 护甲；下个玩家回合额外抽 1 张", "延时收益");
            c15.Effects.Add(new CardEffect(CardEffectType.GainArmor, 4));
            c15.Effects.Add(new CardEffect(CardEffectType.BonusDrawNextTurn, 1));
            Add(c15);

            var c16 = Card("C16", "不屈", 2, TargetType.Self, CardRarity.Epic,
                "密林精英", "获得 12 护甲并移除自身全部流血", "大防御+净化");
            c16.Effects.Add(new CardEffect(CardEffectType.GainArmor, 12));
            c16.Effects.Add(new CardEffect(CardEffectType.RemoveBleed, 0));
            Add(c16);

            // ===== 3.3 策略卡 (C17–C24) =====

            var c17 = Card("C17", "侦察", 0, TargetType.None, CardRarity.Common,
                "初始", "抽 1 张", "零费过牌");
            c17.Effects.Add(new CardEffect(CardEffectType.Draw, 1));
            Add(c17);

            var c18 = Card("C18", "计划", 1, TargetType.None, CardRarity.Common,
                "草原奖励", "抽 2 张，再选择弃 1 张", "滤牌");
            c18.Effects.Add(new CardEffect(CardEffectType.DrawThenDiscard, 2, 1));
            Add(c18);

            var c19 = Card("C19", "节能", 0, TargetType.None, CardRarity.Rare,
                "草原事件、奖励", "本回合下张牌费用 -1，结算后消耗", "减费");
            c19.Effects.Add(new CardEffect(CardEffectType.CostReduction, 1));
            c19.Effects.Add(new CardEffect(CardEffectType.Exhaust));
            Add(c19);

            var c20 = Card("C20", "整理", 1, TargetType.None, CardRarity.Common,
                "草原奖励", "选择弃 1 张，再抽 2 张", "置换手牌");
            c20.Effects.Add(new CardEffect(CardEffectType.DiscardThenDraw, 1, 2));
            Add(c20);

            var c21 = Card("C21", "鼓舞", 1, TargetType.None, CardRarity.Rare,
                "伙伴加入、密林奖励", "队伍获得 2 层士气", "士气 Buff");
            c21.Effects.Add(new CardEffect(CardEffectType.AddMorale, 2));
            Add(c21);

            var c22 = Card("C22", "干扰", 1, TargetType.SingleEnemy, CardRarity.Common,
                "草原奖励", "目标当前意图效果 -5", "削弱敌人意图");
            c22.Effects.Add(new CardEffect(CardEffectType.ReduceIntent, 5));
            Add(c22);

            var c23 = Card("C23", "深思", 1, TargetType.None, CardRarity.Rare,
                "密林奖励", "消耗手中 1 张牌，再抽 3 张", "深度过牌");
            c23.Effects.Add(new CardEffect(CardEffectType.ExhaustThenDraw, 3));
            Add(c23);

            var c24 = Card("C24", "应急预案", 0, TargetType.Self, CardRarity.Common,
                "草原事件、奖励", "二选一：获得 5 护甲，或抽 1 张", "抉择卡");
            c24.Effects.Add(new CardEffect(CardEffectType.Choice, 0));
            Add(c24);

            // ===== 3.4 战术卡 (C25–C32) =====

            var c25 = Card("C25", "集火指令", 1, TargetType.SingleEnemy, CardRarity.Rare,
                "草原精英、密林奖励", "目标在本回合内每次受到普通伤害额外 +2", "集火标记");
            c25.Effects.Add(new CardEffect(CardEffectType.FocusFire, 2));
            Add(c25);

            var c26 = Card("C26", "防线调度", 1, TargetType.AllAllies, CardRarity.Common,
                "伙伴加入", "每名存活上阵伙伴获得 4 护甲，主角获得 2 护甲", "团队防护");
            c26.Effects.Add(new CardEffect(CardEffectType.PartnerArmor, 4));
            c26.Effects.Add(new CardEffect(CardEffectType.SelfArmor, 2));
            Add(c26);

            var c27 = Card("C27", "出击命令", 1, TargetType.SingleEnemy, CardRarity.Common,
                "伙伴加入、草原奖励", "选一名伙伴对目标造成指令伤害", "伙伴指令攻击");
            c27.Effects.Add(new CardEffect(CardEffectType.PartnerDamage, 0));
            Add(c27);

            var c28 = Card("C28", "牵制命令", 1, TargetType.SingleEnemy, CardRarity.Common,
                "草原奖励", "目标当前意图效果 -5", "战术削弱");
            c28.Effects.Add(new CardEffect(CardEffectType.ReduceIntent, 5));
            Add(c28);

            var c29 = Card("C29", "集结", 1, TargetType.None, CardRarity.Rare,
                "伙伴加入、密林奖励", "队伍获得 1 层士气；每名伙伴获得 2 护甲", "团队 Buff");
            c29.Effects.Add(new CardEffect(CardEffectType.AddMorale, 1));
            c29.Effects.Add(new CardEffect(CardEffectType.PartnerArmor, 2));
            Add(c29);

            var c30 = Card("C30", "诱饵", 0, TargetType.SingleEnemy, CardRarity.Common,
                "草原奖励", "目标当前意图改为攻击主角，且效果 -3", "嘲讽");
            c30.Effects.Add(new CardEffect(CardEffectType.Taunt, 3));
            Add(c30);

            var c31 = Card("C31", "战术撤步", 1, TargetType.AllAllies, CardRarity.Rare,
                "密林奖励", "所有存活上阵单位移除 1 层围捕并获得 4 护甲", "群体净化+护甲");
            c31.Effects.Add(new CardEffect(CardEffectType.RemoveCapture, 1));
            c31.Effects.Add(new CardEffect(CardEffectType.GainArmor, 4));
            Add(c31);

            var c32 = Card("C32", "总攻号令", 2, TargetType.SingleEnemy, CardRarity.Epic,
                "密林精英、首领奖励", "主角造成 5 伤害；每名伙伴造成指令伤害", "全军出击");
            c32.Effects.Add(new CardEffect(CardEffectType.Damage, 5));
            c32.Effects.Add(new CardEffect(CardEffectType.AllPartnerDamage, 0));
            Add(c32);

            // ===== 3.5 后勤卡 (C33–C40) =====

            var c33 = Card("C33", "包扎", 1, TargetType.SingleAlly, CardRarity.Common,
                "初始、草原奖励", "恢复 6 生命", "基础治疗");
            c33.Effects.Add(new CardEffect(CardEffectType.Heal, 6));
            Add(c33);

            var c34 = Card("C34", "净化", 1, TargetType.SingleAlly, CardRarity.Rare,
                "密林奖励、医馆", "移除全部流血和 1 层疾病", "状态净化");
            c34.Effects.Add(new CardEffect(CardEffectType.RemoveBleed, 0));
            c34.Effects.Add(new CardEffect(CardEffectType.RemoveDisease, 1));
            Add(c34);

            var c35 = Card("C35", "补给", 1, TargetType.None, CardRarity.Common,
                "草原奖励", "本场战斗胜利时额外获得 3 粮食", "后勤收益");
            c35.Effects.Add(new CardEffect(CardEffectType.SupplyFood, 3));
            Add(c35);

            var c36 = Card("C36", "急救", 0, TargetType.SingleAlly, CardRarity.Common,
                "初始", "恢复 3 生命，获得 3 护甲，结算后消耗", "零费急救");
            c36.Effects.Add(new CardEffect(CardEffectType.Heal, 3));
            c36.Effects.Add(new CardEffect(CardEffectType.GainArmor, 3));
            c36.Effects.Add(new CardEffect(CardEffectType.Exhaust));
            Add(c36);

            var c37 = Card("C37", "调养", 1, TargetType.SingleAlly, CardRarity.Rare,
                "密林奖励、医馆", "移除 1 层疲劳并恢复 2 生命", "疲劳治疗");
            c37.Effects.Add(new CardEffect(CardEffectType.RemoveFatigue, 1));
            c37.Effects.Add(new CardEffect(CardEffectType.Heal, 2));
            Add(c37);

            var c38 = Card("C38", "整备", 1, TargetType.AllAllies, CardRarity.Common,
                "草原奖励", "每名存活上阵单位获得 2 护甲，再抽 1 张", "群体护甲+过牌");
            c38.Effects.Add(new CardEffect(CardEffectType.GainArmor, 2));
            c38.Effects.Add(new CardEffect(CardEffectType.Draw, 1));
            Add(c38);

            var c39 = Card("C39", "应急口粮", 0, TargetType.None, CardRarity.Common,
                "草原事件", "本场战斗胜利时额外获得 2 粮食，结算后消耗", "零费补给");
            c39.Effects.Add(new CardEffect(CardEffectType.SupplyFood, 2));
            c39.Effects.Add(new CardEffect(CardEffectType.Exhaust));
            Add(c39);

            var c40 = Card("C40", "医者之手", 2, TargetType.SingleAlly, CardRarity.Epic,
                "密林首领奖励、医馆", "恢复 10 生命并移除受伤", "强力治疗");
            c40.Effects.Add(new CardEffect(CardEffectType.Heal, 10));
            c40.Effects.Add(new CardEffect(CardEffectType.RemoveInjury));
            Add(c40);
        }
    }
}

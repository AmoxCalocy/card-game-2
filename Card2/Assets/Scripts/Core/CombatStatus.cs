using System.Collections.Generic;

namespace OneJourney.Core
{
    /// <summary>
    /// 状态规则（实施计划 A1-10，对应《MVP 配置表》§2.3）。
    /// 上限、叠加、触发时点、移除方式与多状态结算顺序在此统一。
    /// </summary>
    public static class CombatStatus
    {
        // ---- 上限 ----
        public const int MaxBleed = 5;
        public const int MaxMorale = 3;
        public const int MaxDisease = 3;
        public const int MaxFatigue = 3;
        public const int MaxArmor = 30;

        // ---- 每层效果 ----
        public const int DiseaseMaxHpPenalty = 4;
        public const int FatigueArmorPenalty = 5;
        public const int FatigueCommandDamagePenalty = 1;
        public const int MoraleBonusPerStack = 2;

        // ---- 施加与叠加（带上限，不作用于已死亡单位）----

        public static void AddBleed(CombatUnit unit, int stacks)
        {
            if (unit == null || !unit.IsAlive || stacks <= 0) return;
            unit.Bleed = System.Math.Min(MaxBleed, unit.Bleed + stacks);
        }

        public static void AddDisease(CombatUnit unit, int stacks)
        {
            if (unit == null || !unit.IsAlive || stacks <= 0) return;
            unit.Disease = System.Math.Min(MaxDisease, unit.Disease + stacks);
            // 疾病使最大生命降低；当前生命高于新上限时降至新上限
            if (unit.CurrentHp > unit.EffectiveMaxHp)
            {
                unit.CurrentHp = unit.EffectiveMaxHp;
            }
        }

        public static void AddFatigue(CombatUnit unit, int stacks)
        {
            if (unit == null || !unit.IsAlive || stacks <= 0) return;
            unit.Fatigue = System.Math.Min(MaxFatigue, unit.Fatigue + stacks);
            // 疲劳降低护甲上限；当前护甲高于新上限时降至新上限
            if (unit.Armor > unit.EffectiveArmorCap)
            {
                unit.Armor = unit.EffectiveArmorCap;
            }
        }

        public static void AddMorale(int stacks)
        {
            CombatManager.AddMorale(stacks);
        }

        // ---- 移除 ----

        public static void RemoveBleed(CombatUnit unit, int stacks)
        {
            if (unit == null) return;
            unit.Bleed = System.Math.Max(0, unit.Bleed - stacks);
        }

        public static void RemoveAllBleed(CombatUnit unit)
        {
            if (unit == null) return;
            unit.Bleed = 0;
        }

        public static void RemoveDisease(CombatUnit unit, int stacks)
        {
            if (unit == null) return;
            unit.Disease = System.Math.Max(0, unit.Disease - stacks);
        }

        public static void RemoveFatigue(CombatUnit unit, int stacks)
        {
            if (unit == null) return;
            unit.Fatigue = System.Math.Max(0, unit.Fatigue - stacks);
        }

        // ---- 回合开始结算 ----

        /// <summary>
        /// 单位回合开始：流血扣血（真实伤害，无视护甲），伤害后层数 -1。
        /// 流血致死时触发战斗结束检查。返回结算文本或 null（无流血）。
        /// </summary>
        public static string TriggerTurnStartBleed(CombatUnit unit)
        {
            if (unit == null || !unit.IsAlive || unit.Bleed <= 0) return null;

            int stacks = unit.Bleed;
            int beforeHp = unit.CurrentHp;
            unit.Bleed = stacks - 1;
            unit.TakeTrueDamage(stacks);

            string text = unit.DisplayName + " 因流血损失 " + stacks + " 生命（" + beforeHp + " → " + unit.CurrentHp + "），流血剩 " + unit.Bleed + " 层";
            RunRecord.Log(RecordCategory.General, text);

            if (!unit.IsAlive)
            {
                text += "，单位死亡";
                RunRecord.Log(RecordCategory.General, unit.DisplayName + " 因流血死亡");
                CombatManager.CheckEndCondition();
            }

            return text;
        }

        /// <summary>结算一支队伍所有存活单位的回合开始流血。</summary>
        public static string TriggerTeamTurnStartBleed(IReadOnlyList<CombatUnit> team)
        {
            var texts = new List<string>();
            foreach (var u in team)
            {
                string t = TriggerTurnStartBleed(u);
                if (t != null) texts.Add(t);
            }

            return texts.Count > 0 ? string.Join("\n", texts) : null;
        }
    }
}

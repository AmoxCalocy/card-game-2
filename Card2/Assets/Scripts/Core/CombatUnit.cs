using System.Collections.Generic;

namespace OneJourney.Core
{
    /// <summary>
    /// 战斗中的单个单位（主角/伙伴/敌人）。
    /// 战斗结束后，仅 CurrentHp 等长期值带回战役状态。
    /// </summary>
    public sealed class CombatUnit
    {
        public string Id;
        public string DisplayName;
        public int MaxHp;
        public int CurrentHp;
        public int Armor;
        public bool IsAlive => CurrentHp > 0;
        public bool IsPlayerCharacter;

        /// <summary>基础指令伤害（伙伴/敌人用）。</summary>
        public int CommandDamage;

        // ---- 状态（A1-10，上限见《MVP 配置表》§2.3）----
        /// <summary>流血 0-5 层：单位回合开始时受到层数真实伤害，伤害后 -1。</summary>
        public int Bleed;

        /// <summary>疾病 0-3 层（战役长期）：每层最大生命 -4。</summary>
        public int Disease;

        /// <summary>疲劳 0-3 层（战役长期）：每层护甲上限 -5、指令伤害 -1。</summary>
        public int Fatigue;

        public CombatUnit(string id, string displayName, int maxHp, int commandDamage = 0, bool isPlayerCharacter = false)
        {
            Id = id;
            DisplayName = displayName;
            MaxHp = maxHp;
            CurrentHp = maxHp;
            Armor = 0;
            CommandDamage = commandDamage;
            IsPlayerCharacter = isPlayerCharacter;
        }

        /// <summary>疾病修正后的最大生命（每层 -4）。</summary>
        public int EffectiveMaxHp => System.Math.Max(1, MaxHp - Disease * CombatStatus.DiseaseMaxHpPenalty);

        /// <summary>疲劳修正后的护甲上限（每层 -5，下限 0）。</summary>
        public int EffectiveArmorCap => System.Math.Max(0, CombatStatus.MaxArmor - Fatigue * CombatStatus.FatigueArmorPenalty);

        /// <summary>疲劳修正后的指令伤害（每层 -1，下限 0）。</summary>
        public int EffectiveCommandDamage => System.Math.Max(0, CommandDamage - Fatigue * CombatStatus.FatigueCommandDamagePenalty);

        /// <summary>获得护甲（不超过疲劳修正后的上限）。</summary>
        public void AddArmor(int amount)
        {
            if (amount <= 0) return;
            Armor = System.Math.Min(EffectiveArmorCap, Armor + amount);
        }

        /// <summary>对护甲的普通伤害吸收：先扣 Armor 再扣 Hp，实际扣血量。</summary>
        public int TakeDamage(int amount)
        {
            if (amount <= 0 || !IsAlive) return 0;

            int absorbed = 0;
            if (Armor > 0)
            {
                absorbed = System.Math.Min(Armor, amount);
                Armor -= absorbed;
                amount -= absorbed;
            }

            int hpLoss = System.Math.Min(CurrentHp, amount);
            CurrentHp -= hpLoss;
            return absorbed + hpLoss;
        }

        /// <summary>真实伤害：无视护甲直接扣生命。</summary>
        public int TakeTrueDamage(int amount)
        {
            if (amount <= 0 || !IsAlive) return 0;
            int hpLoss = System.Math.Min(CurrentHp, amount);
            CurrentHp -= hpLoss;
            return hpLoss;
        }

        public void Heal(int amount)
        {
            if (!IsAlive || amount <= 0) return;
            CurrentHp = System.Math.Min(CurrentHp + amount, EffectiveMaxHp);
        }

        public CombatUnit Clone()
        {
            return new CombatUnit(Id, DisplayName, MaxHp, CommandDamage, IsPlayerCharacter)
            {
                CurrentHp = CurrentHp,
                Armor = Armor,
                Bleed = Bleed,
                Disease = Disease,
                Fatigue = Fatigue
            };
        }

        /// <summary>创建模拟主角单位。</summary>
        public static CombatUnit CreatePlayer(int hp, int cmdDmg)
        {
            return new CombatUnit("PLAYER", "旅人", hp, cmdDmg, true);
        }

        /// <summary>创建模拟伙伴单位。</summary>
        public static CombatUnit CreateCompanion(string id, string name, int hp, int cmdDmg)
        {
            return new CombatUnit(id, name, hp, cmdDmg);
        }

        /// <summary>创建模拟敌人单位。</summary>
        public static CombatUnit CreateEnemy(string id, string name, int hp)
        {
            return new CombatUnit(id, name, hp);
        }
    }
}

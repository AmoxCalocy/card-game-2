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

        public void Heal(int amount)
        {
            if (!IsAlive || amount <= 0) return;
            CurrentHp = System.Math.Min(CurrentHp + amount, MaxHp);
        }

        public CombatUnit Clone()
        {
            return new CombatUnit(Id, DisplayName, MaxHp, CommandDamage, IsPlayerCharacter)
            {
                CurrentHp = CurrentHp,
                Armor = Armor
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

using System.Collections.Generic;

namespace OneJourney.Core
{
    /// <summary>
    /// 目标选择与伤害结算（实施计划 A1-9）。
    /// 统一结算顺序：目标合法性 → 伤害修正 → 护甲吸收 → 生命扣减 → 死亡处理 → 战斗结束检查。
    /// 被击败单位立即失去选择资格，同一批次后续结算自动跳过。
    /// </summary>
    public static class CombatResolver
    {
        /// <summary>
        /// 解析某目标类型的合法目标列表（仅存活单位）。
        /// 无合法目标时 issue 非空并返回空列表。
        /// </summary>
        public static List<CombatUnit> ResolveTargets(TargetType type, out string issue)
        {
            issue = null;

            if (CombatManager.PlayerTeam == null || CombatManager.EnemyTeam == null)
            {
                issue = "战斗未激活";
                return new List<CombatUnit>();
            }

            switch (type)
            {
                case TargetType.SingleEnemy:
                case TargetType.AllEnemies:
                {
                    var enemies = Alive(CombatManager.EnemyTeam);
                    if (enemies.Count == 0) issue = "没有存活敌人";
                    return enemies;
                }

                case TargetType.Self:
                {
                    var self = CombatManager.PlayerTeam.Find(u => u.IsPlayerCharacter);
                    if (self == null || !self.IsAlive) issue = "主角不存在或已死亡";
                    return self != null && self.IsAlive ? new List<CombatUnit> { self } : new List<CombatUnit>();
                }

                case TargetType.SingleAlly:
                case TargetType.AllAllies:
                {
                    var allies = Alive(CombatManager.PlayerTeam);
                    if (allies.Count == 0) issue = "没有存活单位";
                    return allies;
                }

                default: // None：无目标
                    return new List<CombatUnit>();
            }
        }

        /// <summary>
        /// 对单个目标结算一次普通伤害。
        /// 返回可读结算文本（含护甲吸收与生命变化）。
        /// </summary>
        public static string ApplyDamage(CombatUnit target, int amount)
        {
            if (target == null || !target.IsAlive)
            {
                return "目标已死亡，结算跳过";
            }

            if (amount <= 0)
            {
                return target.DisplayName + " 未受到伤害";
            }

            // 士气：玩家回合首次造成普通伤害时，每层 +2 伤害，触发后清空（仅玩家来源伤害）
            if (!CombatManager.MoraleUsedThisTurn && CombatManager.Morale > 0)
            {
                int bonus = CombatManager.Morale * CombatStatus.MoraleBonusPerStack;
                amount += bonus;
                CombatManager.ClearMorale();
                CombatManager.MarkMoraleUsed();
                RunRecord.Log(RecordCategory.General, "士气触发，伤害 +" + bonus);
            }

            int beforeHp = target.CurrentHp;
            int beforeArmor = target.Armor;
            int total = target.TakeDamage(amount);

            string text = target.DisplayName + " 受到 " + amount + " 点伤害";
            if (beforeArmor > 0 && target.Armor < beforeArmor)
            {
                text += "（护甲吸收 " + (beforeArmor - target.Armor) + "）";
            }

            text += "，生命 " + beforeHp + " → " + target.CurrentHp;

            if (!target.IsAlive)
            {
                text += "，单位死亡";
                RunRecord.Log(RecordCategory.General, target.DisplayName + " 被击败");
                CombatManager.CheckEndCondition();
            }

            return text;
        }

        /// <summary>打出测试卡：费用校验 → 目标解析 → 伤害结算 → 结束检查。</summary>
        public static string PlayTestCard(int cost, TargetType type, int damage)
        {
            if (!CombatManager.CanPlayerAct)
            {
                return "当前不能出牌";
            }

            if (!CombatManager.SpendEnergy(cost))
            {
                return "能量不足（需要 " + cost + "，当前 " + CombatManager.Energy + "）";
            }

            var targets = ResolveTargets(type, out string issue);
            if (issue != null)
            {
                // 退还能量
                CombatManager.RefundEnergy(cost);
                return "无合法目标：" + issue;
            }

            if (targets.Count == 0)
            {
                return "无目标卡牌直接结算（当前测试卡带伤害，忽略）";
            }

            int totalDealt = 0;
            var texts = new List<string>();

            bool isSingle = type == TargetType.SingleEnemy || type == TargetType.SingleAlly || type == TargetType.Self;
            if (isSingle)
            {
                // 单体目标：仅结算第一个存活目标（UI 层负责选具体目标）
                texts.Add(ApplyDamage(targets[0], damage));
                totalDealt = 1;
            }
            else
            {
                foreach (var t in targets)
                {
                    texts.Add(ApplyDamage(t, damage));
                    totalDealt++;
                }
            }

            RunRecord.Log(RecordCategory.General, "测试卡结算：" + totalDealt + " 个目标，伤害 " + damage);
            return string.Join("\n", texts);
        }

        private static List<CombatUnit> Alive(List<CombatUnit> units)
        {
            var result = new List<CombatUnit>();
            foreach (var u in units)
            {
                if (u.IsAlive) result.Add(u);
            }

            return result;
        }
    }
}

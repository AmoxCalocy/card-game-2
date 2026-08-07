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
        /// fromPlayer=true 时接入士气加成（玩家回合首次伤害）。
        /// 返回可读结算文本（含护甲吸收与生命变化）。
        /// </summary>
        public static string ApplyDamage(CombatUnit target, int amount, bool fromPlayer = true)
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
            if (fromPlayer && !CombatManager.MoraleUsedThisTurn && CombatManager.Morale > 0)
            {
                int bonus = CombatManager.Morale * CombatStatus.MoraleBonusPerStack;
                amount += bonus;
                CombatManager.ClearMorale();
                CombatManager.MarkMoraleUsed();
                RunRecord.Log(RecordCategory.General, "士气触发，伤害 +" + bonus);
            }

            // 集火标记（仅玩家来源伤害）
            if (fromPlayer && target.FocusFireExtra > 0)
            {
                amount += target.FocusFireExtra;
                RunRecord.Log(RecordCategory.General, "集火触发，伤害 +" + target.FocusFireExtra);
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
        [System.Obsolete("请使用 PlayCard(int handIndex, CombatUnit selectedTarget) 代替。")]
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

        /// <summary>
        /// 从手牌打出一张卡（实施计划 A1-13）。
        /// 流程：费用校验 → 目标解析 → 移除手牌 → 逐个效果结算 → 弃牌/消耗 → 结束检查。
        /// </summary>
        /// <param name="handIndex">手牌索引（0-based），必须有效。</param>
        /// <param name="selectedTarget">单体目标（可选，null 则自动选第一个合法目标）。</param>
        public static string PlayCard(int handIndex, CombatUnit selectedTarget = null)
        {
            if (!CombatManager.CanPlayerAct) return "当前不能出牌";
            if (CombatManager.Deck == null || handIndex < 0 || handIndex >= CombatManager.Deck.HandSize)
                return "手牌索引无效";

            string cardId = CombatManager.Deck.Hand[handIndex];
            var card = CardCatalog.Find(cardId);
            if (card == null) return "找不到卡牌定义：" + cardId;

            // 费用计算（含减费）
            int actualCost = card.Cost;
            if (CombatManager.CostReductionRemaining > 0)
            {
                int reduction = System.Math.Min(actualCost, CombatManager.CostReductionRemaining);
                actualCost -= reduction;
                CombatManager.CostReductionRemaining -= reduction;
            }

            if (!CombatManager.SpendEnergy(actualCost))
                return "能量不足（需要 " + actualCost + "，当前 " + CombatManager.Energy + "）";

            // 目标解析
            bool needsTarget = card.TargetType != TargetType.None;
            var targets = ResolveTargets(card.TargetType, out string issue);
            if (needsTarget && issue != null)
            {
                CombatManager.RefundEnergy(actualCost);
                return "无合法目标：" + issue;
            }

            // 从手牌移除（暂存，结算后加入对应区域）
            CombatManager.Deck.Hand.RemoveAt(handIndex);
            bool exhausted = false;

            // 逐个效果结算
            var texts = new System.Collections.Generic.List<string>();
            texts.Add(card.DisplayName + "（" + actualCost + "费）");

            foreach (var eff in card.Effects)
            {
                string result = ApplyEffect(eff, card.TargetType, targets, ref selectedTarget, ref exhausted);
                if (!string.IsNullOrEmpty(result)) texts.Add(result);
                if (!CombatManager.IsActive) break; // 战斗结束则中断
            }

            // 卡牌归位
            if (exhausted)
                CombatManager.Deck.ExhaustZone.Add(cardId);
            else
                CombatManager.Deck.DiscardPile.Add(cardId);

            RunRecord.Log(RecordCategory.General, "打出 " + card.DisplayName + "（" + cardId + "）");
            return string.Join("\n", texts);
        }

        private static string ApplyEffect(CardEffect eff, TargetType cardTargetType,
            System.Collections.Generic.List<CombatUnit> targets, ref CombatUnit selectedTarget, ref bool exhausted)
        {
            // 单体效果优先使用用户选中的目标
            bool isSingle = cardTargetType == TargetType.SingleEnemy
                || cardTargetType == TargetType.SingleAlly
                || cardTargetType == TargetType.Self;
            CombatUnit singleTarget = isSingle && selectedTarget != null && selectedTarget.IsAlive
                ? selectedTarget
                : (targets.Count > 0 ? targets[0] : null);

            // 条件检查
            if (eff.Condition != EffectCondition.None)
            {
                bool met = false;
                if (targets.Count > 0)
                {
                    var checkUnit = targets[0];
                    switch (eff.Condition)
                    {
                        case EffectCondition.TargetBleedGE2:
                            met = checkUnit.Bleed >= 2;
                            break;
                        case EffectCondition.SelfArmorGE10:
                            var self = CombatManager.PlayerCharacter();
                            met = self != null && self.Armor >= 10;
                            break;
                    }
                }
                if (!met) return null; // 条件不满足，跳过
            }

            switch (eff.Type)
            {
                case CardEffectType.Damage:
                {
                    if (cardTargetType == TargetType.AllEnemies || cardTargetType == TargetType.AllAllies)
                    {
                        foreach (var t in targets)
                        {
                            if (!t.IsAlive) continue;
                            CombatResolver.ApplyDamage(t, eff.P0);
                            if (!CombatManager.IsActive) return null;
                        }
                        return "造成 " + eff.P0 + " 伤害（全体）";
                    }
                    else if (singleTarget != null)
                    {
                        CombatResolver.ApplyDamage(singleTarget, eff.P0);
                        return "造成 " + eff.P0 + " 伤害 → " + singleTarget.DisplayName;
                    }
                    return null;
                }

                case CardEffectType.GainArmor:
                {
                    if (isSingle && singleTarget != null)
                    {
                        singleTarget.AddArmor(eff.P0);
                        return singleTarget.DisplayName + " 获得 " + eff.P0 + " 护甲";
                    }
                    foreach (var t in targets)
                    {
                        if (!t.IsAlive) continue;
                        t.AddArmor(eff.P0);
                    }
                    return "获得 " + eff.P0 + " 护甲";
                }

                case CardEffectType.Heal:
                {
                    if (isSingle && singleTarget != null)
                    {
                        singleTarget.Heal(eff.P0);
                        return singleTarget.DisplayName + " 恢复 " + eff.P0 + " 生命";
                    }
                    foreach (var t in targets)
                    {
                        if (!t.IsAlive) continue;
                        t.Heal(eff.P0);
                    }
                    return "恢复 " + eff.P0 + " 生命";
                }

                case CardEffectType.Draw:
                {
                    CombatManager.Deck.DrawToHand(eff.P0, GameStartParameters.MaxHandSize);
                    return "抽 " + eff.P0 + " 张";
                }

                case CardEffectType.ApplyBleed:
                {
                    if (isSingle && singleTarget != null)
                    {
                        CombatStatus.AddBleed(singleTarget, eff.P0);
                        return singleTarget.DisplayName + " 获得 " + eff.P0 + " 层流血";
                    }
                    foreach (var t in targets)
                    {
                        if (!t.IsAlive) continue;
                        CombatStatus.AddBleed(t, eff.P0);
                    }
                    return "施加 " + eff.P0 + " 层流血";
                }

                case CardEffectType.ApplyDisease:
                {
                    if (isSingle && singleTarget != null)
                    {
                        CombatStatus.AddDisease(singleTarget, eff.P0);
                        return singleTarget.DisplayName + " 获得 " + eff.P0 + " 层疾病";
                    }
                    foreach (var t in targets)
                    {
                        if (!t.IsAlive) continue;
                        CombatStatus.AddDisease(t, eff.P0);
                    }
                    return "施加 " + eff.P0 + " 层疾病";
                }

                case CardEffectType.ApplyFatigue:
                {
                    if (isSingle && singleTarget != null)
                    {
                        CombatStatus.AddFatigue(singleTarget, eff.P0);
                        return singleTarget.DisplayName + " 获得 " + eff.P0 + " 层疲劳";
                    }
                    foreach (var t in targets)
                    {
                        if (!t.IsAlive) continue;
                        CombatStatus.AddFatigue(t, eff.P0);
                    }
                    return "施加 " + eff.P0 + " 层疲劳";
                }

                case CardEffectType.AddMorale:
                {
                    CombatManager.AddMorale(eff.P0);
                    return "士气 +" + eff.P0;
                }

                case CardEffectType.RemoveBleed:
                {
                    int stacks = eff.P0;
                    if (isSingle && singleTarget != null)
                    {
                        if (stacks == 0) CombatStatus.RemoveAllBleed(singleTarget);
                        else CombatStatus.RemoveBleed(singleTarget, stacks);
                        return stacks == 0 ? "移除全部流血" : "移除 " + stacks + " 层流血";
                    }
                    foreach (var t in targets)
                    {
                        if (!t.IsAlive) continue;
                        if (stacks == 0) CombatStatus.RemoveAllBleed(t);
                        else CombatStatus.RemoveBleed(t, stacks);
                    }
                    return stacks == 0 ? "移除全部流血" : "移除 " + stacks + " 层流血";
                }

                case CardEffectType.RemoveDisease:
                {
                    if (isSingle && singleTarget != null)
                    {
                        singleTarget.Disease = System.Math.Max(0, singleTarget.Disease - eff.P0);
                        singleTarget.CurrentHp = System.Math.Min(singleTarget.CurrentHp, singleTarget.EffectiveMaxHp);
                        return singleTarget.DisplayName + " 移除 " + eff.P0 + " 层疾病";
                    }
                    foreach (var t in targets)
                    {
                        if (!t.IsAlive) continue;
                        t.Disease = System.Math.Max(0, t.Disease - eff.P0);
                        t.CurrentHp = System.Math.Min(t.CurrentHp, t.EffectiveMaxHp);
                    }
                    return "移除 " + eff.P0 + " 层疾病";
                }

                case CardEffectType.RemoveFatigue:
                {
                    if (isSingle && singleTarget != null)
                    {
                        singleTarget.Fatigue = System.Math.Max(0, singleTarget.Fatigue - eff.P0);
                        return singleTarget.DisplayName + " 移除 " + eff.P0 + " 层疲劳";
                    }
                    foreach (var t in targets)
                    {
                        if (!t.IsAlive) continue;
                        t.Fatigue = System.Math.Max(0, t.Fatigue - eff.P0);
                    }
                    return "移除 " + eff.P0 + " 层疲劳";
                }

                case CardEffectType.RemoveArmor:
                {
                    if (isSingle && singleTarget != null)
                    {
                        singleTarget.Armor = System.Math.Max(0, singleTarget.Armor - eff.P0);
                        return singleTarget.DisplayName + " 护甲 -" + eff.P0;
                    }
                    foreach (var t in targets)
                    {
                        if (!t.IsAlive) continue;
                        t.Armor = System.Math.Max(0, t.Armor - eff.P0);
                    }
                    return "移除 " + eff.P0 + " 护甲";
                }

                case CardEffectType.ReduceIntent:
                {
                    if (isSingle && singleTarget != null && singleTarget is EnemyUnit eu && eu.CurrentIntent != null)
                    {
                        eu.CurrentIntent.Damage = System.Math.Max(0, eu.CurrentIntent.Damage - eff.P0);
                        eu.CurrentIntent.ArmorGain = System.Math.Max(0, eu.CurrentIntent.ArmorGain - eff.P0);
                        eu.CurrentIntent.PlunderStacks = System.Math.Max(0, eu.CurrentIntent.PlunderStacks - eff.P0);
                        return "意图效果 -" + eff.P0;
                    }
                    foreach (var t in targets)
                    {
                        if (!t.IsAlive) continue;
                        if (t is EnemyUnit eu2 && eu2.CurrentIntent != null)
                        {
                            eu2.CurrentIntent.Damage = System.Math.Max(0, eu2.CurrentIntent.Damage - eff.P0);
                            eu2.CurrentIntent.ArmorGain = System.Math.Max(0, eu2.CurrentIntent.ArmorGain - eff.P0);
                            eu2.CurrentIntent.PlunderStacks = System.Math.Max(0, eu2.CurrentIntent.PlunderStacks - eff.P0);
                        }
                    }
                    return "意图效果 -" + eff.P0;
                }

                case CardEffectType.SelfArmor:
                {
                    var self = CombatManager.PlayerCharacter();
                    if (self != null) self.AddArmor(eff.P0);
                    return "主角获得 " + eff.P0 + " 护甲";
                }

                case CardEffectType.PartnerArmor:
                {
                    int count = 0;
                    if (CombatManager.PlayerTeam != null)
                    {
                        foreach (var u in CombatManager.PlayerTeam)
                        {
                            if (!u.IsAlive || u.IsPlayerCharacter) continue;
                            u.AddArmor(eff.P0);
                            count++;
                        }
                    }
                    return count > 0 ? count + " 名伙伴各获得 " + eff.P0 + " 护甲" : null;
                }

                case CardEffectType.BonusDrawNextTurn:
                {
                    CombatManager.PendingBonusDraw += eff.P0;
                    return "下回合额外抽 " + eff.P0 + " 张";
                }

                case CardEffectType.CostReduction:
                {
                    CombatManager.CostReductionRemaining += eff.P0;
                    return "下张牌费用 -" + eff.P0;
                }

                case CardEffectType.Exhaust:
                {
                    exhausted = true;
                    return null; // 标记由外层处理
                }

                case CardEffectType.SupplyFood:
                {
                    // MVP：标记供战斗胜利后结算，暂记录日志
                    RunRecord.Log(RecordCategory.General, "补给标记：胜利后额外获得 " + eff.P0 + " 粮食");
                    return "胜利后 +" + eff.P0 + " 粮食";
                }

                case CardEffectType.FocusFire:
                {
                    foreach (var t in targets)
                    {
                        if (!t.IsAlive) continue;
                        t.FocusFireExtra = eff.P0;
                    }
                    return "集火标记 +" + eff.P0;
                }

                case CardEffectType.Taunt:
                {
                    foreach (var t in targets)
                    {
                        if (!t.IsAlive) continue;
                        if (t is EnemyUnit eu && eu.CurrentIntent != null)
                        {
                            eu.CurrentIntent.TargetsPlayer = true;
                            eu.CurrentIntent.Damage = System.Math.Max(0, eu.CurrentIntent.Damage - eff.P0);
                            eu.CurrentIntent.PlunderStacks = System.Math.Max(0, eu.CurrentIntent.PlunderStacks - eff.P0);
                        }
                    }
                    return "诱饵生效，意图改向主角，效果 -" + eff.P0;
                }

                case CardEffectType.RemoveCapture:
                {
                    foreach (var t in targets)
                    {
                        if (!t.IsAlive) continue;
                        // 围捕暂未实现独立字段，记录日志供后续
                        RunRecord.Log(RecordCategory.General, t.DisplayName + " 移除围捕 " + eff.P0 + " 层");
                    }
                    return "移除围捕";
                }

                case CardEffectType.RemoveInjury:
                {
                    foreach (var t in targets)
                    {
                        if (!t.IsAlive) continue;
                        // 受伤暂未实现独立字段，记录日志
                        RunRecord.Log(RecordCategory.General, t.DisplayName + " 移除受伤");
                    }
                    return "移除受伤";
                }

                case CardEffectType.PartnerDamage:
                {
                    if (CombatManager.PlayerTeam == null || targets.Count == 0) return null;
                    var enemy = targets[0];
                    if (!enemy.IsAlive) return null;

                    // 选第一个存活非主角伙伴
                    CombatUnit partner = null;
                    foreach (var u in CombatManager.PlayerTeam)
                    {
                        if (!u.IsAlive || u.IsPlayerCharacter) continue;
                        partner = u;
                        break;
                    }

                    if (partner == null) return "没有可用的伙伴";
                    int dmg = partner.EffectiveCommandDamage + eff.P0;
                    CombatResolver.ApplyDamage(enemy, dmg);
                    return partner.DisplayName + " 造成 " + dmg + " 伤害";
                }

                case CardEffectType.AllPartnerDamage:
                {
                    if (CombatManager.PlayerTeam == null || targets.Count == 0) return null;
                    var enemy = targets[0];
                    if (!enemy.IsAlive) return null;

                    int hitCount = 0;
                    foreach (var u in CombatManager.PlayerTeam)
                    {
                        if (!u.IsAlive || u.IsPlayerCharacter) continue;
                        int dmg = u.EffectiveCommandDamage + eff.P0;
                        CombatResolver.ApplyDamage(enemy, dmg);
                        hitCount++;
                        if (!enemy.IsAlive || !CombatManager.IsActive) break;
                    }
                    return hitCount + " 名伙伴各造成指令伤害";
                }

                case CardEffectType.DrawThenDiscard:
                {
                    CombatManager.Deck.DrawToHand(eff.P0, GameStartParameters.MaxHandSize);
                    int toDiscard = eff.P1;
                    for (int i = 0; i < toDiscard && CombatManager.Deck.HandSize > 0; i++)
                    {
                        int idx = CombatManager.Deck.HandSize - 1;
                        string cid = CombatManager.Deck.Hand[idx];
                        CombatManager.Deck.DiscardFromHand(cid);
                    }
                    return "抽 " + eff.P0 + " 弃 " + toDiscard;
                }

                case CardEffectType.DiscardThenDraw:
                {
                    int toDiscard = eff.P0;
                    for (int i = 0; i < toDiscard && CombatManager.Deck.HandSize > 0; i++)
                    {
                        string cid = CombatManager.Deck.Hand[0];
                        CombatManager.Deck.DiscardFromHand(cid);
                    }
                    CombatManager.Deck.DrawToHand(eff.P1, GameStartParameters.MaxHandSize);
                    return "弃 " + eff.P0 + " 抽 " + eff.P1;
                }

                case CardEffectType.ExhaustThenDraw:
                {
                    if (CombatManager.Deck.HandSize > 0)
                    {
                        int idx = CombatManager.Deck.HandSize - 1;
                        string cid = CombatManager.Deck.Hand[idx];
                        CombatManager.Deck.ExhaustFromHand(cid);
                    }
                    CombatManager.Deck.DrawToHand(eff.P0, GameStartParameters.MaxHandSize);
                    return "消耗 1 张，抽 " + eff.P0 + " 张";
                }

                case CardEffectType.Choice:
                {
                    // MVP：默认选第一个选项（护甲）
                    if (eff.P0 == 0)
                    {
                        var self = CombatManager.PlayerCharacter();
                        if (self != null) self.AddArmor(5);
                        return "应急：获得 5 护甲";
                    }
                    else
                    {
                        CombatManager.Deck.DrawToHand(1, GameStartParameters.MaxHandSize);
                        return "应急：抽 1 张";
                    }
                }

                default:
                    return null;
            }
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

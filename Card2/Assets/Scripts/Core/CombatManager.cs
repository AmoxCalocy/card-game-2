using System.Collections.Generic;

namespace OneJourney.Core
{
    /// <summary>战斗宏观阶段。</summary>
    public enum CombatPhase
    {
        None = 0,
        Initializing = 1,
        Running = 2,
        Victory = 3,
        Defeat = 4,
        Ended = 5
    }

    /// <summary>回合内阶段（仅在 Running 状态有效）。</summary>
    public enum TurnPhase
    {
        None = 0,
        PlayerTurnStart = 1,  // 回合开始结算
        PlayerTurn = 2,       // 玩家行动（可出牌）
        PlayerTurnEnd = 3,    // 回合结束结算
        EnemyTurn = 4,        // 敌方行动
        EnemyTurnEnd = 5      // 敌方回合结束、过渡至下一回合
    }

    /// <summary>
    /// 战斗生命周期 + 回合结构（实施计划 A1-6 / A1-7）。
    /// MVP 固定玩家先手，能量每回合重置为 3 且不保留，手牌保留但上限 5。
    /// </summary>
    public static class CombatManager
    {
        public static CombatPhase Phase { get; private set; } = CombatPhase.None;
        public static TurnPhase CurrentTurnPhase { get; private set; } = TurnPhase.None;

        public static List<CombatUnit> PlayerTeam { get; private set; }
        public static List<CombatUnit> EnemyTeam { get; private set; }
        public static CombatDeck Deck { get; private set; }

        public static int TurnNumber { get; private set; }
        public static int Energy { get; private set; }
        public const int MaxEnergy = 3;

        /// <summary>队伍士气 0-3 层（共享）：玩家回合首次造成普通伤害时每层 +2 伤害，触发后清空。</summary>
        public static int Morale { get; private set; }

        /// <summary>本回合是否已消耗士气。</summary>
        public static bool MoraleUsedThisTurn { get; private set; }

        /// <summary>获得士气（夹取 0-MaxMorale）。</summary>
        public static int AddMorale(int stacks)
        {
            Morale = System.Math.Min(CombatStatus.MaxMorale, Morale + System.Math.Max(0, stacks));
            return Morale;
        }

        /// <summary>清空士气（士气触发后调用）。</summary>
        public static void ClearMorale()
        {
            Morale = 0;
        }

        /// <summary>标记本回合士气已消耗（首次伤害触发后调用）。</summary>
        public static void MarkMoraleUsed()
        {
            MoraleUsedThisTurn = true;
        }

        public static bool RetreatAllowed => false;

        public static bool IsActive => Phase >= CombatPhase.Initializing && Phase < CombatPhase.Ended;

        /// <summary>玩家当前是否可以执行出牌操作。</summary>
        public static bool CanPlayerAct => IsActive && Phase == CombatPhase.Running && CurrentTurnPhase == TurnPhase.PlayerTurn;

        // ------- 初始化 -------

        public static void Init(
            IReadOnlyList<CombatUnit> playerUnits,
            IReadOnlyList<CombatUnit> enemyUnits,
            IReadOnlyList<string> campaignDeck)
        {
            if (playerUnits == null || playerUnits.Count == 0)
            {
                RunRecord.Log(RecordCategory.General, "[错误] 战斗初始化失败：玩家队伍为空");
                Phase = CombatPhase.None;
                return;
            }

            if (enemyUnits == null || enemyUnits.Count == 0)
            {
                RunRecord.Log(RecordCategory.General, "[错误] 战斗初始化失败：敌人队伍为空");
                Phase = CombatPhase.None;
                return;
            }

            Phase = CombatPhase.Initializing;

            PlayerTeam = new List<CombatUnit>(playerUnits.Count);
            foreach (var u in playerUnits) PlayerTeam.Add(u.Clone());

            EnemyTeam = new List<CombatUnit>(enemyUnits.Count);
            foreach (var u in enemyUnits) EnemyTeam.Add(u.Clone());

            Deck = new CombatDeck();
            Deck.InitFromCampaign(campaignDeck, RunSession.Random);
            Deck.DrawToHand(GameStartParameters.InitialHandSize, GameStartParameters.MaxHandSize);

            Phase = CombatPhase.Running;
            RunRecord.Log(RecordCategory.General, "战斗开始：玩家 " + PlayerTeam.Count + " 人 vs 敌人 " + EnemyTeam.Count + " 个，牌组 " + campaignDeck.Count + " 张");

            BeginPlayerTurn();
        }

        // ------- 回合流转 -------

        /// <summary>玩家回合开始：抽 1 张牌，重置能量为 3，结算回合开始状态。</summary>
        public static void BeginPlayerTurn()
        {
            if (!IsActive || Phase != CombatPhase.Running) return;
            if (CurrentTurnPhase != TurnPhase.None && CurrentTurnPhase != TurnPhase.EnemyTurnEnd) return;

            TurnNumber++;
            Energy = MaxEnergy;
            MoraleUsedThisTurn = false;
            CurrentTurnPhase = TurnPhase.PlayerTurnStart;

            RunRecord.Log(RecordCategory.General, "第 " + TurnNumber + " 回合开始，能量重置为 " + MaxEnergy);

            // 回合开始结算：玩家队伍流血
            string bleedText = CombatStatus.TriggerTeamTurnStartBleed(PlayerTeam);
            if (bleedText != null)
            {
                RunRecord.Log(RecordCategory.General, bleedText);
                if (CheckEndConditionRaw() != null) return;
            }

            Deck.DrawToHand(GameStartParameters.CardsPerTurn, GameStartParameters.MaxHandSize);
            CurrentTurnPhase = TurnPhase.PlayerTurn;
        }

        /// <summary>玩家结束回合：未用能量清零，进入敌方回合。</summary>
        public static void EndPlayerTurn()
        {
            if (!CanPlayerAct) return;

            CurrentTurnPhase = TurnPhase.PlayerTurnEnd;
            RunRecord.Log(RecordCategory.General, "玩家结束第 " + TurnNumber + " 回合");

            Energy = 0;

            ProcessEnemyTurn();
        }

        private static void ProcessEnemyTurn()
        {
            CurrentTurnPhase = TurnPhase.EnemyTurn;
            RunRecord.Log(RecordCategory.General, "敌方回合开始");

            // 敌方回合开始：敌人流血
            string bleedText = CombatStatus.TriggerTeamTurnStartBleed(EnemyTeam);
            if (bleedText != null)
            {
                RunRecord.Log(RecordCategory.General, bleedText);
            }

            // 敌方行动 —— A1-11 实现，当前为空回合

            CurrentTurnPhase = TurnPhase.EnemyTurnEnd;
            RunRecord.Log(RecordCategory.General, "敌方回合结束");

            if (CheckEndConditionRaw() != null) return;

            BeginPlayerTurn();
        }

        // ------- 能量 -------

        public static bool CanSpendEnergy(int cost)
        {
            return CanPlayerAct && cost >= 0 && Energy >= cost;
        }

        public static bool SpendEnergy(int cost)
        {
            if (!CanSpendEnergy(cost)) return false;
            Energy -= cost;
            return true;
        }

        /// <summary>退还能量（出牌失败时回滚，仅玩家行动阶段有效）。</summary>
        public static void RefundEnergy(int cost)
        {
            if (!CanPlayerAct) return;
            Energy = System.Math.Min(MaxEnergy, Energy + cost);
        }

        // ------- 胜负判定 -------

        public static string CheckEndCondition()
        {
            return CheckEndConditionRaw();
        }

        private static string CheckEndConditionRaw()
        {
            if (!IsActive || Phase != CombatPhase.Running) return null;

            foreach (var u in PlayerTeam)
            {
                if (u.IsPlayerCharacter && !u.IsAlive)
                {
                    Phase = CombatPhase.Defeat;
                    CurrentTurnPhase = TurnPhase.None;
                    Energy = 0;
                    RunRecord.Log(RecordCategory.General, "主角死亡，战斗失败");
                    return "defeat";
                }
            }

            bool anyAlive = false;
            foreach (var e in EnemyTeam)
            {
                if (e.IsAlive) { anyAlive = true; break; }
            }

            if (!anyAlive)
            {
                Phase = CombatPhase.Victory;
                CurrentTurnPhase = TurnPhase.None;
                Energy = 0;
                RunRecord.Log(RecordCategory.General, "所有敌人被击败，战斗胜利");
                return "victory";
            }

            return null;
        }

        public static void ForceDefeat()
        {
            if (IsActive && Phase == CombatPhase.Running)
            {
                Phase = CombatPhase.Defeat;
                CurrentTurnPhase = TurnPhase.None;
                Energy = 0;
                RunRecord.Log(RecordCategory.General, "强制失败");
            }
        }

        // ------- 结束 -------

        public static void End()
        {
            Phase = CombatPhase.Ended;
            CurrentTurnPhase = TurnPhase.None;
            TurnNumber = 0;
            Energy = 0;
            Morale = 0;
            MoraleUsedThisTurn = false;
            PlayerTeam = null;
            EnemyTeam = null;
            Deck = null;
            RunRecord.Log(RecordCategory.General, "战斗结束，临时状态已清理");
        }
    }
}

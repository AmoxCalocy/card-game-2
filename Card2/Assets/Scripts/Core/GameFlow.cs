using System.Collections.Generic;
using UnityEngine;

namespace OneJourney.Core
{
    /// <summary>状态切换记录：一次合法的状态转移。</summary>
    public struct StateTransitionLog
    {
        public readonly int Sequence;
        public readonly GameState From;
        public readonly GameState To;
        public readonly string Reason;

        public StateTransitionLog(int sequence, GameState from, GameState to, string reason)
        {
            Sequence = sequence;
            From = from;
            To = to;
            Reason = reason ?? string.Empty;
        }
    }

    /// <summary>
    /// 流程状态机：定义一局游戏允许的状态流转（实施计划 A0-2）。
    /// 所有状态切换必须经过 TryTransition 校验，并写入状态日志。
    /// </summary>
    public static class GameFlow
    {
        private const int MaxLogCount = 100;

        private static readonly List<StateTransitionLog> LogList = new List<StateTransitionLog>();
        private static int _sequence;

        public static GameState CurrentState { get; private set; } = GameState.MainMenu;

        public static IReadOnlyList<StateTransitionLog> Log => LogList;

        public static StateTransitionLog? LastTransition
        {
            get
            {
                if (LogList.Count == 0)
                {
                    return null;
                }

                return LogList[LogList.Count - 1];
            }
        }

        public static event System.Action Changed;

        public static bool CanTransition(GameState from, GameState to)
        {
            return IsAllowed(from, to);
        }

        /// <summary>尝试从当前状态切换到目标状态；非法转移被拒绝且不产生任何变化。</summary>
        public static bool TryTransition(GameState to, string reason)
        {
            GameState from = CurrentState;
            if (!IsAllowed(from, to))
            {
                Debug.LogWarning("[GameFlow] 禁止的状态切换：" + RunSession.DisplayName(from) + " → " + RunSession.DisplayName(to) + "（" + reason + "）");
                return false;
            }

            CurrentState = to;
            LogList.Add(new StateTransitionLog(++_sequence, from, to, reason));
            if (LogList.Count > MaxLogCount)
            {
                LogList.RemoveAt(0);
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>重置到主菜单并清空状态日志（新会话）。</summary>
        public static void Reset()
        {
            CurrentState = GameState.MainMenu;
            LogList.Clear();
            _sequence = 0;
        }

        private static bool IsAllowed(GameState from, GameState to)
        {
            switch (from)
            {
                case GameState.MainMenu:
                    // 新游戏；测试入口可直接进入各页面
                    return to == GameState.NewGame
                        || to == GameState.Combat
                        || to == GameState.Map
                        || to == GameState.Event
                        || to == GameState.Camp;

                case GameState.NewGame:
                    return to == GameState.Map; // 初始化完成进入地图

                case GameState.Map:
                    return to == GameState.Move || to == GameState.Camp; // 选择节点移动；营地/城镇固定入口

                case GameState.Move:
                    return to == GameState.Event || to == GameState.Combat || to == GameState.Camp; // 移动结算后进入节点内容

                case GameState.Event:
                    return to == GameState.Map || to == GameState.Combat; // 事件结束回地图；事件触发战斗

                case GameState.Combat:
                    return to == GameState.Reward // 普通战斗胜利
                        || to == GameState.Victory // 击败区域首领（垂直切片结局）
                        || to == GameState.Defeat; // 战斗失败

                case GameState.Reward:
                    return to == GameState.Map; // 奖励结算后继续地图

                case GameState.Camp:
                    return to == GameState.Map; // 营地操作完成后回地图

                case GameState.Victory:
                case GameState.Defeat:
                    return to == GameState.Settlement; // 结局进入结算页

                case GameState.Settlement:
                    return to == GameState.MainMenu || to == GameState.NewGame; // 返回主菜单或同种子重开

                default:
                    return false; // None 等不可转移
            }
        }
    }
}

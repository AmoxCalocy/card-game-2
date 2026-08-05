using System.Collections.Generic;
using NUnit.Framework;
using OneJourney.Core;

namespace OneJourney.Tests.EditMode
{
    public class GameFlowTests
    {
        [TearDown]
        public void TearDown()
        {
            RunSession.Reset();
        }

        [Test]
        public void TryTransition_NewGamePath_StateAndLogMatch()
        {
            Assert.IsTrue(GameFlow.TryTransition(GameState.NewGame, "新游戏"));
            Assert.IsTrue(GameFlow.TryTransition(GameState.Map, "初始化完成"));

            Assert.AreEqual(GameState.Map, GameFlow.CurrentState);
            Assert.AreEqual(2, GameFlow.Log.Count);
            Assert.AreEqual(GameState.MainMenu, GameFlow.Log[0].From);
            Assert.AreEqual(GameState.NewGame, GameFlow.Log[0].To);
            Assert.AreEqual(GameState.NewGame, GameFlow.Log[1].From);
            Assert.AreEqual(GameState.Map, GameFlow.Log[1].To);
            Assert.AreEqual("新游戏", GameFlow.Log[0].Reason);
        }

        [Test]
        public void TryTransition_IllegalJump_RejectedWithoutSideEffects()
        {
            GameFlow.TryTransition(GameState.NewGame, "新游戏");

            bool accepted = GameFlow.TryTransition(GameState.Combat, "非法跳转");

            Assert.IsFalse(accepted);
            Assert.AreEqual(GameState.NewGame, GameFlow.CurrentState);
            Assert.AreEqual(1, GameFlow.Log.Count);
        }

        [Test]
        public void TryTransition_SameStateTwice_SecondRejected()
        {
            GameFlow.TryTransition(GameState.NewGame, "新游戏");

            bool accepted = GameFlow.TryTransition(GameState.NewGame, "重复切换");

            Assert.IsFalse(accepted);
            Assert.AreEqual(1, GameFlow.Log.Count);
        }

        [Test]
        public void TryTransition_TestEntries_AllFourPagesAllowed()
        {
            foreach (var page in new[] { GameState.Combat, GameState.Map, GameState.Event, GameState.Camp })
            {
                RunSession.Reset();

                bool accepted = GameFlow.TryTransition(page, "测试入口");

                Assert.IsTrue(accepted, "测试入口应允许进入 " + page);
                Assert.AreEqual(page, GameFlow.CurrentState);
            }
        }

        [Test]
        public void TryTransition_FullMvpRoute_ReachesSettlement()
        {
            var route = new[]
            {
                GameState.NewGame, GameState.Map, GameState.Move, GameState.Combat,
                GameState.Reward, GameState.Map, GameState.Move, GameState.Combat,
                GameState.Victory, GameState.Settlement
            };

            for (int i = 0; i < route.Length; i++)
            {
                Assert.IsTrue(GameFlow.TryTransition(route[i], "路线第 " + (i + 1) + " 步"), "第 " + (i + 1) + " 步应可转移");
            }

            Assert.AreEqual(GameState.Settlement, GameFlow.CurrentState);
        }

        [Test]
        public void TryTransition_FailureRoute_ReachesSettlementThenRestart()
        {
            Assert.IsTrue(GameFlow.TryTransition(GameState.NewGame, "新游戏"));
            Assert.IsTrue(GameFlow.TryTransition(GameState.Map, "初始化"));
            Assert.IsTrue(GameFlow.TryTransition(GameState.Move, "移动"));
            Assert.IsTrue(GameFlow.TryTransition(GameState.Combat, "遭遇"));
            Assert.IsTrue(GameFlow.TryTransition(GameState.Defeat, "失败"));
            Assert.IsTrue(GameFlow.TryTransition(GameState.Settlement, "结算"));
            Assert.IsTrue(GameFlow.TryTransition(GameState.NewGame, "同种子重开"));
            Assert.AreEqual(GameState.NewGame, GameFlow.CurrentState);
        }

        [Test]
        public void TryTransition_SettlementCannotEnterOtherFlows()
        {
            GameFlow.TryTransition(GameState.NewGame, "新游戏");
            GameFlow.TryTransition(GameState.Map, "初始化");
            GameFlow.TryTransition(GameState.Move, "移动");
            GameFlow.TryTransition(GameState.Combat, "遭遇");
            GameFlow.TryTransition(GameState.Victory, "胜利");
            GameFlow.TryTransition(GameState.Settlement, "结算");

            foreach (var blocked in new[] { GameState.Combat, GameState.Map, GameState.Event, GameState.Camp, GameState.Move, GameState.Reward })
            {
                Assert.IsFalse(GameFlow.CanTransition(GameState.Settlement, blocked), "结算未完成时禁止进入 " + blocked);
            }
        }

        [Test]
        public void AllStates_CanReachMenuOrSettlement_NoUnrecoverableMiddleState()
        {
            var states = new[]
            {
                GameState.MainMenu, GameState.NewGame, GameState.Map, GameState.Move,
                GameState.Event, GameState.Combat, GameState.Reward, GameState.Camp,
                GameState.Victory, GameState.Defeat, GameState.Settlement
            };

            foreach (var start in states)
            {
                Assert.IsTrue(CanReach(start, GameState.MainMenu) || CanReach(start, GameState.Settlement),
                    "状态 " + start + " 必须能到达主菜单或结算");
            }
        }

        private static bool CanReach(GameState from, GameState target)
        {
            var visited = new HashSet<GameState> { from };
            var queue = new Queue<GameState>();
            queue.Enqueue(from);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var next in AllStates)
                {
                    if (next == target)
                    {
                        return true;
                    }

                    if (!GameFlow.CanTransition(current, next) || visited.Contains(next))
                    {
                        continue;
                    }

                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }

            return false;
        }

        private static readonly GameState[] AllStates =
        {
            GameState.MainMenu, GameState.NewGame, GameState.Map, GameState.Move,
            GameState.Event, GameState.Combat, GameState.Reward, GameState.Camp,
            GameState.Victory, GameState.Defeat, GameState.Settlement
        };
    }
}

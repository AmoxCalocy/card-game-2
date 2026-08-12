using System.Collections.Generic;
using NUnit.Framework;
using OneJourney.Core;

namespace OneJourney.Tests.EditMode
{
    public class RegionMapTests
    {
        private static readonly int[] FixedSeeds = { 1, 2, 3, 42, 12345, 777, 20240806, 314159, 271828, 999983 };

        [SetUp]
        public void SetUp()
        {
            RunSession.Reset();
            RunSession.StartNewGame(1);
            ContentRegistry.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            CombatManager.End();
            RewardResolver.Clear();
            RegionMap.Clear();
            RunSession.Reset();
            ContentRegistry.Clear();
        }

        private static void GenerateMap(int seed)
        {
            RegionMap.Clear();
            Assert.IsTrue(RegionMap.Generate(ContentRegion.Plains, new GameRandom(seed)),
                "种子 " + seed + " 应生成成功");
            Assert.IsTrue(RegionMap.IsGenerated, "种子 " + seed + " 应生成成功");
        }

        // ---- 生成与结构（配置表 §9）----

        [Test]
        public void Generate_AllFixedSeeds_HasFourLayersWithExpectedCounts()
        {
            foreach (int seed in FixedSeeds)
            {
                GenerateMap(seed);
                Assert.AreEqual(RegionMap.LayerCount, 4, "种子 " + seed);
                Assert.AreEqual(10, RegionMap.Nodes.Count, "种子 " + seed + " 节点总数");

                // 每层节点数：1/2/3 层各 3 个，第 4 层 1 个
                for (int layer = 1; layer <= 3; layer++)
                {
                    int count = CountLayer(layer);
                    Assert.AreEqual(3, count, "种子 " + seed + " 第 " + layer + " 层节点数");
                }
                Assert.AreEqual(1, CountLayer(4), "种子 " + seed + " 第 4 层节点数");
            }
        }

        [Test]
        public void Generate_AllFixedSeeds_LayerCompositionMatchesConfig()
        {
            foreach (int seed in FixedSeeds)
            {
                GenerateMap(seed);

                // 第 1 层：普通战斗、事件、营地各 1
                AssertComposition(seed, 1, NodeType.Combat, NodeType.Event, NodeType.Camp);
                // 第 2 层：普通战斗、事件、精英各 1
                AssertComposition(seed, 2, NodeType.Combat, NodeType.Event, NodeType.Elite);
                // 第 3 层：普通战斗、事件、营地各 1
                AssertComposition(seed, 3, NodeType.Combat, NodeType.Event, NodeType.Camp);
                // 第 4 层：首领 1
                AssertComposition(seed, 4, NodeType.Boss);
            }
        }

        [Test]
        public void Generate_AllFixedSeeds_EnemyAndEventPoolsResolvable()
        {
            foreach (int seed in FixedSeeds)
            {
                GenerateMap(seed);
                foreach (var node in RegionMap.Nodes)
                {
                    if (node.EnemyPoolIds != null)
                    {
                        foreach (var id in node.EnemyPoolIds)
                        {
                            // 草原敌人池只允许 EN01–EN05（配置表 §5/§9）
                            Assert.IsTrue(IsPlainsEnemyId(id),
                                "种子 " + seed + " 节点 " + node.Id + " 敌人池引用不存在或不属于草原：" + id);
                        }
                    }

                    if (node.EventPoolIds != null)
                    {
                        foreach (var id in node.EventPoolIds)
                        {
                            Assert.IsTrue(id.StartsWith("E"), "种子 " + seed + " 事件池 ID 格式错误：" + id);
                        }
                    }
                }
            }
        }

        // ---- 连通性（验收：10 个固定种子均有起点到首领的路径）----

        [Test]
        public void Generate_AllFixedSeeds_ReachableFromStartToBoss()
        {
            foreach (int seed in FixedSeeds)
            {
                GenerateMap(seed);
                Assert.IsTrue(HasPathToBoss(), "种子 " + seed + " 不存在从起点到首领的路径");
            }
        }

        [Test]
        public void Generate_AllFixedSeeds_EveryNodeHasAtLeastOneNextConnection()
        {
            foreach (int seed in FixedSeeds)
            {
                GenerateMap(seed);
                for (int i = 0; i < RegionMap.Nodes.Count; i++)
                {
                    var node = RegionMap.Nodes[i];
                    if (node.Layer == RegionMap.LayerCount) continue; // 首领层无需出边
                    Assert.GreaterOrEqual(node.NextIndexes.Count, 1,
                        "种子 " + seed + " 节点 " + node.Id + " 没有指向下一层的连接");
                    foreach (int next in node.NextIndexes)
                    {
                        Assert.AreEqual(node.Layer + 1, RegionMap.Nodes[next].Layer,
                            "种子 " + seed + " 节点 " + node.Id + " 存在跨层或回退连接");
                    }
                }
            }
        }

        [Test]
        public void Generate_AllFixedSeeds_ThirdLayerAllConnectToBoss()
        {
            foreach (int seed in FixedSeeds)
            {
                GenerateMap(seed);
                int bossIndex = -1;
                for (int i = 0; i < RegionMap.Nodes.Count; i++)
                {
                    if (RegionMap.Nodes[i].Type == NodeType.Boss) bossIndex = i;
                }
                Assert.GreaterOrEqual(bossIndex, 0);

                for (int i = 0; i < RegionMap.Nodes.Count; i++)
                {
                    if (RegionMap.Nodes[i].Layer != 3) continue;
                    Assert.IsTrue(RegionMap.Nodes[i].NextIndexes.Contains(bossIndex),
                        "种子 " + seed + " 第 3 层节点 " + RegionMap.Nodes[i].Id + " 未连接首领");
                }
            }
        }

        [Test]
        public void Generate_SameSeed_ProducesIdenticalMap()
        {
            GenerateMap(12345);
            var snapshotA = SnapshotMap();

            RegionMap.Clear();
            GenerateMap(12345);
            var snapshotB = SnapshotMap();

            Assert.AreEqual(snapshotA.Count, snapshotB.Count);
            for (int i = 0; i < snapshotA.Count; i++)
            {
                Assert.AreEqual(snapshotA[i], snapshotB[i], "节点 " + i + " 不一致");
            }
        }

        // ---- 移动限制（验收：未连接/已访问/跨层被拒绝）----

        [Test]
        public void Move_FromStart_OnlyLayerOneAllowed()
        {
            // 每次尝试都从起点重新生成，避免上一次移动改变当前位置
            for (int i = 0; i < RegionMap.Nodes.Count; i++)
            {
                GenerateMap(1);
                bool ok = RegionMap.TryMoveTo(i, out string reason);
                if (RegionMap.Nodes[i].Layer == 1)
                {
                    Assert.IsTrue(ok, "起点应可移动到第 1 层节点 " + i);
                }
                else
                {
                    Assert.IsFalse(ok, "起点不应可移动到第 " + RegionMap.Nodes[i].Layer + " 层节点 " + i);
                    Assert.IsNotEmpty(reason);
                }
            }
        }

        [Test]
        public void Move_UnconnectedNode_RejectedAndStateUnchanged()
        {
            GenerateMap(1);
            // 移到第 1 层任意节点
            int first = RegionMap.ReachableNext()[0];
            Assert.IsTrue(RegionMap.TryMoveTo(first, out _));

            int before = RegionMap.CurrentNodeIndex;
            var pathBefore = new List<int>(RegionMap.Path);

            for (int i = 0; i < RegionMap.Nodes.Count; i++)
            {
                var node = RegionMap.Nodes[i];
                if (node.Layer != 2) continue;
                if (RegionMap.Nodes[first].NextIndexes.Contains(i)) continue; // 只测未连接的

                Assert.IsFalse(RegionMap.TryMoveTo(i, out string reason), "未连接节点不应可移动");
                Assert.IsNotEmpty(reason);
                Assert.AreEqual(before, RegionMap.CurrentNodeIndex, "移动被拒绝后当前位置不应变化");
                CollectionAssert.AreEqual(pathBefore, RegionMap.Path, "移动被拒绝后路径不应变化");
            }
        }

        [Test]
        public void Move_VisitedNode_Rejected()
        {
            GenerateMap(1);
            int first = RegionMap.ReachableNext()[0];
            Assert.IsTrue(RegionMap.TryMoveTo(first, out _));
            Assert.IsFalse(RegionMap.TryMoveTo(first, out string reason), "已访问节点不应可重复进入");
            Assert.IsNotEmpty(reason);
        }

        [Test]
        public void Move_CrossLayer_Rejected()
        {
            GenerateMap(1);
            // 从起点直接尝试第 2 层（跨层）
            int layer2 = -1;
            for (int i = 0; i < RegionMap.Nodes.Count; i++)
            {
                if (RegionMap.Nodes[i].Layer == 2) { layer2 = i; break; }
            }
            Assert.IsFalse(RegionMap.TryMoveTo(layer2, out string reason));
            Assert.IsNotEmpty(reason);
        }

        [Test]
        public void Move_ValidConnectedNode_SucceedsAndRecordsPath()
        {
            GenerateMap(42);
            int first = RegionMap.ReachableNext()[0];
            Assert.IsTrue(RegionMap.TryMoveTo(first, out _));
            Assert.AreEqual(first, RegionMap.CurrentNodeIndex);
            Assert.AreEqual(1, RegionMap.Path.Count);
            Assert.AreEqual(RegionMap.LayerCount - 1, RegionMap.RemainingLayers);

            // 走到下一层中已连接的节点
            var next = RegionMap.Nodes[first].NextIndexes;
            Assert.GreaterOrEqual(next.Count, 1);
            Assert.IsTrue(RegionMap.TryMoveTo(next[0], out _));
            Assert.AreEqual(2, RegionMap.Path.Count);
        }

        [Test]
        public void Move_FullPathToBoss_Succeeds()
        {
            GenerateMap(777);
            // 模拟完整路径：每层选一个可达节点走到首领
            for (int layer = 1; layer <= RegionMap.LayerCount; layer++)
            {
                var reachable = RegionMap.ReachableNext();
                Assert.GreaterOrEqual(reachable.Count, 1, "第 " + layer + " 层应始终有可达节点");
                Assert.IsTrue(RegionMap.TryMoveTo(reachable[0], out _), "第 " + layer + " 层移动应成功");
            }
            Assert.AreEqual(RegionMap.LayerCount, RegionMap.CurrentLayer);
            Assert.AreEqual(NodeType.Boss, RegionMap.Nodes[RegionMap.CurrentNodeIndex].Type);
            Assert.AreEqual(0, RegionMap.RemainingLayers);
        }

        // === 辅助 ===

        private static bool IsPlainsEnemyId(string id)
        {
            switch (id)
            {
                case "EN01": case "EN02": case "EN03": case "EN04": case "EN05":
                    return true;
                default:
                    return false;
            }
        }

        private static int CountLayer(int layer)
        {
            int count = 0;
            foreach (var node in RegionMap.Nodes)
            {
                if (node.Layer == layer) count++;
            }
            return count;
        }

        private static void AssertComposition(int seed, int layer, params NodeType[] expected)
        {
            var types = new List<NodeType>();
            foreach (var node in RegionMap.Nodes)
            {
                if (node.Layer == layer) types.Add(node.Type);
            }
            Assert.AreEqual(expected.Length, types.Count, "种子 " + seed + " 第 " + layer + " 层类型数");
            foreach (var t in expected)
            {
                Assert.IsTrue(types.Contains(t), "种子 " + seed + " 第 " + layer + " 层缺少 " + t);
            }
        }

        /// <summary>从起点（连接第 1 层全部）出发 BFS，判断首领是否可达。</summary>
        private static bool HasPathToBoss()
        {
            var visited = new bool[RegionMap.Nodes.Count];
            var queue = new Queue<int>();

            for (int i = 0; i < RegionMap.Nodes.Count; i++)
            {
                if (RegionMap.Nodes[i].Layer == 1)
                {
                    visited[i] = true;
                    queue.Enqueue(i);
                }
            }

            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                if (RegionMap.Nodes[cur].Type == NodeType.Boss) return true;
                foreach (int next in RegionMap.Nodes[cur].NextIndexes)
                {
                    if (visited[next]) continue;
                    visited[next] = true;
                    queue.Enqueue(next);
                }
            }

            return false;
        }

        private static List<string> SnapshotMap()
        {
            var list = new List<string>();
            foreach (var node in RegionMap.Nodes)
            {
                list.Add(node.Id + "|" + node.Layer + "|" + node.Type + "|" + string.Join(",", node.NextIndexes));
            }
            return list;
        }
    }
}

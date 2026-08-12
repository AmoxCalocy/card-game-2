using System.Collections.Generic;

namespace OneJourney.Core
{
    /// <summary>地图节点（配置表 §9）：一层内的一个可选位置。</summary>
    public class RegionMapNode
    {
        public string Id;
        public int Layer;              // 1-4
        public NodeType Type;
        public string DisplayName;
        public string[] EnemyPoolIds;  // 战斗/精英/首领节点的敌人池（可空）
        public string[] EventPoolIds;  // 事件节点的事件池（可空）
        public readonly List<int> NextIndexes = new List<int>(); // 连接的下一层节点索引

        public static string NodeTypeName(NodeType type)
        {
            switch (type)
            {
                case NodeType.Combat: return "战斗";
                case NodeType.Event: return "事件";
                case NodeType.Camp: return "营地";
                case NodeType.Elite: return "精英";
                case NodeType.Boss: return "首领";
                default: return type.ToString();
            }
        }
    }

    /// <summary>
    /// 区域节点地图（A2-17，配置表 §9）。
    /// 固定 4 层；起点连接第一层所有节点；第 1-3 层每个节点至少连接下一层 1 个节点；
    /// 第 3 层所有节点连接第 4 层首领；不存在回退连接。
    /// 层内节点顺序与连接由随机种子决定。
    /// </summary>
    public static class RegionMap
    {
        public const int LayerCount = 4;

        public static IReadOnlyList<RegionMapNode> Nodes => _nodes;
        public static bool IsGenerated { get; private set; }
        public static ContentRegion Region { get; private set; } = ContentRegion.None;

        /// <summary>当前位置索引；-1 表示起点（尚未进入任何节点）。</summary>
        public static int CurrentNodeIndex { get; private set; } = -1;

        public static IReadOnlyList<int> VisitedIndexes => _visited;
        public static IReadOnlyList<int> Path => _path;

        public static bool IsVisited(int nodeIndex)
        {
            return _visited.Contains(nodeIndex);
        }

        /// <summary>当前层：起点为 0，进入节点后为节点所在层（1-4）。</summary>
        public static int CurrentLayer => CurrentNodeIndex < 0 ? 0 : _nodes[CurrentNodeIndex].Layer;

        /// <summary>剩余层数（含当前层向后的完整层数；起点时为 4）。</summary>
        public static int RemainingLayers => LayerCount - CurrentLayer;

        private static readonly List<RegionMapNode> _nodes = new List<RegionMapNode>();
        private static readonly List<int> _visited = new List<int>();
        private static readonly List<int> _path = new List<int>();

        /// <summary>生成指定区域的地图（当前实现草原）。失败时返回 false 并记录原因。</summary>
        public static bool Generate(ContentRegion region, GameRandom rng)
        {
            Clear();

            if (region != ContentRegion.Plains)
            {
                RunRecord.Log(RecordCategory.General, "地图生成失败：暂仅支持草原区域（" + region + "）");
                return false;
            }

            Region = region;
            var r = rng;

            // 第 1 层：普通战斗、事件、营地各 1
            var layer1 = new List<RegionMapNode>
            {
                MakeCombatNode(1, new[] { "EN01", "EN02", "EN04" }),
                MakeEventNode(1, PlainsEventPool()),
                MakeCampNode(1)
            };

            // 第 2 层：普通战斗、事件、精英各 1
            var layer2 = new List<RegionMapNode>
            {
                MakeCombatNode(2, new[] { "EN01", "EN02", "EN04" }),
                MakeEventNode(2, PlainsEventPool()),
                MakeEliteNode(2, new[] { "EN03" })
            };

            // 第 3 层：普通战斗、事件、营地各 1
            var layer3 = new List<RegionMapNode>
            {
                MakeCombatNode(3, new[] { "EN01", "EN02", "EN04" }),
                MakeEventNode(3, PlainsEventPool()),
                MakeCampNode(3)
            };

            // 第 4 层：草原首领
            var layer4 = new List<RegionMapNode>
            {
                MakeBossNode(4, new[] { "EN05" })
            };

            // 层内顺序由种子决定
            r.Shuffle(layer1);
            r.Shuffle(layer2);
            r.Shuffle(layer3);

            var layers = new[] { layer1, layer2, layer3, layer4 };
            foreach (var layer in layers)
            {
                foreach (var node in layer)
                {
                    node.Id = "M" + (_nodes.Count + 1);
                    _nodes.Add(node);
                }
            }

            // 层间连接：第 1-3 层每个节点至少连 1 个下一层节点；下一层每个节点至少 1 条入边
            for (int i = 0; i < 3; i++)
            {
                ConnectLayers(layers[i], layers[i + 1], r);
            }

            IsGenerated = true;
            return true;
        }

        /// <summary>
        /// 尝试移动到目标节点。校验：已生成、目标为下一层、与当前位置相连（起点连接第一层全部）、未访问。
        /// 成功时更新位置并写入本局记录（地图分支）；失败时通过 reason 说明原因且状态不变。
        /// </summary>
        public static bool TryMoveTo(int nodeIndex, out string reason)
        {
            reason = null;

            if (!IsGenerated)
            {
                reason = "地图尚未生成";
                return false;
            }

            if (nodeIndex < 0 || nodeIndex >= _nodes.Count)
            {
                reason = "节点不存在";
                return false;
            }

            var node = _nodes[nodeIndex];

            if (node.Layer != CurrentLayer + 1)
            {
                reason = "只能移动到下一层节点（目标在第 " + node.Layer + " 层，当前在第 " + CurrentLayer + " 层）";
                return false;
            }

            if (CurrentNodeIndex >= 0)
            {
                var current = _nodes[CurrentNodeIndex];
                if (!current.NextIndexes.Contains(nodeIndex))
                {
                    reason = "目标节点与当前位置不相连";
                    return false;
                }
            }
            // 起点（CurrentNodeIndex == -1）连接第一层所有节点，层校验已覆盖

            if (_visited.Contains(nodeIndex))
            {
                reason = "该节点已访问，不能重复进入";
                return false;
            }

            _visited.Add(nodeIndex);
            _path.Add(nodeIndex);
            CurrentNodeIndex = nodeIndex;

            RunRecord.Log(RecordCategory.MapBranch,
                "移动到 " + node.DisplayName + "（第 " + node.Layer + " 层 / " + RegionMapNode.NodeTypeName(node.Type) + "）");
            return true;
        }

        /// <summary>当前可移动到的节点索引列表（用于界面高亮）。</summary>
        public static List<int> ReachableNext()
        {
            var result = new List<int>();
            if (!IsGenerated) return result;

            for (int i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                if (node.Layer != CurrentLayer + 1) continue;
                if (_visited.Contains(i)) continue;
                if (CurrentNodeIndex >= 0 && !_nodes[CurrentNodeIndex].NextIndexes.Contains(i)) continue;
                result.Add(i);
            }

            return result;
        }

        public static void Clear()
        {
            _nodes.Clear();
            _visited.Clear();
            _path.Clear();
            CurrentNodeIndex = -1;
            IsGenerated = false;
            Region = ContentRegion.None;
        }

        // === 内部 ===

        private static string[] PlainsEventPool()
        {
            return new[] { "E01", "E02", "E03", "E04", "E05", "E06", "E07", "E08", "E09", "E10" };
        }

        private static RegionMapNode MakeCombatNode(int layer, string[] enemies)
        {
            return new RegionMapNode { Layer = layer, Type = NodeType.Combat, DisplayName = "战斗", EnemyPoolIds = enemies };
        }

        private static RegionMapNode MakeEventNode(int layer, string[] events)
        {
            return new RegionMapNode { Layer = layer, Type = NodeType.Event, DisplayName = "事件", EventPoolIds = events };
        }

        private static RegionMapNode MakeCampNode(int layer)
        {
            return new RegionMapNode { Layer = layer, Type = NodeType.Camp, DisplayName = "营地" };
        }

        private static RegionMapNode MakeEliteNode(int layer, string[] enemies)
        {
            return new RegionMapNode { Layer = layer, Type = NodeType.Elite, DisplayName = "精英", EnemyPoolIds = enemies };
        }

        private static RegionMapNode MakeBossNode(int layer, string[] enemies)
        {
            return new RegionMapNode { Layer = layer, Type = NodeType.Boss, DisplayName = "首领", EnemyPoolIds = enemies };
        }

        /// <summary>连接上下两层：下层每个节点至少 1 条入边，上层每个节点至少 1 条出边。</summary>
        private static void ConnectLayers(List<RegionMapNode> upper, List<RegionMapNode> lower, GameRandom rng)
        {
            // 1) 为下层每个节点随机分配一个上层节点作入边（保证下层可达）
            foreach (var low in lower)
            {
                var up = upper[rng.Next(upper.Count)];
                up.NextIndexes.Add(Index(low));
            }

            // 2) 上层每个节点若还没有出边，随机补一条（保证上层出边）
            foreach (var up in upper)
            {
                if (up.NextIndexes.Count == 0)
                {
                    var low = lower[rng.Next(lower.Count)];
                    up.NextIndexes.Add(Index(low));
                }
            }

            // 3) 随机补充：上层每个节点以 50% 概率再多连一个未连接的下层节点（增加分支）
            foreach (var up in upper)
            {
                if (rng.Next(2) == 0)
                {
                    var low = lower[rng.Next(lower.Count)];
                    if (!up.NextIndexes.Contains(Index(low)))
                    {
                        up.NextIndexes.Add(Index(low));
                    }
                }
            }
        }

        private static int Index(RegionMapNode node)
        {
            return _nodes.IndexOf(node);
        }
    }
}

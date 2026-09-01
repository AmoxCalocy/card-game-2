using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OneJourney.Core
{
    public sealed class MapPageView : MonoBehaviour
    {
        [SerializeField] private Image _regionArtwork;
        [SerializeField] private TMP_Text _regionInitial;
        [SerializeField] private TMP_Text _regionTitleText;
        [SerializeField] private TMP_Text _progressText;
        [SerializeField] private TMP_Text _resourceText;
        [SerializeField] private TMP_Text _riskText;
        [SerializeField] private RectTransform _connectionLayer;
        [SerializeField] private RectTransform _nodeLayer;
        [SerializeField] private MapNodeView _nodePrefab;

        private readonly List<GameObject> _dynamicObjects = new List<GameObject>();
        private readonly Dictionary<int, MapNodeView> _nodeViews = new Dictionary<int, MapNodeView>();
        private IReadOnlyList<RegionMapNode> _nodes;
        private UnityAction<int> _onNodeConfirmed;
        private int _selectedIndex = -1;

        public void SetMap(ContentRegion region, IReadOnlyList<RegionMapNode> nodes, IReadOnlyList<int> path,
            int currentNodeIndex, IReadOnlyList<int> visitedIndexes, IReadOnlyList<int> reachableIndexes,
            string resources, string riskHint, UnityAction<int> onNodeConfirmed)
        {
            ClearMap();

            _nodes = nodes;
            _onNodeConfirmed = onNodeConfirmed;
            _selectedIndex = -1;

            bool jungle = region == ContentRegion.Jungle;
            string regionName = jungle ? "密林" : "草原";
            _regionTitleText.text = regionName + "远征地图";
            _regionInitial.text = jungle ? "林" : "原";
            _regionArtwork.color = jungle
                ? new Color(0.13f, 0.28f, 0.21f, 1f)
                : new Color(0.25f, 0.31f, 0.20f, 1f);
            _progressText.text = currentNodeIndex < 0
                ? "尚未出发 · 共 " + RegionMap.LayerCount + " 层"
                : "已抵达第 " + nodes[currentNodeIndex].Layer + " 层 · 剩余 " + RegionMap.RemainingLayers + " 层";
            _resourceText.text = resources;
            _riskText.text = riskHint;
            _riskText.color = RunSession.AmbushPending
                ? new Color(0.95f, 0.38f, 0.28f, 1f)
                : new Color(0.95f, 0.69f, 0.30f, 1f);

            CreateConnections(nodes, path, currentNodeIndex, reachableIndexes);
            CreateStartNode(currentNodeIndex);

            for (int i = 0; i < nodes.Count; i++)
            {
                MapNodeVisualState state;
                if (currentNodeIndex == i) state = MapNodeVisualState.Current;
                else if (Contains(visitedIndexes, i)) state = MapNodeVisualState.Visited;
                else if (Contains(reachableIndexes, i)) state = MapNodeVisualState.Reachable;
                else state = MapNodeVisualState.Future;

                CreateNode(i, NodePosition(nodes, i), NodeIcon(nodes[i].Type), nodes[i].DisplayName,
                    "第 " + nodes[i].Layer + " 层", state);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
        }

        private void CreateConnections(IReadOnlyList<RegionMapNode> nodes, IReadOnlyList<int> path,
            int currentNodeIndex, IReadOnlyList<int> reachableIndexes)
        {
            Vector2 start = StartPosition();
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Layer != 1) continue;
                DrawConnection("Start_" + i, start, NodePosition(nodes, i),
                    ConnectionColor(-1, i, path, currentNodeIndex, reachableIndexes),
                    ConnectionThickness(-1, i, path, currentNodeIndex, reachableIndexes));
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                Vector2 from = NodePosition(nodes, i);
                for (int j = 0; j < nodes[i].NextIndexes.Count; j++)
                {
                    int targetIndex = nodes[i].NextIndexes[j];
                    DrawConnection(i + "_" + targetIndex, from, NodePosition(nodes, targetIndex),
                        ConnectionColor(i, targetIndex, path, currentNodeIndex, reachableIndexes),
                        ConnectionThickness(i, targetIndex, path, currentNodeIndex, reachableIndexes));
                }
            }
        }

        private void CreateStartNode(int currentNodeIndex)
        {
            MapNodeVisualState state = currentNodeIndex < 0
                ? MapNodeVisualState.Current
                : MapNodeVisualState.Visited;
            CreateNode(-1, StartPosition(), "始", "出发点", "旅程起点", state);
        }

        private void CreateNode(int index, Vector2 position, string icon, string title, string layer,
            MapNodeVisualState state)
        {
            var view = Instantiate(_nodePrefab, _nodeLayer);
            view.name = index < 0 ? "MapStart" : "MapNode_" + index;
            view.RectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            view.RectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            view.RectTransform.pivot = new Vector2(0.5f, 0.5f);
            view.RectTransform.anchoredPosition = position;
            view.SetContent(icon, title, layer, state);
            _dynamicObjects.Add(view.gameObject);

            if (index < 0) return;
            _nodeViews[index] = view;
            if (state == MapNodeVisualState.Reachable)
            {
                int captured = index;
                view.Button.onClick.AddListener(() => OnNodePressed(captured));
            }
        }

        private void OnNodePressed(int nodeIndex)
        {
            if (_selectedIndex == nodeIndex)
            {
                _onNodeConfirmed?.Invoke(nodeIndex);
                return;
            }

            if (_selectedIndex >= 0 && _nodeViews.TryGetValue(_selectedIndex, out MapNodeView previous))
                previous.SetSelected(false);

            _selectedIndex = nodeIndex;
            MapNodeView view = _nodeViews[nodeIndex];
            view.SetSelected(true);
        }

        private void DrawConnection(string name, Vector2 from, Vector2 to, Color color, float thickness)
        {
            var go = new GameObject("MapRoute_" + name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_connectionLayer, false);

            var rect = (RectTransform)go.transform;
            Vector2 delta = to - from;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = (from + to) * 0.5f;
            rect.sizeDelta = new Vector2(delta.magnitude, thickness);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            _dynamicObjects.Add(go);
        }

        private void ClearMap()
        {
            for (int i = 0; i < _dynamicObjects.Count; i++)
            {
                if (_dynamicObjects[i] == null) continue;
                _dynamicObjects[i].SetActive(false);
                Destroy(_dynamicObjects[i]);
            }

            _dynamicObjects.Clear();
            _nodeViews.Clear();
        }

        private static Vector2 StartPosition()
        {
            return new Vector2(0f, -235f);
        }

        private static Vector2 NodePosition(IReadOnlyList<RegionMapNode> nodes, int nodeIndex)
        {
            int layer = nodes[nodeIndex].Layer;
            int count = 0;
            int position = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Layer != layer) continue;
                if (i == nodeIndex) position = count;
                count++;
            }

            float x = (position - (count - 1) * 0.5f) * 420f;
            float y = -140f + (layer - 1) * 125f;
            return new Vector2(x, y);
        }

        private static Color ConnectionColor(int fromIndex, int toIndex, IReadOnlyList<int> path,
            int currentNodeIndex, IReadOnlyList<int> reachableIndexes)
        {
            if (IsCurrentRoute(fromIndex, toIndex, currentNodeIndex, reachableIndexes))
                return new Color(0.95f, 0.68f, 0.24f, 0.95f);
            if (IsPathRoute(fromIndex, toIndex, path))
                return new Color(0.62f, 0.52f, 0.28f, 0.92f);
            return new Color(0.22f, 0.25f, 0.30f, 0.72f);
        }

        private static float ConnectionThickness(int fromIndex, int toIndex, IReadOnlyList<int> path,
            int currentNodeIndex, IReadOnlyList<int> reachableIndexes)
        {
            if (IsCurrentRoute(fromIndex, toIndex, currentNodeIndex, reachableIndexes)) return 8f;
            if (IsPathRoute(fromIndex, toIndex, path)) return 6f;
            return 4f;
        }

        private static bool IsCurrentRoute(int fromIndex, int toIndex, int currentNodeIndex,
            IReadOnlyList<int> reachableIndexes)
        {
            bool startsHere = currentNodeIndex < 0 ? fromIndex < 0 : fromIndex == currentNodeIndex;
            return startsHere && Contains(reachableIndexes, toIndex);
        }

        private static bool IsPathRoute(int fromIndex, int toIndex, IReadOnlyList<int> path)
        {
            if (path.Count == 0) return false;
            if (fromIndex < 0) return path[0] == toIndex;

            for (int i = 0; i < path.Count - 1; i++)
            {
                if (path[i] == fromIndex && path[i + 1] == toIndex) return true;
            }

            return false;
        }

        private static bool Contains(IReadOnlyList<int> values, int value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == value) return true;
            }

            return false;
        }

        private static string NodeIcon(NodeType type)
        {
            switch (type)
            {
                case NodeType.Combat: return "战";
                case NodeType.Event: return "事";
                case NodeType.Camp: return "营";
                case NodeType.Elite: return "精";
                case NodeType.Boss: return "首";
                default: return "?";
            }
        }

    }
}

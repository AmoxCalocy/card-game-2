using System.Collections.Generic;

namespace OneJourney.Core
{
    /// <summary>本局记录分类（实施计划 A0-5）。</summary>
    public enum RecordCategory
    {
        General = 0,
        Draw = 1,
        EnemyIntent = 2,
        MapBranch = 3,
        EventChoice = 4,
        RewardChoice = 5
    }

    /// <summary>一条有序记录条目。</summary>
    public struct RunRecordEntry
    {
        public readonly int Index;
        public readonly RecordCategory Category;
        public readonly string Detail;

        public RunRecordEntry(int index, RecordCategory category, string detail)
        {
            Index = index;
            Category = category;
            Detail = detail ?? string.Empty;
        }

        public override string ToString()
        {
            return "#" + Index + " [" + CategoryName(Category) + "] " + Detail;
        }

        public static string CategoryName(RecordCategory cat)
        {
            switch (cat)
            {
                case RecordCategory.Draw:
                    return "抽牌";
                case RecordCategory.EnemyIntent:
                    return "敌人意图";
                case RecordCategory.MapBranch:
                    return "地图分支";
                case RecordCategory.EventChoice:
                    return "事件选项";
                case RecordCategory.RewardChoice:
                    return "奖励选择";
                default:
                    return "一般";
            }
        }
    }

    /// <summary>
    /// 本局详细记录（实施计划 A0-5）：按发生顺序写入抽牌、敌人意图、地图分支、
    /// 事件选项结果与奖励选择，作为可复现验证的唯一依据。
    /// </summary>
    public static class RunRecord
    {
        private const int MaxEntries = 200;
        private static readonly List<RunRecordEntry> EntriesList = new List<RunRecordEntry>();

        public static IReadOnlyList<RunRecordEntry> Entries => EntriesList;

        public static int Count => EntriesList.Count;

        public static void Log(RecordCategory category, string detail)
        {
            var entry = new RunRecordEntry(EntriesList.Count, category, detail);
            EntriesList.Add(entry);

            if (EntriesList.Count > MaxEntries)
            {
                EntriesList.RemoveAt(0);
                // 重新编号，保持 index 连续
                for (int i = 0; i < EntriesList.Count; i++)
                {
                    EntriesList[i] = new RunRecordEntry(i, EntriesList[i].Category, EntriesList[i].Detail);
                }
            }
        }

        public static void Clear()
        {
            EntriesList.Clear();
        }
    }
}

using System.Collections.Generic;

namespace OneJourney.Core
{
    /// <summary>
    /// MVP 内容目录：内容稳定标识的唯一代码来源（与《MVP 配置表》design/mvp-configuration-tables.md 保持一致）。
    /// 内容 ID 同时作为测试用例编号（TC-&lt;ID&gt;）。
    /// </summary>
    public static class ContentCatalog
    {
        public const int CardCount = 40;
        public const int CompanionCount = 8;
        public const int EnemyCount = 10;
        public const int EventCount = 20;
        public const int RelicCount = 8;
        public const int BuildingCount = 5;

        public static readonly string[] CardIds =
        {
            "C01", "C02", "C03", "C04", "C05", "C06", "C07", "C08", "C09", "C10",
            "C11", "C12", "C13", "C14", "C15", "C16", "C17", "C18", "C19", "C20",
            "C21", "C22", "C23", "C24", "C25", "C26", "C27", "C28", "C29", "C30",
            "C31", "C32", "C33", "C34", "C35", "C36", "C37", "C38", "C39", "C40"
        };

        public static readonly string[] CompanionIds =
        {
            "P01", "P02", "P03", "P04", "P05", "P06", "P07", "P08"
        };

        public static readonly string[] EnemyIds =
        {
            "EN01", "EN02", "EN03", "EN04", "EN05", "EN06", "EN07", "EN08", "EN09", "EN10"
        };

        public static readonly string[] EventIds =
        {
            "E01", "E02", "E03", "E04", "E05", "E06", "E07", "E08", "E09", "E10",
            "E11", "E12", "E13", "E14", "E15", "E16", "E17", "E18", "E19", "E20"
        };

        public static readonly string[] RelicIds =
        {
            "R01", "R02", "R03", "R04", "R05", "R06", "R07", "R08"
        };

        public static readonly string[] BuildingIds =
        {
            "B01", "B02", "B03", "B04", "B05"
        };

        /// <summary>伙伴专属加入卡（配置表 §4）：加入牌组一次的卡。</summary>
        public static readonly KeyValuePair<string, string>[] CompanionJoinCards =
        {
            new KeyValuePair<string, string>("P01", "C12"),
            new KeyValuePair<string, string>("P02", "C34"),
            new KeyValuePair<string, string>("P03", "C17"),
            new KeyValuePair<string, string>("P04", "C35"),
            new KeyValuePair<string, string>("P05", "C21"),
            new KeyValuePair<string, string>("P06", "C27"),
            new KeyValuePair<string, string>("P07", "C23"),
            new KeyValuePair<string, string>("P08", "C26")
        };

        /// <summary>城镇建筑解锁加入奖励池的卡牌（配置表 §8）。</summary>
        public static readonly KeyValuePair<string, string[]>[] BuildingUnlockCards =
        {
            new KeyValuePair<string, string[]>("B03", new[] { "C04", "C11" }),
            new KeyValuePair<string, string[]>("B04", new[] { "C34", "C37", "C40" })
        };

        /// <summary>事件引用的卡牌（配置表 §6，直接获得型）。</summary>
        public static readonly KeyValuePair<string, string>[] EventGrantCards =
        {
            new KeyValuePair<string, string>("E07", "C04"),
            new KeyValuePair<string, string>("E09", "C19"),
            new KeyValuePair<string, string>("E11", "C34"),
            new KeyValuePair<string, string>("E16", "C23"),
            new KeyValuePair<string, string>("E19", "C37"),
            new KeyValuePair<string, string>("E20", "C27")
        };

        /// <summary>事件引用的敌人（配置表 §6，战斗型）。</summary>
        public static readonly KeyValuePair<string, string[]>[] EventCombatEnemies =
        {
            new KeyValuePair<string, string[]>("E03", new[] { "EN01", "EN02" }),
            new KeyValuePair<string, string[]>("E12", new[] { "EN07" }),
            new KeyValuePair<string, string[]>("E13", new[] { "EN08" }),
            new KeyValuePair<string, string[]>("E18", new[] { "EN08" }),
            new KeyValuePair<string, string[]>("E20", new[] { "EN06", "EN09" })
        };
    }
}

namespace OneJourney.Core
{
    /// <summary>
    /// 第一版起始参数（《MVP 配置表》§2 落地）。
    /// 任何数值调整必须同步更新配置表、本文档与对应测试预期。
    /// </summary>
    public static class GameStartParameters
    {
        // 队伍与战斗（配置表 §2.1）
        public const int MaxPartySize = 4; // 主角 + 最多 3 名上阵伙伴
        public const int BaseEnergy = 3;
        public const int InitialHandSize = 3;
        public const int CardsPerTurn = 1;
        public const int MaxHandSize = 5;
        public const int MinDeckSize = 10;
        public const int MaxDeckSize = 30;

        // 起始资源（配置表 §2.4）
        public const int StartFood = 14;
        public const int StartWealth = 30;
        public const int StartReputation = 0;
        public const int StartBuildingMaterials = 0;

        // 资源边界（配置表 §2.4）
        public const int MaxFood = 30;
        public const int MaxWealth = 999;
        public const int MaxReputation = 100;
        public const int MaxBuildingMaterials = 99;

        // 移动与粮食不足惩罚（配置表 §2.4）
        public const int GrasslandMoveFoodCost = 1;
        public const int ForestMoveFoodCost = 2;
        public const int StarvationFatigueGain = 1; // 粮食不足时主角获得 1 层疲劳
        public const int StarvationRiskGain = 2; // 粮食不足时风险 +2

        // 风险（配置表 §2.4）
        public const int RiskThreshold = 10;
        public const int RiskAfterCrisis = 5;

        // 垂直切片目标：击败密林首领（配置表 §5）
        public const string VerticalSliceBossEnemyId = "EN10";

        /// <summary>初始牌组（配置表 §2.1）：C01×4、C09×3、C17×1、C33×1、C36×1，共 10 张。</summary>
        public static readonly string[] StartingDeck =
        {
            "C01", "C01", "C01", "C01",
            "C09", "C09", "C09",
            "C17",
            "C33",
            "C36"
        };
    }
}

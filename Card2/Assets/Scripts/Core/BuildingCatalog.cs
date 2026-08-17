namespace OneJourney.Core
{
    /// <summary>建筑归属：营地建筑（B01-B02）或城镇建筑（B03-B05，草原首领击败后解锁）。</summary>
    public enum BuildingType
    {
        Camp,
        Town
    }

    /// <summary>一阶建筑定义（MVP 配置表 §8）。</summary>
    public class BuildingDef
    {
        public string Id;
        public string DisplayName;
        public BuildingType Type;
        public int CostWealth;
        public int CostMaterial;
        public int CostReputation;
        public bool RequiresBossDefeated; // 城镇建筑前置：草原首领已击败
        public string EffectText;
    }

    /// <summary>5 座 MVP 一阶建筑静态目录（配置表 §8）：成本/前置/效果在此集中。</summary>
    public static class BuildingCatalog
    {
        public static readonly BuildingDef[] All =
        {
            new BuildingDef
            {
                Id = "B01", DisplayName = "储粮帐篷", Type = BuildingType.Camp,
                CostMaterial = 3,
                EffectText = "每个区域首次进入营地时，粮食额外 +4"
            },
            new BuildingDef
            {
                Id = "B02", DisplayName = "野战医棚", Type = BuildingType.Camp,
                CostWealth = 20, CostMaterial = 3,
                EffectText = "营地提供救治：选择 1 名存活单位移除受伤或 1 层疾病（MVP：移除 1 层疾病）"
            },
            new BuildingDef
            {
                Id = "B03", DisplayName = "铁匠铺", Type = BuildingType.Town,
                CostWealth = 30, CostMaterial = 5, CostReputation = 5, RequiresBossDefeated = true,
                EffectText = "C04、C11 加入所有后续战斗奖励池；首次建成时可免费升级 1 张卡"
            },
            new BuildingDef
            {
                Id = "B04", DisplayName = "医馆", Type = BuildingType.Town,
                CostWealth = 25, CostMaterial = 4, CostReputation = 5, RequiresBossDefeated = true,
                EffectText = "城镇提供医馆服务：选择 1 名存活单位移除 1 层疾病或疲劳；C34、C37、C40 加入后续奖励池"
            },
            new BuildingDef
            {
                Id = "B05", DisplayName = "市集", Type = BuildingType.Town,
                CostWealth = 30, CostMaterial = 5, CostReputation = 5, RequiresBossDefeated = true,
                EffectText = "每个区域首次通过事件获得财富时，额外 +5 财富"
            }
        };

        public static BuildingDef Find(string id)
        {
            foreach (var b in All)
            {
                if (b.Id == id) return b;
            }

            return null;
        }
    }
}

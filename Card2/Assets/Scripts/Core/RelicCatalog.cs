namespace OneJourney.Core
{
    /// <summary>遗物定义（MVP 配置表 §7）。</summary>
    public class RelicDef
    {
        public string Id;
        public string DisplayName;
        public string EffectText;
        /// <summary>是否首领奖励专属（R08）；false 表示精英与首领奖励池均可出现。</summary>
        public bool BossOnly;
    }

    /// <summary>8 件 MVP 遗物静态目录（配置表 §7）：效果文本集中于此，触发逻辑在各接入点。</summary>
    public static class RelicCatalog
    {
        public static readonly RelicDef[] All =
        {
            new RelicDef
            {
                Id = "R01", DisplayName = "旅人罗盘",
                EffectText = "进入地图时显示下一层全部节点类型；与 P03 不重复叠加（MVP 地图已显示全部节点，效果视为天然生效）"
            },
            new RelicDef
            {
                Id = "R02", DisplayName = "铁锅",
                EffectText = "每个区域第一次进入营地节点时，粮食 +4"
            },
            new RelicDef
            {
                Id = "R03", DisplayName = "琥珀护符",
                EffectText = "每场战斗开始时，所有存活上阵单位获得 3 护甲"
            },
            new RelicDef
            {
                Id = "R04", DisplayName = "医师药箱",
                EffectText = "每个区域第一次进入营地时，选择 1 名存活单位移除 1 层疾病或疲劳"
            },
            new RelicDef
            {
                Id = "R05", DisplayName = "商队印记",
                EffectText = "每个区域首次通过事件获得财富时，额外获得 5 财富"
            },
            new RelicDef
            {
                Id = "R06", DisplayName = "狼牙坠饰",
                EffectText = "每场战斗中主角首次造成普通伤害时，该次伤害额外 +3"
            },
            new RelicDef
            {
                Id = "R07", DisplayName = "指挥旗",
                EffectText = "每场战斗中首次打出的战术卡费用 -1，最低为 0"
            },
            new RelicDef
            {
                Id = "R08", DisplayName = "不熄灯", BossOnly = true,
                EffectText = "每场首领战开始时，队伍获得 2 层士气"
            }
        };

        public static RelicDef Find(string id)
        {
            foreach (var r in All)
            {
                if (r.Id == id) return r;
            }

            return null;
        }
    }
}

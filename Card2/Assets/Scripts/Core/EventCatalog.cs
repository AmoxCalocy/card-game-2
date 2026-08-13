namespace OneJourney.Core
{
    /// <summary>事件选项的条件类型（配置表 §6）。</summary>
    public enum EventOptionCondition
    {
        None = 0,                     // 无条件
        PayResource = 1,              // 支付粮食/财富/声望（CostFood/CostWealth/CostReputation）
        HasPartnerAndReputation = 2,  // 已招募伙伴 且 声望 ≥ RequireReputation（E03 交涉）
        HasPartnerOrReputation = 3,   // 已招募伙伴 或 声望 ≥ RequireReputation（E12 破译）
        HasPartnerOrCard = 4,         // 已招募伙伴 或 牌组拥有卡（E19 共用药品）
        HasPartnerOrPartner = 5,      // 已招募伙伴 A 或 B（E20 追猎）
        ReputationAtLeast = 6,        // 声望 ≥ RequireReputation（E08 征募）
        HasRemoveableCard = 7,        // 牌组中存在可移除的非初始锁定卡（E06/E09/E17）
        HasPartner = 8                // 已招募指定伙伴（E13 反伏击：P03）
    }

    /// <summary>事件选项的状态移除类型。</summary>
    public enum EventStatusChoice
    {
        None = 0,
        FatigueSingle = 1,            // 选择 1 名存活单位移除 1 层疲劳（E10 休整）
        DiseaseAll = 2,               // 所有存活单位各移除 1 层疾病（E11 救治）
        DiseaseOrFatigueSingle = 3    // 选择 1 名存活单位移除 1 层疾病或疲劳（E14 配药）
    }

    /// <summary>单个事件选项（配置表 §6）：条件、支付、即时结果与可选后续。</summary>
    public class EventOptionDef
    {
        public string Label;
        public string ResultText;                 // 固定结果说明（界面显示）
        public EventOptionCondition Condition;
        public int CostFood, CostWealth, CostReputation;   // 支付（PayResource）
        public string RequirePartnerId;           // 条件伙伴（Has* 系列）
        public string RequirePartnerId2;          // 条件伙伴 B（HasPartnerOrPartner，可空）
        public string RequireCardId;              // 条件卡（HasPartnerOrCard）
        public int RequireReputation;             // 条件声望

        public int FoodDelta, WealthDelta, ReputationDelta, MaterialDelta, RiskDelta; // 即时结果

        public string RecruitPartnerId;           // 招募伙伴（已招募 → 忠诚 +10；死亡/未实现离队 → 禁用）
        public int RecruitLoyalty = -1;           // 招募时指定忠诚度（-1 = 默认 60）

        public string GrantCardId;                // 获得卡牌（加入战役牌组）
        public string GrantRelicId;               // 获得遗物（记录持有）

        public bool RemoveCard;                   // 需要选择移除一张卡（子选择）
        public bool UpgradeCard;                  // 需要选择一张卡升级（子选择，E07）
        public EventStatusChoice StatusChoice;    // 状态移除（子选择或全员）

        public string[] CombatEnemyIds;           // 触发战斗的敌人（可空）
        public string CombatLabel;                // 战斗描述
        public int VictoryBonusWealth, VictoryBonusMaterial, VictoryBonusReputation; // 战斗胜利额外奖励
        public string VictoryBonusCardId;         // 胜利获得卡（加入牌组）
        public string VictoryBonusRelicId;        // 胜利获得遗物
        public string VictoryBonusPartnerId;      // 胜利招募伙伴
    }

    /// <summary>一个事件（配置表 §6）：至少 2 个选项。</summary>
    public class EventDef
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public ContentRegion Region;
        public EventCategory Category;
        public EventOptionDef[] Options;
    }

    /// <summary>
    /// 20 个 MVP 事件静态目录（实施计划 A2-19，对应《MVP 配置表》§6）。
    /// 数值与条件为配置表唯一来源；事件战斗的敌人引用与 ContentCatalog.EventCombatEnemies 保持一致。
    /// </summary>
    public static class EventCatalog
    {
        public static readonly EventDef[] All =
        {
            // ===== 6.1 草原事件 E01-E10 =====

            new EventDef
            {
                Id = "E01", DisplayName = "饥荒村", Region = ContentRegion.Plains, Category = EventCategory.Disaster,
                Description = "路边的村庄正遭受饥荒，村民们围上来乞求援助。",
                Options = new[]
                {
                    new EventOptionDef
                    {
                        Label = "支援", Condition = EventOptionCondition.PayResource, CostFood = 3,
                        ResultText = "声望 +8", ReputationDelta = 8
                    },
                    new EventOptionDef
                    {
                        Label = "有限援助", Condition = EventOptionCondition.PayResource, CostFood = 1,
                        ResultText = "声望 +3", ReputationDelta = 3
                    },
                    new EventOptionDef
                    {
                        Label = "离开", Condition = EventOptionCondition.None,
                        ResultText = "风险 +1", RiskDelta = 1
                    }
                }
            },

            new EventDef
            {
                Id = "E02", DisplayName = "迷路的斥候", Region = ContentRegion.Plains, Category = EventCategory.Encounter,
                Description = "一名斥候在岔路口徘徊，他向你们询问附近的路线。",
                Options = new[]
                {
                    new EventOptionDef
                    {
                        Label = "带回", Condition = EventOptionCondition.PayResource, CostFood = 2,
                        ResultText = "招募诺克斯（P03），声望 +3", RecruitPartnerId = "P03", ReputationDelta = 3
                    },
                    new EventOptionDef
                    {
                        Label = "索取报酬", Condition = EventOptionCondition.None,
                        ResultText = "财富 +8，风险 +1", WealthDelta = 8, RiskDelta = 1
                    }
                }
            },

            new EventDef
            {
                Id = "E03", DisplayName = "劫匪过路费", Region = ContentRegion.Plains, Category = EventCategory.Encounter,
                Description = "一伙劫匪拦住去路，要求缴纳过路费。",
                Options = new[]
                {
                    new EventOptionDef
                    {
                        Label = "缴纳", Condition = EventOptionCondition.PayResource, CostWealth = 10,
                        ResultText = "平安通过"
                    },
                    new EventOptionDef
                    {
                        Label = "交涉", Condition = EventOptionCondition.HasPartnerAndReputation,
                        RequirePartnerId = "P05", RequireReputation = 5,
                        ResultText = "声望 +3 后通过", ReputationDelta = 3
                    },
                    new EventOptionDef
                    {
                        Label = "战斗", Condition = EventOptionCondition.None,
                        ResultText = "遭遇路匪与野犬，胜利后财富 +5",
                        CombatEnemyIds = new[] { "EN01", "EN02" }, CombatLabel = "路匪与野犬",
                        VictoryBonusWealth = 5
                    }
                }
            },

            new EventDef
            {
                Id = "E04", DisplayName = "受伤哨兵", Region = ContentRegion.Plains, Category = EventCategory.Encounter,
                Description = "一名哨兵倒在路边，身上带着严重的伤口。",
                Options = new[]
                {
                    new EventOptionDef
                    {
                        Label = "救治", Condition = EventOptionCondition.PayResource, CostWealth = 5, CostFood = 2,
                        ResultText = "招募阿德里安（P01），声望 +4", RecruitPartnerId = "P01", ReputationDelta = 4
                    },
                    new EventOptionDef
                    {
                        Label = "搜刮", Condition = EventOptionCondition.None,
                        ResultText = "建材 +2，声望 -3", MaterialDelta = 2, ReputationDelta = -3
                    }
                }
            },

            new EventDef
            {
                Id = "E05", DisplayName = "损坏的商车", Region = ContentRegion.Plains, Category = EventCategory.Social,
                Description = "一辆商车坏在路中央，商人焦急地求助。",
                Options = new[]
                {
                    new EventOptionDef
                    {
                        Label = "修车", Condition = EventOptionCondition.PayResource, CostWealth = 8,
                        ResultText = "招募莉薇（P04），粮食 +2", RecruitPartnerId = "P04", FoodDelta = 2
                    },
                    new EventOptionDef
                    {
                        Label = "取货", Condition = EventOptionCondition.None,
                        ResultText = "财富 +12，风险 +1", WealthDelta = 12, RiskDelta = 1
                    }
                }
            },

            new EventDef
            {
                Id = "E06", DisplayName = "草火逼近", Region = ContentRegion.Plains, Category = EventCategory.Disaster,
                Description = "草原上的野火正随风逼近，浓烟遮蔽了半边天空。",
                Options = new[]
                {
                    new EventOptionDef
                    {
                        Label = "开辟隔离带", Condition = EventOptionCondition.HasRemoveableCard, RemoveCard = true,
                        ResultText = "建材 +3，声望 +2", MaterialDelta = 3, ReputationDelta = 2
                    },
                    new EventOptionDef
                    {
                        Label = "绕行", Condition = EventOptionCondition.None,
                        ResultText = "粮食 -3，风险 +1", FoodDelta = -3, RiskDelta = 1
                    }
                }
            },

            new EventDef
            {
                Id = "E07", DisplayName = "流动铁匠", Region = ContentRegion.Plains, Category = EventCategory.Social,
                Description = "一名铁匠支起临时摊子，展示着闪亮的兵器。",
                Options = new[]
                {
                    new EventOptionDef
                    {
                        Label = "升级", Condition = EventOptionCondition.PayResource, CostWealth = 15, UpgradeCard = true,
                        ResultText = "选择 1 张卡升级"
                    },
                    new EventOptionDef
                    {
                        Label = "购置", Condition = EventOptionCondition.PayResource, CostWealth = 8,
                        ResultText = "获得 C04 破甲斩", GrantCardId = "C04"
                    },
                    new EventOptionDef
                    {
                        Label = "离开", Condition = EventOptionCondition.None,
                        ResultText = "无变化"
                    }
                }
            },

            new EventDef
            {
                Id = "E08", DisplayName = "难民营", Region = ContentRegion.Plains, Category = EventCategory.Social,
                Description = "一片难民营地挤满了流离失所的人，头领正与你们交涉。",
                Options = new[]
                {
                    new EventOptionDef
                    {
                        Label = "接纳", Condition = EventOptionCondition.PayResource, CostFood = 4,
                        ResultText = "招募艾达（P05），声望 +5", RecruitPartnerId = "P05", ReputationDelta = 5
                    },
                    new EventOptionDef
                    {
                        Label = "征募", Condition = EventOptionCondition.ReputationAtLeast, RequireReputation = 8,
                        ResultText = "招募艾达（P05），忠诚度 50，声望 -2", RecruitPartnerId = "P05",
                        RecruitLoyalty = 50, ReputationDelta = -2
                    },
                    new EventOptionDef
                    {
                        Label = "拒绝", Condition = EventOptionCondition.None,
                        ResultText = "风险 +2", RiskDelta = 2
                    }
                }
            },

            new EventDef
            {
                Id = "E09", DisplayName = "风暴石碑", Region = ContentRegion.Plains, Category = EventCategory.Encounter,
                Description = "风暴中露出一座古老石碑，表面刻着陌生的符文。",
                Options = new[]
                {
                    new EventOptionDef
                    {
                        Label = "祈愿", Condition = EventOptionCondition.HasRemoveableCard, RemoveCard = true,
                        ResultText = "获得 C19 节能", GrantCardId = "C19"
                    },
                    new EventOptionDef
                    {
                        Label = "掠夺", Condition = EventOptionCondition.None,
                        ResultText = "财富 +10，风险 +2", WealthDelta = 10, RiskDelta = 2
                    }
                }
            },

            new EventDef
            {
                Id = "E10", DisplayName = "草原水源", Region = ContentRegion.Plains, Category = EventCategory.Encounter,
                Description = "一片清澈的水源出现在草原深处。",
                Options = new[]
                {
                    new EventOptionDef
                    {
                        Label = "休整", Condition = EventOptionCondition.None, StatusChoice = EventStatusChoice.FatigueSingle,
                        ResultText = "选择 1 名存活单位移除 1 层疲劳"
                    },
                    new EventOptionDef
                    {
                        Label = "采集", Condition = EventOptionCondition.None,
                        ResultText = "粮食 +5，风险 +1", FoodDelta = 5, RiskDelta = 1
                    }
                }
            },

            // ===== 6.2 密林事件 E11-E20 =====

            new EventDef
            {
                Id = "E11", DisplayName = "疫病营地", Region = ContentRegion.Jungle, Category = EventCategory.Disaster,
                Description = "密林深处有一座被疫病笼罩的营地，病人们在帐篷中呻吟。",
                Options = new[]
                {
                    new EventOptionDef
                    {
                        Label = "救治", Condition = EventOptionCondition.PayResource, CostWealth = 10,
                        StatusChoice = EventStatusChoice.DiseaseAll,
                        ResultText = "招募米蕾（P02），全员移除 1 层疾病，声望 +5",
                        RecruitPartnerId = "P02", ReputationDelta = 5
                    },
                    new EventOptionDef
                    {
                        Label = "掠夺药材", Condition = EventOptionCondition.None,
                        ResultText = "获得 C34 净化，声望 -4", GrantCardId = "C34", ReputationDelta = -4
                    }
                }
            },

            new EventDef
            {
                Id = "E12", DisplayName = "封存遗迹", Region = ContentRegion.Jungle, Category = EventCategory.Encounter,
                Description = "藤蔓缠绕的遗迹入口被符文封印，传来低沉的嗡鸣。",
                Options = new[]
                {
                    new EventOptionDef
                    {
                        Label = "破译", Condition = EventOptionCondition.HasPartnerOrReputation,
                        RequirePartnerId = "P07", RequireReputation = 10,
                        ResultText = "招募赛尔（P07），获得遗物 R01 旅人罗盘",
                        RecruitPartnerId = "P07", GrantRelicId = "R01"
                    },
                    new EventOptionDef
                    {
                        Label = "强行进入", Condition = EventOptionCondition.None,
                        ResultText = "遭遇菌疫兽，胜利后获得遗物 R01",
                        CombatEnemyIds = new[] { "EN07" }, CombatLabel = "菌疫兽",
                        VictoryBonusRelicId = "R01"
                    }
                }
            },

            new EventDef
            {
                Id = "E13", DisplayName = "林间伏击", Region = ContentRegion.Jungle, Category = EventCategory.Encounter,
                Description = "树枝在身后断裂——有人正埋伏在暗处。",
                Options = new[]
                {
                    new EventOptionDef
                    {
                        Label = "反伏击", Condition = EventOptionCondition.HasPartner,
                        RequirePartnerId = "P03",
                        ResultText = "财富 +8，风险 -1", WealthDelta = 8, RiskDelta = -1
                    },
                    new EventOptionDef
                    {
                        Label = "突围", Condition = EventOptionCondition.PayResource, CostFood = 3,
                        ResultText = "平安通过"
                    },
                    new EventOptionDef
                    {
                        Label = "战斗", Condition = EventOptionCondition.None,
                        ResultText = "遭遇林间伏匪，胜利后建材 +1",
                        CombatEnemyIds = new[] { "EN08" }, CombatLabel = "林间伏匪",
                        VictoryBonusMaterial = 1
                    }
                }
            },

            new EventDef
            {
                Id = "E14", DisplayName = "药草地", Region = ContentRegion.Jungle, Category = EventCategory.Encounter,
                Description = "一片药草在湿润的林间空地中生长，散发着淡淡药香。",
                Options = new[]
                {
                    new EventOptionDef
                    {
                        Label = "配药", Condition = EventOptionCondition.None, StatusChoice = EventStatusChoice.DiseaseOrFatigueSingle,
                        ResultText = "选择 1 名存活单位移除 1 层疾病或疲劳"
                    },
                    new EventOptionDef
                    {
                        Label = "采药出售", Condition = EventOptionCondition.None,
                        ResultText = "财富 +12", WealthDelta = 12
                    }
                }
            },

            new EventDef
            {
                Id = "E15", DisplayName = "走私小径", Region = ContentRegion.Jungle, Category = EventCategory.Social,
                Description = "一条隐秘小径通向密林深处，走私贩子示意你们跟上。",
                Options = new[]
                {
                    new EventOptionDef
                    {
                        Label = "走私", Condition = EventOptionCondition.PayResource, CostReputation = 2,
                        ResultText = "财富 +15，风险 +2", WealthDelta = 15, RiskDelta = 2
                    },
                    new EventOptionDef
                    {
                        Label = "举报", Condition = EventOptionCondition.None,
                        ResultText = "声望 +5，财富 +3", ReputationDelta = 5, WealthDelta = 3
                    }
                }
            },

            new EventDef
            {
                Id = "E16", DisplayName = "失落远征", Region = ContentRegion.Jungle, Category = EventCategory.Encounter,
                Description = "一具远征队骸骨靠在大树下，行囊中似乎还有完好的物资。",
                Options = new[]
                {
                    new EventOptionDef
                    {
                        Label = "搜救", Condition = EventOptionCondition.PayResource, CostFood = 3,
                        ResultText = "建材 +3，声望 +5", MaterialDelta = 3, ReputationDelta = 5
                    },
                    new EventOptionDef
                    {
                        Label = "搜刮", Condition = EventOptionCondition.None,
                        ResultText = "获得 C23 深思，风险 +1", GrantCardId = "C23", RiskDelta = 1
                    }
                }
            },

            new EventDef
            {
                Id = "E17", DisplayName = "古树契约", Region = ContentRegion.Jungle, Category = EventCategory.Encounter,
                Description = "一棵古树静静伫立，树皮上浮现出古老的契约文字。",
                Options = new[]
                {
                    new EventOptionDef
                    {
                        Label = "献出记忆", Condition = EventOptionCondition.HasRemoveableCard, RemoveCard = true,
                        ResultText = "获得遗物 R02 铁锅", GrantRelicId = "R02"
                    },
                    new EventOptionDef
                    {
                        Label = "供奉", Condition = EventOptionCondition.PayResource, CostWealth = 10,
                        ResultText = "获得遗物 R02 铁锅，声望 +2", GrantRelicId = "R02", ReputationDelta = 2
                    }
                }
            },

            new EventDef
            {
                Id = "E18", DisplayName = "盗猎营", Region = ContentRegion.Jungle, Category = EventCategory.Social,
                Description = "盗猎者的营地中关着铁笼，里面竟锁着一名战士。",
                Options = new[]
                {
                    new EventOptionDef
                    {
                        Label = "解救", Condition = EventOptionCondition.None,
                        ResultText = "遭遇林间伏匪，胜利后招募布蕾（P08），声望 +4",
                        CombatEnemyIds = new[] { "EN08" }, CombatLabel = "林间伏匪",
                        VictoryBonusPartnerId = "P08", VictoryBonusReputation = 4
                    },
                    new EventOptionDef
                    {
                        Label = "交易", Condition = EventOptionCondition.PayResource, CostWealth = 12,
                        ResultText = "招募布蕾（P08），声望 -3", RecruitPartnerId = "P08", ReputationDelta = -3
                    }
                }
            },

            new EventDef
            {
                Id = "E19", DisplayName = "发热旅人", Region = ContentRegion.Jungle, Category = EventCategory.Disaster,
                Description = "一名旅人倒在路边，浑身发烫，意识模糊。",
                Options = new[]
                {
                    new EventOptionDef
                    {
                        Label = "共用药品", Condition = EventOptionCondition.HasPartnerOrCard,
                        RequirePartnerId = "P02", RequireCardId = "C34",
                        ResultText = "声望 +6，获得 C37 调养", ReputationDelta = 6, GrantCardId = "C37"
                    },
                    new EventOptionDef
                    {
                        Label = "绕过", Condition = EventOptionCondition.None,
                        ResultText = "风险 +1", RiskDelta = 1
                    }
                }
            },

            new EventDef
            {
                Id = "E20", DisplayName = "狼群踪迹", Region = ContentRegion.Jungle, Category = EventCategory.Encounter,
                Description = "地上的爪印通向密林深处，隐约传来狼嚎。",
                Options = new[]
                {
                    new EventOptionDef
                    {
                        Label = "追猎", Condition = EventOptionCondition.HasPartnerOrPartner,
                        RequirePartnerId = "P03", RequirePartnerId2 = "P06",
                        ResultText = "招募约恩（P06），粮食 +5", RecruitPartnerId = "P06", FoodDelta = 5
                    },
                    new EventOptionDef
                    {
                        Label = "设伏", Condition = EventOptionCondition.None,
                        ResultText = "遭遇毒丝蛛与古牙野猪，胜利后获得 C27 出击命令",
                        CombatEnemyIds = new[] { "EN06", "EN09" }, CombatLabel = "毒丝蛛与古牙野猪",
                        VictoryBonusCardId = "C27"
                    }
                }
            }
        };

        /// <summary>按 ID 查找事件。</summary>
        public static EventDef Find(string id)
        {
            foreach (var e in All)
            {
                if (e.Id == id) return e;
            }

            return null;
        }
    }
}

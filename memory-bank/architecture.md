# 架构与文件职责（截至 2026-08-12）

## 目录与程序集
- `Assets/Scripts/Core/` — 核心运行时逻辑（程序集 OneJourney.Core，无外部引用）。
- `Assets/Tests/EditMode/` — EditMode 测试（OneJourney.EditModeTests.asmdef，NUnit 风格）。

## 运行时入口与场景
- `GameBootstrap.cs` — `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`：初始化 `GameConfigProvider`、`ContentRegistry.LoadAll()` 与 `RunSession.Reset()`。**不创建 UI**——UI 由场景承载（MVP 单场景，UI 不跨场景存活）。
- `Assets/Scenes/SampleScene.unity` — 唯一主场景：Main Camera + GameUi 层级（GameUi / Canvas / TestHud / MainMenu / TestPage / EventSystem）。

## 配置系统
- `GameMode.cs` — 枚举 Development / Testing / Release。
- `GameConfig.cs` — ScriptableObject：`_mode`、`_showTestHud`、`_enableTestEntries`；静态工厂 `Create(mode, showTestHud, enableTestEntries)`。
- `GameConfigProvider.cs` — 静态提供者：按启动参数（`-releaseMode` 锁定、`-testMode`）或 `Application.isEditor` 决定启动模式；从 `Resources/Configs/GameConfig_<Mode>.asset` 加载，缺失时回退 `GameConfig.Create` 默认值并打警告；`IsReleaseLocked` 时忽略非 Release 切换。
- `Assets/Data/Resources/Configs/GameConfig_{Development,Testing,Release}.asset` — 三套配置资产（Release：HUD 与测试入口均隐藏；开发/测试：均开启）。

## 会话与流程
- `GameFlow.cs` — 流程状态机：`CurrentState`（唯一状态源）、11 状态转移表（`IsAllowed`）、`TryTransition(to, reason)`（非法转移拒绝且无副作用并打警告）、状态日志 `Log`（上限 100 条，`LastTransition` 取最新）、`Reset` 清空日志、`Changed` 事件。`GameState` 枚举值：None/MainMenu/Combat/Map/Event/Camp/NewGame/Move/Reward/Victory/Defeat/Settlement（新状态为追加，旧值不变）。
- `RunSession.cs` — 静态会话数据：随机种子（支持 `-seed <n>` 与 UI 种子输入）、`CurrentState` 委托 `GameFlow`、结算记录列表（上限 20 条）；`StartNewGame`（含阻塞检查 + `GameRandom` 初始化 + `RunRecord.Clear()` + `InitCampaignResources()` + `RegionMap.Generate(Plains)`）、`EnterTestPage`（Combat 时调用 `InitTestCombat` 创建测试战斗；Map 时生成草原地图）、`RecordResolution`、`Reset`（清理 `Random`/`RunRecord`/`Records`/`GameFlow`/`PartnerRoster`/`RewardResolver`/`RegionMap`/资源/风险/疲劳）；`Random` 静态属性；`Changed` 事件驱动 UI 刷新。
- 战役状态（A2-18）：`Food`/`Wealth`/`Reputation`/`Materials`（起始资源）、`Risk`（0-10 区域风险）、`PlayerFatigue`（主角战役疲劳）、`AmbushPending`（危机伏击标记）；`TryMoveToNode(nodeIndex)`（移动结算：地图校验→粮食消耗→不足惩罚（粮归 0/主角疲劳 +1/风险 +2）→风险增长（基础 +1、精英额外 +1）→阈值 10 重置 5 并标记伏击，返回可读文本，拒绝时资源不变）；`SyncPlayerFromCombat`（胜利回写主角疲劳）；测试辅助 `SetFoodForTest`/`SetRiskForTest`。
- `GameStartParameters.cs` — 第一版起始参数与全局规则常量（唯一代码来源，对应《MVP 配置表》）：主角 45 血/6 指令伤害、上阵 4 人、牌组 10–30、手牌 3/1/5、能量 3、起始资源（粮 14 财 30 声望 0 建材 0）、粮食不足惩罚（+1 疲劳、风险 +2）、风险常量（阈值 10、危机后重置 5、草原/密林移动 +1/+2、精英额外 +1、营地结算 -2）、起始牌组 10 张（C01×4、C09×3、C17、C33、C36）、垂直切片目标 EN10。

## 内容系统
- `ContentBase`（`ContentModels.cs`）— 内容数据基类 `ScriptableObject`（id/displayName/description）。七类内容：`CardData`（卡牌，cost 0–4、targetType、effectText）、`PartnerData`（伙伴，maxHp 1–100、commandDamage 0–20、role/joinCardId）、`EnemyData`（敌人，maxHp 1–200、intents）、`EventData`（事件，options≥2）、`RelicData`（遗物，effectText）、`NodeData`（节点，enemyPoolIds/eventPoolIds）、`BuildingData`（建筑，四项成本 0–999、effectText）。
- `ContentCatalog.cs` — 静态 ID 清单与引用关系表（伙伴加入卡/建筑解锁卡/事件授予卡/事件战斗敌人），与《MVP 配置表》保持一致。
- `ContentValidator.cs` — 校验纯函数：必填/范围/引用/ID 唯一；`ValidationIssue` 结构可定位到类型[ID]字段。`ContentRegistry`：从 `Resources/Content` 加载全部资产并校验；`HasBlockingIssues` 为 true 时 `StartNewGame` 被阻止；`Clear()` 供测试隔离与修复后重加载。
- `ContentValidationTests.cs` — 12 个 EditMode 用例覆盖全部校验场景，`LogAssert.Expect` 声明预期错误日志。
- `design/content-validation-spec.md` — 校验规范（必填字段表、校验流程、错误格式、新增内容要求）。

## 随机与记录
- `GameRandom.cs` — 可复现种子随机数器：包装 `System.Random`，`Next`/`NextFloat`/`Shuffle`(Fisher-Yates)/`WeightedPick`（空池/零权重/负权重时通过 `out issue` 报告明确原因，返回 -1/default 而非崩溃）。
- `RunRecord.cs` — 本局详细记录（上限 200 条）：5 类 `RecordCategory`（抽牌/敌人意图/地图分支/事件选项/奖励选择）+ 一般；`Log` 写入有序条目、`Clear` 清空、`Entries` 只读访问；溢满时截断并重新编号。
- `GameRandomTests.cs` — 12 个 EditMode 用例：同种子一致/异种子不同/洗牌确定/加权正常/空池/零权重/负权重/泛型加权。
- `RunRecordTests.cs` — 5 个 EditMode 用例：记录顺序/清空/超上限截断/分类名。

## 卡牌系统（A1-13）
- `CardDef.cs` — 卡牌效果类型枚举（28 种 `CardEffectType`：Damage/GainArmor/Heal/Draw/ApplyBleed/ApplyDisease/ApplyFatigue/AddMorale/RemoveBleed/RemoveDisease/RemoveFatigue/RemoveArmor/ReduceIntent/SelfArmor/PartnerArmor/BonusDrawNextTurn/CostReduction/Exhaust/SupplyFood/FocusFire/Taunt/RemoveCapture/RemoveInjury/PartnerDamage/AllPartnerDamage/DrawThenDiscard/DiscardThenDraw/ExhaustThenDraw/Choice）、效果条件枚举（TargetBleedGE2/SelfArmorGE10）、`CardEffect` 结构（Type+P0+P1+Condition）、`CardDef` 数据类（Id/DisplayName/Cost/TargetType/Rarity/Effects）。
- `CardCatalog.cs` — 40 张卡牌静态目录：`All` 只读列表（按配置表 §3 分五类 C01-C40）、`Find(id)`/`Exists(id)` 查找。MVP 阶段为代码内硬编码，后续可迁移至 ScriptableObject。

## 地图系统（A2-17）
- `RegionMap.cs` — 区域节点地图静态管理器：`RegionMapNode`（Id/Layer 1-4/Type/EnemyPoolIds/EventPoolIds/NextIndexes）+ `RegionMap`：`Generate(region, rng)`（草原 4 层：L1 战斗/事件/营地、L2 战斗/事件/精英、L3 战斗/事件/营地、L4 首领；层内顺序洗牌、层间 `ConnectLayers` 保证上下层入度/出度≥1、第三层全连首领、无回退）、`TryMoveTo(index)`（下一层/相连/未访问三重校验，失败 out reason 且状态不变，成功写 `RunRecord` 地图分支）、`ReachableNext()`（当前可移动节点，供 UI 高亮）、`CurrentNodeIndex`（-1=起点）/`CurrentLayer`/`RemainingLayers`/`Path`/`VisitedIndexes`、`Clear`。密林留待 A2-23。
- `RegionMapTests.cs` — 13 个 EditMode 用例：10 固定种子结构/构成/池引用、BFS 可达首领、每节点出���≥1、无回退、第三层全连首领、同种子同图、起点仅限第一层、未连接/已访问/跨层拒绝、合法移动、完整路径到首领。

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
- `RegionMapTests.cs` — 13 个 EditMode 用例：10 固定种子结构/构成/池引用、BFS 可达首领、每节点出度≥1、无回退、第三层全连首领、同种子同图、起点仅限第一层、未连接/已访问/跨层拒绝、合法移动、完整路径到首领。

## 战斗系统
- `CombatUnit.cs` — 战斗单位：`Id`/`DisplayName`/`MaxHp`/`CurrentHp`/`Armor`/`IsAlive`/`IsPlayerCharacter`；`TakeDamage`（优先扣护甲再扣血，返回实际伤害）、`Heal`、`Clone`（独立副本隔离原始数据）；工厂方法 `CreatePlayer`/`CreateCompanion`/`CreateEnemy`。`FocusFireExtra`（集火标记额外伤害）。
- `CombatDeck.cs` — 战斗独立牌堆：`DrawPile`/`Hand`/`DiscardPile`/`ExhaustZone`；`InitFromCampaign`（复制+洗牌）、`DrawToHand`（空堆洗回/手牌上限）、`DiscardHand`（临时卡 `TEMP_` 前缀→消耗区，普通卡→弃牌堆）、`ExhaustFromHand`/`DiscardFromHand`、`Clone`。
- `CombatManager.cs` — 战斗生命周期 + 回合结构 + 敌人行动：`Phase`/`TurnPhase`/`TurnNumber`/`Energy`（MaxEnergy=3 每回合重置）；`Morale`/`MoraleUsedThisTurn`/`Plunder`；`PendingBonusDraw`（下回合额外抽牌）/`CostReductionRemaining`（本回合减费）；`RevealEnemyIntents`（BeginPlayerTurn 揭示敌人意图供玩家规划）、`ExecuteEnemyActions`（攻击/全体攻击/防御/掠夺四类执行，行动前重验目标存活，无目标默认跳过；支持 `TargetsPlayer` 诱饵定向）、`PickDefaultTarget`（生命百分比最低，平局主角优先）、`PlayerCharacter()`（获取存活主角）；`CanPlayerAct`/`CanSpendEnergy`/`SpendEnergy`/`RefundEnergy`；`ForceDefeat`/`End`。
- `CombatDeckTests.cs` — 13 个 EditMode 用例：副本独立/洗牌确定/同种子同序/弃牌堆洗回/两堆皆空/手牌上限/弃手牌/消耗/弃单张/Clone/临时卡隔离。
- `CombatResolver.cs` — 目标选择与伤害结算：`ResolveTargets`（六种 TargetType，仅存活单位，无目标 out issue）、`ApplyDamage`（护甲吸收→生命→死亡→CheckEndCondition，返回可读结算文本；接入士气和集火标记）、`PlayCard(int handIndex, CombatUnit selectedTarget)`（完整出牌管线：费用校验→目标解析→移除手牌→`ApplyEffect` 分发 28 种效果→弃牌/消耗→结束检查）、`PlayTestCard`（已标记 Obsolete，剩余引用来自旧测试按钮）。
- `CombatResolverTests.cs` — 18 个 EditMode 用例：目标范围/死亡排除/无目标报错/护甲恰好吸收/伤害多1/单体只伤一个/全体伤全部/批内击杀胜利/批内跳过死目标/能量不足/无目标退款。
- `CombatStatus.cs` — 状态规则统一入口：上限常量（流血 5/士气 3/疾病 3/疲劳 3/护甲 30）、每层效果（疾病 -4 最大生命/疲劳 -5 护甲上限 -1 指令伤害/士气 +2 伤害）、施加叠加（钳上限、不作用于死亡单位、疾病钳当前生命、疲劳钳护甲）、移除、`TriggerTurnStartBleed`（真实伤害=层数，伤害后 -1，致死触发结束检查）、`TriggerTeamTurnStartBleed`。
- `CombatStatusTests.cs` — 16 个 EditMode 用例：流血叠加/真实伤害/衰减/致死/死亡不施加/疾病上限钳制/疲劳上限钳制/士气加成重置/多状态共存顺序。
- `CampaignDeck.cs` — 战役牌组（A2-16）：战斗外持久化卡牌集合；`AddCard`（≤30）/`RemoveCard`/`RemoveCardAt`（≥10）/`CloneCardList`；A2-19：`IsInitialLockedCard`（初始牌组锁定，不可被事件移除）/`HasRemoveableCard`/`RemoveableCards`（事件移除卡选项）/`UpgradedCards`+`UpgradeCard`（升级标记，同卡仅一次）。
- `RewardResolver.cs` — 战斗奖励（A2-16）：按遭遇类型生成资源+卡牌选项；`PendingOptions`/`ClaimCard`（选一清空）/`SkipReward`/`Clear`。
- `PartnerDef.cs` / `PartnerRoster.cs` — 伙伴系统（A2-15）：8 名伙伴静态定义 + 运行时状态（招募/上阵/HP/疾病/疲劳/忠诚度）；`BuildCombatTeam`（伙伴在前旅人第二位）/`SyncFromCombat`/`InitTestRoster`。
- `CardCatalogTests.cs` — 27 个 EditMode 用例（A1-13）：目录完整性（6）/ 出牌基础流程（5）/ 各类卡牌效果（14）/ 边界与特殊机制（2）。
- `PartnerRosterTests.cs` — 15 个 EditMode 用例（A2-15）：数据完整性/招募/上阵上限/未招募拒绝/死亡拒绝/BuildCombatTeam/SyncFromCombat/Clear。
- `CampaignDeckTests.cs` — 12 个 EditMode 用例（A2-16）：牌组初始化/上下限/独立副本/奖励生成/领取/跳过。
- `EnemyUnit.cs` — 敌人单位（继承 CombatUnit）：`EnemyIntentExec`（攻击/全体攻击/防御/掠夺 + 副作用 BleedStacks/DiseaseStacks + 诱饵标记 `TargetsPlayer`）+ `RollIntent`（种子驱动加权抽取）+ 10 种敌人工厂（EN01-EN10，按配置表 §5）；`Clone` 重写深拷贝意图。
- `EnemyIntentTests.cs` — 12 个 EditMode 用例：同意图/零权重/首回合揭示/攻击/防御/掠夺/AOE/死敌跳过/无目标/默认目标选择/士气隔离。
- `EncounterConfig.cs` — 9 组遭遇表（草原普通×2/精英/首领，密林普通×3/精英/首领，含标签/区域/类型）；`RunSession._testEncounterIndex` 翻页选择。

## 事件系统（A2-19）
- `EventCatalog.cs` — 20 个事件静态目录（配置表 §6，与 CardCatalog/EnemyUnit 同模式：代码内硬编码，可复现）：`EventDef`（Id/DisplayName/Description/Region/Category/Options）、`EventOptionDef`（条件/支付/即时结果/招募/获得卡与遗物/移除卡/升级/状态移除/事件战斗与胜利额外奖励）、`EventOptionCondition`（PayResource/HasPartner*/ReputationAtLeast/HasRemoveableCard 等 8 种）、`EventStatusChoice`（FatigueSingle/DiseaseAll/DiseaseOrFatigueSingle）；`Find(id)`。
- 事件结算（RunSession 内）：`StartEventFromNode`（地图事件节点按种子抽取）/`StartEvent`（测试与节点进入，Event 状态幂等）/`EventOptionBlockReason`（条件校验返回禁用原因，供 UI 置灰）/`ChooseEventOption`（支付→战斗或子选择或即时结算）/`ChooseEventCard`（移除/升级子选择）/`ChooseEventStatusUnit`（单位状态移除）/`CancelEventChoice`/`ApplyPendingEventCombatRewards`（胜利额外奖励仅结算一次，由 CombatManager 胜利分支调用）/`ClearPendingEventCombatRewards`（失败清除，防残留）；`Relics` 遗物持有记录（效果 A2-22 接入）；`PlayerDisease` 主角战役疾病（SyncPlayerFromCombat 同步疲劳+疾病）。
- 资源钳制：事件与移动结算统一用 `Clamp` 保证粮 0-30/财 0-999/声望 0-100/建材 0-99/风险 0-10 不为负；招募伙伴已招募→忠诚 +10、阵亡→选项禁用（配置表 §6 通用规则）。
- `EventTests.cs` — 57 个 EditMode 用例：目录完整性/每选项结算/条件不满足/事件战斗胜利与失败/子选择/忠诚规则/钳制。

## UI 结构（场景组件化 + Prefab 驱动）
- `GameUi.cs` — 场景 UI 驱动：`[SerializeField]` 持有面板/HUD/标题/描述/按钮/`_battleView`/`_mapNodeContainer`/`_eventOptionContainer`；`ShowPage` 战斗中委托 `BattleView`；`ReturnToMenu()` 公开供 BattleView 调用；`Refresh` 同步刷新 BattleView；`OnMapNodeClicked` 走 `RunSession.TryMoveToNode` 并在事件节点进入事件页；`RefreshEventOptions` 动态渲染事件选项（条件不满足置灰）与子选择列表（移除/升级卡、单位状态）。
- `BattleView.cs` — 战斗界面独立控制器（A1-14）：从 `_battlePagePrefab` 运行时实例化 BattlePage；管理手牌/单位卡片生命周期；处理出牌/选目标/结束回合/返回菜单交互。子组件通过 `ResolveRefs()` 自动解析。
- HUD（TestHud，尺寸 680×300）：7 行文本——随机种子 / 当前状态 / 当前配置 / 最近一次规则结算 / 最近状态切换（最近 3 条）/ 内容校验状态（OK 或 N 个问题+首个）/ 本局记录（N 条+最新类别 #序号）。
- Canvas：ScreenSpaceOverlay + CanvasScaler（1920×1080，match 0.5）。**子对象顺序即渲染顺序**：MainMenu → BattlePage(运行时) → TestPage → TestHud（HUD 在顶层）。
- MainMenu：ScrollRect + Viewport(RectMask2D) + Content(VerticalLayoutGroup)，增删按钮自动重排。
- TestPage：旧版战斗测试页（A1-14 后战斗中隐藏，BattleView 替代）。
- EventSystem：EventSystem + StandaloneInputModule（Legacy Input）。

## 战斗界面 Prefab（A1-14）
- `Assets/Prefabs/BattlePage.prefab` — 五区块布局（全部 TMP）：TopBar（TurnInfo/Energy/Morale/Plunder/EndTurnBtn）/ MainArea（TeamPanel/EnemyPanel/RightPanel+ReturnBtn）/ BottomBar（DrawPile/HandCards/DiscardPile）。
- `Assets/Prefabs/HandCard.prefab` — 手牌卡片（TMP）：TopBar(类型色)/ CostRow(Cost/Name) / Effect。
- `Assets/Prefabs/UnitCard.prefab` — 队伍单位卡片（TMP）：TopBar / Name / HP / Status / Intent。
- `Assets/Prefabs/EnemyCard.prefab` — 敌人卡片（TMP，结构同 UnitCard，独立样式）。

## 事件流
- `RunSession.Changed` / `GameFlow.Changed` → `GameUi.Refresh()`（HUD：种子/状态/配置/最近结算/最近状态切换）；`GameConfigProvider.Changed` → `GameUi.RefreshConfigUi()`（显隐控制）。
- `GameBootstrap`（BeforeSceneLoad）→ `GameConfigProvider.Initialize()` + `ContentRegistry.LoadAll()` + `RunSession.Reset()` → 场景 `GameUi.Awake` 接管 UI。

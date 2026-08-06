# 架构与文件职责（截至 2026-08-05）

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
- `RunSession.cs` — 静态会话数据：随机种子（支持 `-seed <n>` 与 UI 种子输入）、`CurrentState` 委托 `GameFlow`、结算记录列表（上限 20 条）；`StartNewGame`（含阻塞检查 + `GameRandom` 初始化 + `RunRecord.Clear()`）、`EnterTestPage`、`Reset`（清理 `Random`/`RunRecord`/`Records`/`GameFlow`）；`Random` 静态属性提供种子随机数器；`Changed` 事件驱动 UI 刷新。
- `GameStartParameters.cs` — 第一版起始参数与全局规则常量（唯一代码来源，对应《MVP 配置表》）：主角 45 血/6 指令伤害、上阵 4 人、牌组 10–30、手牌 3/1/5、能量 3、起始资源（粮 14 财 30 声望 0 建材 0）、粮食不足惩罚（+1 疲劳、风险 +2）、风险常量（阈值 10、危机后重置 5、草原/密林移动 +1/+2、精英 +1、营地 -2）、起始牌组 10 张（C01×4、C09×3、C17、C33、C36）、垂直切片目标 EN10。

## 内容系统
- `ContentBase`（`ContentModels.cs`）— 内容数据基类 `ScriptableObject`（id/displayName/description）。七类内容：`CardData`（卡牌，cost 0–4、targetType、effectText）、`PartnerData`（伙伴，maxHp 1–100、commandDamage 0–20、role/joinCardId）、`EnemyData`（敌人，maxHp 1–200、intents）、`EventData`（事件，options≥2）、`RelicData`（遗物，effectText）、`NodeData`（节点，enemyPoolIds/eventPoolIds）、`BuildingData`（建筑，四项成本 0–999、effectText）。
- `ContentCatalog.cs` — 静态 ID 清单与引用关系表（伙伴加入卡/建筑解锁卡/事件授予卡/事件战斗敌人），与《MVP 配置表》保持一致。
- `ContentValidator.cs` — 校验纯函数：必填/范围/引用/ID 唯一；`ValidationIssue` 结构可定位到类型[ID]字段。`ContentRegistry`：从 `Resources/Content` 加载全部资产并校验；`HasBlockingIssues` 为 true 时 `StartNewGame` 被阻止；`Clear()` 供测试隔离与修复后重加载。
- `ContentValidationTests.cs` — 12 个 EditMode 用例覆盖全部校验场景，`LogAssert.Expect` 声明预期错误日志。
- `design/content-validation-spec.md` — 校验规范（必填字段表、校验流程、错误格式、新增内容要求）。

## 随机与记录
- `GameRandom.cs` — 可复现种子随机数器：`Next`/`NextFloat`/`Shuffle`/`WeightedPick`（空池/零权重/负权重时通过 `out issue` 报告，返回 -1/default 而非崩溃）。
- `RunRecord.cs` — 本局详细记录（上限 200 条）：5 类 `RecordCategory`（抽牌/敌人意图/地图分支/事件选项/奖励选择）；`Log`/`Clear`/`Entries`。
- `GameRandomTests.cs` — 12 个 EditMode 用例（同种子一致/异种子不同/洗牌确定/空池/零权重/负权重/泛型加权）。
- `RunRecordTests.cs` — 5 个 EditMode 用例（记录顺序/清空/超上限/分类名）。

## 战斗系统
- `CombatUnit.cs` — 战斗单位：HP/护甲/存活/伤害吸收(护甲优先)/治愈/独立副本。
- `CombatDeck.cs` — 独立牌堆：`InitFromCampaign`/`DrawToHand`(空堆洗回+手牌上限)/`DiscardHand`(临时卡→消耗区，普通卡→弃牌堆)/`ExhaustFromHand`。
- `CombatManager.cs` — 生命周期 + 回合结构：`Phase`/`TurnPhase`/`TurnNumber`/`Energy`；`Morale`/`MoraleUsedThisTurn`；回合开始双方流血结算；`CanPlayerAct`/`SpendEnergy`/`RefundEnergy`。
- `CombatManagerTests.cs` — 20 个 EditMode 用例。
- `CombatResolver.cs` — 目标解析（6 种 TargetType）+ 伤害管线（护甲→生命→死亡→结束检查）+ `PlayTestCard`（无目标退款）。
- `CombatResolverTests.cs` — 18 个 EditMode 用例（目标范围/护甲边界/批内胜利/死目标跳过/退款）。

## UI 结构（场景组件化）
- `GameUi.cs` — 场景 UI 驱动：`[SerializeField]` 持有面板/HUD/标题/描述/按钮（含种子输入、战斗胜利/失败按钮）/显隐元素；`BuildCombatDescription` 生成战斗状态文本。
- HUD（TestHud，尺寸 680×300）：7 行文本——随机种子 / 当前状态 / 当前配置 / 最近一次规则结算 / 最近状态切换（最近 3 条）/ 内容校验状态 / 本局记录（N 条+最新类别 #序号）。
- Canvas：ScreenSpaceOverlay + CanvasScaler（1920×1080，match 0.5）。**子对象顺序即渲染顺序**：MainMenu → TestPage → TestHud（HUD 在顶层）。
- MainMenu：ScrollRect + Viewport(RectMask2D) + Content(VerticalLayoutGroup，childControlWidth=false，元素宽 700，内容高 706)，增删按钮自动重排。
- TestPage：VerticalLayoutGroup，默认 Inactive；标题/描述/`CombatActions`（GridLayoutGroup 5×2，字号 16）/记录按钮/返回按钮；战斗按钮仅 Combat 状态显示。
- EventSystem：EventSystem + StandaloneInputModule（Legacy Input）。

## 事件流
- `RunSession.Changed` / `GameFlow.Changed` → `GameUi.Refresh()`（HUD：种子/状态/配置/最近结算/最近状态切换）；`GameConfigProvider.Changed` → `GameUi.RefreshConfigUi()`（显隐控制）。
- `GameBootstrap`（BeforeSceneLoad）→ `GameConfigProvider.Initialize()` + `ContentRegistry.LoadAll()` + `RunSession.Reset()` → 场景 `GameUi.Awake` 接管 UI。

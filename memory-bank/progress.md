# 实施进度

## 2026-08-05 · A0-1 固定项目运行基线（UI 部分，用户已验证）
- 完成：
  - `GameUi.cs` 由运行时动态构建改为场景组件驱动（`[SerializeField]` 引用 + Awake 按钮绑定）。
  - `GameBootstrap.cs` 不再动态创建 GameRoot/GameUi，只保留配置与会话初始化。
  - `Assets/Scenes/SampleScene.unity` 搭建完整 UI 层级（GameUi/Canvas/TestHud/MainMenu/TestPage/EventSystem），引用已连接并保存。
  - 修复 TestHud 被 MainMenu 遮挡：移至 Canvas 子对象最后（渲染顶层）。
- 用户验证通过：主菜单、新游戏、四个测试入口、三套配置切换、HUD 显示均正常。
- 配置事实：Release 配置 `_showTestHud=false`、`_enableTestEntries=false`（HUD 与测试入口隐藏）；开发/测试配置均开启。

## 2026-08-05 · A0-2 定义游戏术语和状态边界（用户已验证）
- 完成：
  - `GameFlow.cs`（新）：流程状态机——11 个状态、转移表（`IsAllowed`）、`TryTransition` 校验（非法转移拒绝且无副作用并打警告）、状态日志（上限 100 条）、`Reset` 清空日志、`Changed` 事件。
  - `RunSession.cs`：`GameState` 枚举追加 `NewGame/Move/Reward/Victory/Defeat/Settlement`（原有枚举值不变，场景序列化兼容）；`CurrentState` 委托 `GameFlow`；`StartNewGame` 走 主菜单→新局初始化→地图 两步转移，`EnterTestPage` 仅允许从主菜单进入；`DisplayName` 补齐新状态。
  - `GameUi.cs`：HUD 增加「最近状态切换」行（显示最近 3 条，含 From/To/Reason），订阅 `GameFlow.Changed`；TestHud 高度 200→300。
  - `GameFlowTests.cs`（新）：8 个 EditMode 用例（新游戏路径/非法转移/重复切换/四测试入口/完整路线/失败重开/结算隔离/全状态可达性）。
  - 设计文档：`design/glossary.md`（术语表）、`design/game-state-flow.md`（状态流转定义）。
- 用户反馈并修复：HUD 第五行曾只显示最后一条切换（两次连续转移只显示后一次），改为显示最近 3 条。
- 用户验证通过：EditMode 测试与 Play 模式状态切换均正常。

## 2026-08-05 · A0-3 制定基础数值与内容清单（用户已验证）
- 完成：
  - `design/mvp-configuration-tables.md`（已有 v0.1 完整配置表）新增 **10.1 测试用例编号规则**：TC-C01…TC-C40、TC-P01…、TC-EN01…、TC-E01…、TC-R01…、TC-B01…、TC-S01、TC-MAP-PLAINS/JUNGLE。
  - `GameStartParameters.cs`（新，A0-4 步已从 GameRules.cs 重命名）：第一版起始参数唯一代码来源——主角 45 血/6 指令伤害、上阵 4 人、牌组 10–30、手牌 3/1/5、能量 3、起始资源（粮 14 财 30 声望 0 建材 0）、粮食不足惩罚（主角 +1 疲劳、风险 +2）、风险规则（阈值 10、危机后重置 5）、起始牌组 10 张（C01×4、C09×3、C17、C33、C36）、垂直切片目标 EN10。
  - `RunSession.cs`：新游戏结算记录包含起始资源摘要（HUD 可查）。
  - `GameStartParametersTests.cs`（新）：5 个 EditMode 用例（参数固定值、起始牌组与配置表一致、十局新游戏起始资源一致且种子互异、指定种子记录）。
- 验证：十局新游戏种子唯一、起始资源完全一致；配置表 92 个 ID 唯一、47 处引用全部可解析；用户验证通过。

## 2026-08-05 · A0-4 建立数据校验与错误呈现规范（用户已验证）
- 完成：
  - `ContentModels.cs`（新）— 7 类内容数据模型（CardData/PartnerData/EnemyData/EventData/RelicData/NodeData/BuildingData），均继承 `ContentBase : ScriptableObject`，含必填字段注释与 `[Range]` 约束。
  - `ContentCatalog.cs`（新）— 内容 ID 清单（40 卡×8 伙伴×10 敌×20 事件×8 遗物×5 建筑）+ 加入卡/解锁卡/事件授予卡/事件战斗敌人引用表。
  - `ContentValidator.cs`（新）— 校验纯函数 + `ContentRegistry`：必填/范围/引用/ID 唯一性检查；`LoadAll()` 从 `Resources/Content` 加载；`HasBlockingIssues` 时阻止新游戏；`Clear()` 供测试隔离与修复后重加载。
  - `GameBootstrap.cs` — 接入 `ContentRegistry.LoadAll()`。
  - `RunSession.cs` — `StartNewGame` 在阻塞时记录「内容校验」结算并拒绝进入。
  - `GameUi.cs` — HUD 第 6 行显示「内容校验：OK / N 个问题（首个：…）」。
  - `ContentValidationTests.cs`（新）— 12 个 EditMode 用例（缺必填/缺 ID/引用不存在/越界/重复 ID/事件选项不足/敌人无意图/零权重意图/修复通过/坏内容阻止新游戏/修复后启动），`LogAssert.Expect` 声明预期错误日志。
  - `design/content-validation-spec.md`（新）— 校验规范：必填字段表、校验流程、错误格式、新增内容要求。
- 验证：Test Runner 全绿（24+ 用例），Play 模式 HUD 显示「内容校验：OK」且新游戏正常进入。

## 2026-08-05 · A0-5 实现可复现的随机与结算记录（用户已验证）
- 完成：
  - `GameRandom.cs`（新）— 带种子 `System.Random` 包装：`Next`/`NextFloat`/`Shuffle`(Fisher-Yates)/`WeightedPick`（空池/零权重/负权重保护）。
  - `RunRecord.cs`（新）— 有序本局记录（上限 200 条），5 类：抽牌/敌人意图/地图分支/事件选项/奖励选择。
  - `RunSession.cs` — `Random` 随机器在 `StartNewGame`/`EnterTestPage` 用种子初始化，`Reset` 清理；`RunRecord.Clear()` 嵌入生命周期。
  - `GameUi.cs` — 场景新增 `InputField_种子`（左下角 250×40）+ `Button_指定种子新游戏`（180×40）；HUD 第 7 行显示本局记录状态。
  - `GameRandomTests.cs`（新）— 12 个 EditMode 用例。
  - `RunRecordTests.cs`（新）— 5 个 EditMode 用例。
- 验证：Test Runner 全绿；Play 模式指定种子 12345→HUD 正确显示；留空→随机种子。

## 2026-08-05 · A1-6 搭建战斗的初始化与结束规则（用户已验证）
- 完成：
  - `CombatUnit.cs`（新）— 战斗单位：HP/护甲/存活/伤害吸收/治愈/独立副本。
  - `CombatDeck.cs`（新）— 战斗独立牌堆：抽牌堆/手牌/弃牌堆/消耗区；空堆洗回、手牌上限。
  - `CombatManager.cs`（新）— 生命周期：Init（克隆+安全拦截）→Running→胜负判定→End（清理）；撤退禁用。
  - `RunSession.cs` — `EnterTestPage(Combat)` 初始化 2v2 测试战斗。
  - `GameUi.cs` — 测试页显示战斗状态；新增「模拟胜利」「模拟失败」按钮。
  - `CombatManagerTests.cs`（新）— 11 个 EditMode 用例。
  - 修复：`RunSessionTests.EnterTestPage_SpecifiedSeed` 断言更新。
- 验证：Test Runner 全绿；Play 模式战斗入口正常，按钮可用，返回重进不残留。

## 2026-08-05 · A1-7 实现回合结构与共享能量（用户已验证）
- 完成：
  - `CombatManager.cs` — 新增 `TurnPhase`/`TurnNumber`/`Energy`(MaxEnergy=3)；`BeginPlayerTurn`/`EndPlayerTurn`/敌方回合流转；`CanPlayerAct`/`SpendEnergy` 阶段+能量校验。
  - `GameUi.cs` — 新增「消耗 1 点能量」「结束回合」按钮，四按钮横向排列在 `CombatActions` 容器；HUD 右上角。
  - `RunSession.cs` — `Reset` 加入 `CombatManager.End()` 回合清零。
  - `CombatManagerTests.cs` — +9 用例，共 20 用例。
- 验证：Test Runner 全绿；Play 模式能量消耗、回合流转、返回重进清零均正常。

## 2026-08-05 · A1-8 实现抽牌堆、弃牌堆与消耗区（用户已验证）
- 完成：
  - `CombatDeckTests.cs`（新）— 13 个 EditMode 用例。
  - `CombatDeck.cs` — `DiscardHand` 临时卡（`TEMP_`→消耗区）与普通卡（→弃牌堆）分离。
  - `GameUi.cs` — 新增抽牌/弃牌/消耗/临时卡按钮；手牌显示卡 ID；胜利/失败后自动 End() 清空。
  - 修复：能量 0 按钮置灰不位移；手牌满抽牌提示。
- 验证：Test Runner 全绿；抽/弃/洗回/消耗/临时卡→消耗区→胜利清空→重进不含临时卡。

## 2026-08-06 · A1-9 实现目标选择与伤害结算（用户已验证）
- 完成：
  - `CombatResolver.cs`（新）— 目标解析（6 种 TargetType）+ 伤害管线（护甲→生命→死亡→结束检查）+ `PlayTestCard`（无目标退款）。
  - `CombatManager.cs` — 新增 `RefundEnergy`。
  - `CombatResolverTests.cs`（新）— 18 个 EditMode 用例（含护甲恰好吸收/多1/批内击杀胜利）。
  - `GameUi.cs` — 剑击/横扫出牌按钮；描述显示战斗 Phase；按钮改 5×2 网格字号 16。
- 验证：Test Runner 全绿；Play 模式出牌/伤害/Victory 显示正常。

## 2026-08-06 · A1-10 实现护甲、流血、士气、疾病与疲劳（用户已验证）
- 完成：
  - `CombatStatus.cs`（新）— 状态规则统一入口：上限/每层效果/施加叠加（钳上限、疾病钳血、疲劳钳甲）/移除/回合开始流血结算。
  - `CombatUnit.cs` — 状态字段 + 有效上限属性 + `TakeTrueDamage`/`AddArmor`。
  - `CombatManager.cs` — `Morale`/`MoraleUsedThisTurn` + 方法；回合开始双方流血结算。
  - `CombatResolver.cs` — `ApplyDamage` 接入士气加成（首次 +2×层数，触发清空）。
  - `GameUi.cs` — 状态显示 + 4 个状态按钮（流血/疾病/疲劳/士气），网格 5×3。
  - `CombatStatusTests.cs`（新）— 16 个 EditMode 用例。
- 修复：CombatUnit 重复字段；士气访问权限；测试断言未考虑疾病钳血。
- 工程教训：recompile 假成功需 `AssetDatabase.Refresh(ForceUpdate)` + 验证 DLL 时间戳（已记入 Locus mistake-note）。
- 验证：Test Runner 全绿（`CombatStatusTests` 16 用例）；Play 模式状态施加/衰减/钳制/士气均正常。

## 2026-08-06 · A1-11 实现敌人意图与敌方行动（用户已验证）
- 完成：
  - `EnemyUnit.cs`（新）— `EnemyIntentExec`（Attack/AoeAttack/Defense/Plunder）+ 加权抽取 + 路匪/野犬工厂。
  - `CombatManager.cs` — 意图揭示/四种执行/默认目标最低 HP%/掠夺层/胜利掠夺结算。
  - `CombatResolver.cs` — `fromPlayer` 参数隔离敌方伤害与士气。
  - `GameUi.cs` — 敌人意图显示 + 掠夺层数。
  - `EnemyIntentTests.cs`（新）— 12 个 EditMode 用例。
- 修复：测试中非目标敌人干扰；并行会话多次回滚 sealed/virtual/Clone。
- 验证：Test Runner 全绿；Play 模式意图可见、执行匹配、同种子复现。

## 2026-08-06 · A1-12 制作 10 种 MVP 敌人与基础遭遇表（用户已验证）
- 完成：
  - `EnemyUnit.cs` — 补齐 EN03-EN10 共 10 种敌人工厂；`EnemyIntentExec` 加 BleedStacks/DiseaseStacks。
  - `CombatManager.cs` — `ApplySideEffects`（施加流血/疾病）；`ExecuteEnemyActions` 接入。
  - `EncounterConfig.cs`（新）— 9 组遭遇表（草原/密林普通+精英+首领）。
  - `RunSession.cs` — 遭遇翻页选择 + `NextEncounter`/`PrevEncounter`。
  - `GameUi.cs` — 「◀ 上一组」「下一组 ▶」按钮 + 当前遭遇名。
- 修复：翻页按钮 visibility 条件（测试页状态而非战斗活跃）；毒丝蛛流血1层确认生效（新回合开始时结算）。
- 验证：Play 模式翻页切换 9 组均正常；菌疫兽疾病 -4 上限；毒丝蛛流血生效。

## 协作规则（用户 2026-08-05 确认）
- 每步完成后：更新文档与开始下一步**分开**，均需用户明确告知后才实施。

## 进行中
- 下一步：A1-13 完成 40 张基础卡的第一版（等待用户指示开始）。


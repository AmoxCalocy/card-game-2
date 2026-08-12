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
  - `GameFlow.cs`（新）— 流程状态机：11 状态转移表、`TryTransition` 校验（非法转移拒绝且无副作用）、状态日志（上限 100 条）、`Changed` 事件。
  - `RunSession.cs` — GameState 枚举追加 NewGame/Move/Reward/Victory/Defeat/Settlement（**原枚举值不变**，兼容场景序列化）；`CurrentState` 委托 `GameFlow`；`StartNewGame` 走主菜单→新局初始化→地图两步转移；`EnterTestPage` 仅允许从主菜单进入；`DisplayName` 补齐新状态。
  - `GameUi.cs` — 订阅 `GameFlow.Changed`；HUD 第 5 行「最近状态切换」显示最近 3 条（原只显示最后 1 条，用户反馈后修复）；TestHud 高度 200→300。
  - `GameFlowTests.cs`（新）— 8 个 EditMode 用例：新游戏路径/非法转移/重复切换/四测试入口/完整路线/失败重开/结算隔离/全状态可达性。
  - 设计文档：`design/glossary.md`（术语表）、`design/game-state-flow.md`（状态流转定义）。
- 冒烟验证 + 用户验证通过；修复项：HUD 状态切换只显示最后一条 → 改为最近 3 条。

## 2026-08-05 · A0-3 制定基础数值与内容清单（用户已验证）
- 完成：
  - `design/mvp-configuration-tables.md`（已有 v0.1 完整配置表）新增 **10.1 测试用例编号规则**：TC-C01…TC-C40、TC-P01…、TC-EN01…、TC-E01…、TC-R01…、TC-B01…、TC-S01、TC-MAP-PLAINS/JUNGLE。
  - `GameStartParameters.cs`（新，原记录名 GameRules.cs 已重命名）：第一版起始参数唯一代码来源——主角 45 血/6 指令伤害、上阵 4 人、牌组 10–30、手牌 3/1/5、能量 3、起始资源（粮 14 财 30 声望 0 建材 0）、粮食不足惩罚（主角 +1 疲劳、风险 +2）、风险规则（阈值 10、危机后重置 5）、起始牌组 10 张（C01×4、C09×3、C17、C33、C36）、垂直切片目标 EN10。
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
  - `GameRandom.cs`（新）— 带种子 `System.Random` 包装：`Next`/`NextFloat`/`Shuffle`(Fisher-Yates)/`WeightedPick`（空池/零权重/负权重保护，通过 `out issue` 报告原因）。
  - `RunRecord.cs`（新）— 有序本局记录（上限 200 条），5 类：抽牌/敌人意图/地图分支/事件选项/奖励选择；`Log`/`Clear`/`Entries`。
  - `RunSession.cs` — `Random`（`GameRandom`）在 `StartNewGame`/`EnterTestPage` 用种子初始化，`Reset` 清理；`RunRecord.Clear()` 嵌入生命周期。
  - `GameUi.cs` — 场景新增 `InputField_种子`（Integer 输入）与 `Button_指定种子新游戏`（左下角固定，250+180×40），`OnStartWithSeed` 回调支持指定种子；HUD 第 7 行显示本局记录状态。
  - `GameRandomTests.cs`（新）— 12 个 EditMode 用例（同种子一致/异种子不同/洗牌确定/加权正常/空池/零权重/负权重/泛型加权）。
  - `RunRecordTests.cs`（新）— 5 个 EditMode 用例（记录顺序/清空/超上限截断重编号/中文分类名）。
- 验证：Test Runner 全绿（`GameRandomTests` 12 + `RunRecordTests` 5 与既有用例均通过）；Play 模式输入种子 `12345`→HUD 显示种子 12345；留空→随机种子。

## 2026-08-05 · A1-6 搭建战斗的初始化与结束规则（用户已验证）
- 完成：
  - `CombatUnit.cs`（新）— 战斗单位：`Id`/`DisplayName`/`MaxHp`/`CurrentHp`/`Armor`/`IsAlive`/`IsPlayerCharacter`；`TakeDamage`(护甲吸收)、`Heal`、`Clone`(独立副本)、静态工厂方法。
  - `CombatDeck.cs`（新）— 战斗独立牌堆：抽牌堆/手牌/弃牌堆/消耗区；`InitFromCampaign`(复制+洗牌)、`DrawToHand`(空堆洗回、手牌上限)、`DiscardHand`/`ExhaustFromHand`/`DiscardFromHand`、`Clone`。
  - `CombatManager.cs`（新）— 战斗生命周期：`Phase`(None→Initializing→Running→Victory/Defeat→Ended)、`Init`(克隆队伍+洗牌+初始抽牌 3 张，空队伍被拦截)、`CheckEndCondition`(全灭敌人→胜利/主角死亡→失败)、`ForceDefeat`/`End`(清理)、`RetreatAllowed=false`。
  - `RunSession.cs` — `EnterTestPage(Combat)` 调用 `InitTestCombat`：主角 45HP+阿德里安 42HP vs 路匪 28HP+野犬 22HP，10 张起始牌组。
  - `GameUi.cs` — 测试页 Combat 状态显示队伍/敌人/牌堆信息；场景新增 `Button_模拟胜利`（绿）和 `Button_模拟失败`（红），仅在 Combat 状态显示。
  - `CombatManagerTests.cs`（新）— 11 个 EditMode 用例（有效初始化/副本隔离/空玩家拒绝/空敌人拒绝/全灭胜利/主角死亡失败/运行中无结果/End 清理/新战斗不继承/撤退禁用）。
- 修复：`RunSessionTests.EnterTestPage_SpecifiedSeed` 断言更新为匹配新增的「战斗初始化」记录。
- 验证：Test Runner 全绿（`CombatManagerTests` 11 与既有用例均通过）；Play 模式测试战斗入口显示 2v2 信息，「模拟胜利」/「模拟失败」按钮正常；返回重进不残留状态。

## 2026-08-05 · A1-7 实现回合结构与共享能量（用户已验证）
- 完成：
  - `CombatManager.cs` — 新增 `TurnPhase` 枚举（PlayerTurnStart→PlayerTurn→PlayerTurnEnd→EnemyTurn→EnemyTurnEnd）、`TurnNumber`/`Energy`/`MaxEnergy=3`；`Init` 后自动 `BeginPlayerTurn`（抽 1 张、能量重置 3）；`EndPlayerTurn`→敌方回合（空）→自动下一回合；`CanPlayerAct`/`CanSpendEnergy`/`SpendEnergy` 带阶段和能量双重校验；`End`/`ForceDefeat` 清空回合状态。
  - `GameUi.cs` — 新增「消耗 1 点能量」按钮（能量>0 时可见）、「结束回合」按钮（玩家行动阶段可见）；四按钮横向排列在 `CombatActions` 容器中（150/120/150/150×36）；`BuildCombatDescription` 显示回合/能量/阶段；HUD 移至右上角。
  - `RunSession.cs` — `Reset` 加入 `CombatManager.End()` 确保返回菜单后回合状态清零。
  - `CombatManagerTests.cs` — +9 用例（回合起始全能量/结束回合流转/能量重置/能量不足拒绝/非活跃不能操作/End 清零/阶段锁定），共 20 用例。
- 验证：Test Runner 全绿（`CombatManagerTests` 20 用例）；Play 模式能量消耗→按钮消失→结束回合→新回合能量 3 按钮恢复；返回重进回合从 1 开始。

## 2026-08-05 · A1-8 实现抽牌堆、弃牌堆与消耗区（用户已验证）
- 完成：
  - `CombatDeckTests.cs`（新）— 13 个 EditMode 用例（副本独立/洗牌确定/同种子同序/弃牌堆洗回/两堆皆空停止/手牌上限/弃手牌/消耗/弃单张/Clone 独立/临时卡进消耗区）。
  - `CombatDeck.cs` — `DiscardHand` 区分临时卡（`TEMP_` 前缀→消耗区）与普通卡（→弃牌堆）。
  - `GameUi.cs` — 新增「抽 1 张牌」「弃掉手牌」「消耗最后一张」「生成临时卡」按钮加入 `CombatActions` 横向排列；手牌显示所有卡 ID 列表；`OnSimulateVictory`/`OnSimulateDefeat` 调用 `CombatManager.End()` 清空 Deck。
- 修复：「消耗 1 点能量」改为能量 0 时置灰保留位置（不位移）；按钮内 Text 关闭 raycastTarget；手牌满时抽牌提示「手牌已满」。
- 验证：Test Runner 全绿；Play 模式抽牌/弃牌/洗回/消耗/临时卡→弃掉进消耗区→模拟胜利 Deck 清空→重进不含临时卡。


## 2026-08-06 · A1-9 实现目标选择与伤害结算（用户已验证）
- 完成：
  - `CombatResolver.cs`（新）— 目标解析（六种 TargetType，仅存活单位，无目标报 issue）+ 伤害管线（护甲吸收→生命→死亡→结束检查）+ `ApplyDamage`（可读结算文本，死亡触发 CheckEndCondition）+ `PlayTestCard`（费用校验→目标解析→结算，无目标退还能量；单体只结算第一个，AOE 结算全部）。
  - `CombatManager.cs` — 新增 `RefundEnergy`（出牌失败回滚）。
  - `CombatResolverTests.cs`（新）— 18 个 EditMode 用例：五种目标范围/死亡排除/无目标报错/护甲恰好吸收/伤害多1/单体只伤一个/全体伤全部/杀最后敌人触发胜利/AOE 批内击杀触发胜利/批内跳过死目标/能量不足/无目标退款。
  - `GameUi.cs` — 「剑击 1费/单体/6伤」「横扫 2费/全体/5伤」出牌测试按钮；描述区增加「战斗：Phase」显示；战斗按钮改 5×2 GridLayoutGroup（字号统一 16）。
- 修复：`Aoe_DeadTargetInBatch_SkippedSafely` 断言 HP 写错（野犬 22 而非 28）。
- 验证：Test Runner 全绿（`CombatResolverTests` 18 用例）；Play 模式出牌扣能量、伤害、杀光敌人显示 Victory 正常。

## 2026-08-06 · A1-10 实现护甲、流血、士气、疾病与疲劳（用户已验证）
- 完成：
  - `CombatStatus.cs`（新）— 状态规则统一入口：上限常量（流血 5/士气 3/疾病 3/疲劳 3/护甲 30）、每层效果（疾病 -4 最大生命/疲劳 -5 护甲上限 -1 指令伤害/士气 +2 伤害）、施加叠加（带上限、不作用于死亡单位、疾病钳当前生命、疲劳钳护甲）、移除、`TriggerTurnStartBleed`（真实伤害=层数，伤害后 -1，致死触发结束检查）。
  - `CombatUnit.cs` — 状态字段（Bleed/Disease/Fatigue）+ `EffectiveMaxHp`/`EffectiveArmorCap`/`EffectiveCommandDamage` + `AddArmor`（钳上限）+ `TakeTrueDamage` + `Heal` 钳有效上限 + `Clone` 带状态。
  - `CombatManager.cs` — `Morale`/`MoraleUsedThisTurn` + `AddMorale`/`ClearMorale`/`MarkMoraleUsed`；`BeginPlayerTurn` 结算玩家流血、`ProcessEnemyTurn` 结算敌人流血；`End` 清空士气。
  - `CombatResolver.cs` — `ApplyDamage` 接入士气：玩家回合首次普通伤害 +2×层数，触发后清空并标记。
  - `GameUi.cs` — 描述区显示士气/流血/疾病/疲劳（HP 显示有效上限）；新增「流血+2(敌1)」「疾病+1(敌1)」「疲劳+1(主角)」「士气+2」按钮，网格扩为 5×3。
  - `CombatStatusTests.cs`（新）— 16 个 EditMode 用例（流血叠加/真实伤害/衰减/致死/死亡不施加/疾病上限与钳制/疲劳上限与钳制/士气加成与重置/多状态共存结算顺序）。
- 修复：CombatUnit 重复状态字段（清理）；`MoraleUsedThisTurn`/`Morale` 访问权限（改方法）；两个测试断言未考虑疾病钳制生命。
- 工程教训：`unity_recompile` 在 Unity 未察觉外部 .cs 修改时会"假成功"（DLL 不更新）——需先 `AssetDatabase.Refresh(ForceUpdate)` 再 recompile，或直接 Refresh 触发编译；已记入 project-mistake-note。
- 验证：Test Runner 全绿（`CombatStatusTests` 16 用例）；Play 模式流血衰减、疾病钳血、疲劳降上限、士气加成后清空均正常。

## 2026-08-06 · A1-11 实现敌人意图与敌方行动（用户已验证）
- 完成：
  - `EnemyUnit.cs`（新）— 敌人单位（继承 CombatUnit）：意图池（`EnemyIntentExec`：名称/种类 Attack/AoeAttack/Defense/Plunder/权重/伤害/护甲/掠夺层）+ `RollIntent` 加权抽取（种子驱动，可复现）+ 路匪/野犬工厂（按配置表 §5）；`Clone` 重写深拷贝意图。
  - `CombatUnit.cs` — 去 sealed、`Clone` 改 virtual（供 EnemyUnit 重写）；`CreateEnemy` 改为返回 EnemyUnit。
  - `CombatManager.cs` — `RevealEnemyIntents`（BeginPlayerTurn 揭示意图供玩家规划）；`ExecuteEnemyActions`（4 种意图执行：Attack 默认目标最低 HP%/AoeAttack 全体/Defense 护甲/Plunder 伤害+掠夺层，行动前重验目标存活，无目标默认跳过不报错）；`PickDefaultTarget`（生命百分比最低，平局主角优先）；`Plunder`/`AddPlunder`/`ClearPlunder`（0-3 层，胜利时记录掠夺结算，End 清空）。
  - `CombatStatus.cs` — 新增 `MaxPlunder` 常量。
  - `CombatResolver.cs` — `ApplyDamage` 加 `fromPlayer` 参数（敌方伤害不触发士气）。
  - `RunSession.cs` — `InitTestCombat` 改用路匪/野犬工厂（带完整意图池）。
  - `GameUi.cs` — 敌人行显示当前意图（如「意图：砍击（6 伤害）」）+ 掠夺层数。
  - `EnemyIntentTests.cs`（新）— 12 个 EditMode 用例（同种子同意图/零权重不选/首回合揭示/攻击/防御/掠夺/全体/死敌跳过/无目标不崩溃/默认目标选择/敌方不触发士气）。
- 修复：测试干扰（并行敌方行动）→ 禁用非目标敌人意图；并行会话多次回滚 sealed/virtual/Clone。
- 验证：Test Runner 全绿（`EnemyIntentTests` 12 用例）；Play 模式敌人意图可见、结束回合后 HP 扣减匹配意图、同种子重进意图复现。

## 2026-08-06 · A1-12 制作 10 种 MVP 敌人与基础遭遇表（用户已验证）
- 完成：
  - `EnemyUnit.cs` — 补齐 EN03-EN10 工厂（旱地掠手/角兽/草原劫首/毒丝蛛/菌疫兽/林间伏匪/古牙野猪/密林守望者，全部按配置表 §5 生命/意图/权重）；`EnemyIntentExec` 加 `BleedStacks`/`DiseaseStacks` 副作用字段。
  - `CombatManager.cs` — `ExecuteEnemyActions` 对 Attack/AoeAttack/Plunder 意图调用 `ApplySideEffects`（施加流血/疾病）；新增 `ApplySideEffects` 辅助方法。
  - `EncounterConfig.cs`（新）— 9 组遭遇表（草原普通×2/精英/首领，密林普通×3/精英/首领，含标签/区域/类型）。
  - `RunSession.cs` — `InitTestCombat` 按 `_testEncounterIndex` 选择遭遇；`NextEncounter`/`PrevEncounter`/`CurrentEncounterLabel` 翻页。
  - `GameUi.cs` — 测试页新增「◀ 上一组」「下一组 ▶」按钮（测试页始终可见）；`OnEnterTestPage` 显示当前遭遇名及操作提示。
- 修复：遭遇翻页按钮 visibility 条件修正（测试页状态而非战斗活跃状态）；毒丝蛛啃咬流血 1 层已验证生效（新回合开始时结算）。
- 验证：Play 模式翻页切换 9 组遭遇均正常；菌疫兽施加疾病 -4 上限；毒丝蛛啃咬施加流血（新回合开始时触发了 1 点真实伤害）。

## 协作规则（用户 2026-08-05 确认）
- 每步完成后：更新文档与开始下一步**分开**，均需用户明确告知后才实施。

## 2026-08-07 · A1-13 完成 40 张基础卡的第一版（用户已验证）
- 完成：
  - `CardDef.cs`（新）— 卡牌效果类型枚举（28 种 `CardEffectType`：Damage/GainArmor/Heal/Draw/ApplyBleed/ApplyDisease/ApplyFatigue/AddMorale/RemoveBleed/RemoveDisease/RemoveFatigue/RemoveArmor/ReduceIntent/SelfArmor/PartnerArmor/BonusDrawNextTurn/CostReduction/Exhaust/SupplyFood/FocusFire/Taunt/RemoveCapture/RemoveInjury/PartnerDamage/AllPartnerDamage/DrawThenDiscard/DiscardThenDraw/ExhaustThenDraw/Choice）、效果条件枚举（TargetBleedGE2/SelfArmorGE10）、`CardEffect` 结构（Type+P0+P1+Condition）、`CardDef` 数据类（Id/DisplayName/Cost/TargetType/Rarity/Effects）。
  - `CardCatalog.cs`（新）— 40 张卡牌静态目录：`All` 只读列表、`Find(id)`/`Exists(id)` 查找。按配置表 §3 分五类（攻击 C01–C08 / 防御 C09–C16 / 策略 C17–C24 / 战术 C25–C32 / 后勤 C33–C40），每张卡含完整效果列表。
  - `CombatResolver.cs` — 新增 `PlayCard(int handIndex, CombatUnit selectedTarget)` 完整出牌管线（费用校验→目标解析→移除手牌→逐效果结算→弃牌/消耗→结束检查）；新增 `ApplyEffect` 私有方法（28 种效果分发 + 条件检查）；`ApplyDamage` 接入集火标记（`FocusFireExtra`）。
  - `CombatUnit.cs` — 新增 `FocusFireExtra` 字段（集火标记额外伤害，回合结束时清零）。
  - `EnemyUnit.cs` — `EnemyIntentExec` 新增 `TargetsPlayer` 字段（诱饵标记：意图改为攻击主角）。
  - `CombatManager.cs` — 新增 `PendingBonusDraw`（下回合额外抽牌，回合开始结算后清零）/`CostReductionRemaining`（本回合下张牌费用减免，出牌后清零，回合结束清零）/`PlayerCharacter()`（获取存活主角）；`BeginPlayerTurn` 抽牌数合并 `PendingBonusDraw`；`ExecuteEnemyActions` 支持 `TargetsPlayer` 定向（Attack/Plunder 意图优先攻击主角）；`End`/`EndPlayerTurn` 清空新字段。
  - `GameUi.cs` — 新增 `_handCardContainer`（场景 HandCards 容器，VerticalLayoutGroup）+ `RefreshHandCards()`（动态生成手牌按钮，显示名称+费用，点击调用 `OnPlayHandCard`）+ `OnPlayHandCard(int)`（调用 `CombatResolver.PlayCard`）；`BuildCombatDescription` 手牌以 `CardCatalog.Find` 显示中文名称；`_playSingleCardButton`/`_playAoeCardButton` 改为使用手牌出牌（而非硬编码 `PlayTestCard`）。
  - 场景：`TestPage` 下新增 `HandCards` 容器（VerticalLayoutGroup），`_handCardContainer` 已连接。
  - `CardCatalogTests.cs`（新）— 27 个 EditMode 用例：目录完整性（6：数量/ID 唯一/有效查找/无效查找/Exists/起始牌组可解析）、出牌基础流程（4：消耗能量/索引无效/能量不足/弃牌堆归位/消耗区）、各类代表卡效果（14：攻击 5 + 防御 3 + 策略 3 + 战术 2 + 后勤 3）、边界条件（2：战后不能出牌/无目标）、减费/手牌操作（2）。
- 修复：SetUp 缺 `RunSession.StartNewGame(1)` 导致 Random null → NPE（已补）；C04 断言的护甲期望值错误（移除+吸收后为 0 非 5）；C17 的 handBefore 取值时机需在 Add 之后。
- 验证：Test Runner 全绿（`CardCatalogTests` 27 用例）；Play 模式手牌显示中文名称、点击出牌扣能量/造成伤害/护甲/治疗/消耗均正常。

## 2026-08-07 · A1-14 制作战斗界面与操作反馈（用户已验证）
- 完成：
  - `BattleView.cs`（新）— 战斗界面控制器：从 `_battlePagePrefab` 动态实例化 BattlePage；`Show()`/`Hide()`/`Refresh()` 生命周期；`ResolveRefs()` 从 Prefab 实例解析子组件引用；`CreateUnitCard`（按 Prefab 实例化并填充队伍/敌人数据：名称/HP/护甲/状态/意图/集火标记）；`CreateHandCard`（按 Prefab 实例化并填费用/名称/效果文字，带类型色顶栏）；`OnHandCardClicked`（单体卡选目标模式 / 无目标卡直接打出）；`OnTargetSelected`（调用 `PlayCard(handIndex, target)`）；`OnEndTurn`/`OnReturn`；`IsValidTarget`（按 TargetType 过滤高亮合法目标）。
  - Prefab 体系（4 个，全部 TMP）：`BattlePage.prefab`（五区块布局：TopBar/TeamPanel/EnemyPanel/RightPanel/BottomBar）/ `HandCard.prefab`（Cost/Name/Effect）/ `UnitCard.prefab`（队伍用）/ `EnemyCard.prefab`（敌人用）。
  - `CombatResolver.cs` — `ApplyEffect` 所有单体效果（Damage/GainArmor/Heal/状态/净化/破甲/意图削减）优先使用 `selectedTarget` 参数（用户点击目标）；条件检查逻辑优化；`ReduceIntent` 多目标分支变量引用修复（`eu`→`eu2`）。
  - `GameUi.cs` — 新增 `_battleView` 引用 + `ReturnToMenu()` 公开方法；`ShowPage` 战斗中优先显示 BattleView；`Refresh` 同步刷新 BattleView。
  - `OneJourney.Core.asmdef` — 新增 `Unity.TextMeshPro` 引用（TMP 支持）。
  - 场景：`BattlePage` 不再预置在场景中，由 `BattleView.Show()` 运行时从 Prefab 实例化到 Canvas 下。
- 修复：`Instantiate` 父节点为 `GameUi.transform` 导致 UI 不渲染→改为 `GetComponentInChildren<Canvas>().transform`；选目标时双方都高亮→`IsValidTarget` 按 TargetType 过滤；点击目标始终命中 `targets[0]`→`ApplyEffect` 优先使用 `selectedTarget`。
- 验证：Play 模式战斗界面五区块布局正常显示，手牌点击出牌/选目标/结束回合/敌方行动/返回菜单均正常。

## 2026-08-10 · A2-15 实现伙伴队伍与 4 人上阵限制（用户已验证）
- 完成：
  - `PartnerDef.cs`（新）— 伙伴静态定义（8 名：Id/DisplayName/Role/Trait/MaxHp/CommandDamage/PassiveText/JoinCardId/RecruitEventId）。
  - `PartnerRoster.cs`（新）— `PartnerState`（运行时 HP/疾病/疲劳/忠诚度/招募/上阵状态）+ `PartnerRoster` 静态管理器：`Recruit`/`SetActiveTeam`（主角+最多3名，校验招募/存活/上限）/`BuildCombatTeam(player)`/`SyncFromCombat(team)`/`InitTestRoster`/`Clear`。
  - `RunSession.cs` — `InitTestCombat` 改用 `PartnerRoster.BuildCombatTeam`；`Reset` 加入 `PartnerRoster.Clear()`。
  - `BattleView.cs` — 队伍卡片显示伙伴定位/特质（如"防护 · 坚守"）。
  - `PartnerRosterTests.cs`（新）— 15 个 EditMode 用例（数据完整性/招募/上阵上限/未招募拒绝/死亡拒绝/替换旧队伍/BuildCombatTeam 含主角/SyncFromCombat 同步 HP 状态/死亡归零/Clear 重置）。
- 验证：Test Runner 全绿；Play 模式队伍卡片显示伙伴定位/特质；战斗结束后 SyncFromCombat 同步 HP。

## 2026-08-10 · A2-16 实现战斗奖励与牌组修改（用户已验证）
- 完成：
  - `CampaignDeck.cs`（新）— 战役牌组：`AddCard`（上限 30）/`RemoveCard`/`RemoveCardAt`（下限 10）/`CloneCardList`（战斗独立副本）。
  - `RewardResolver.cs`（新）— 奖励生成：按遭遇类型（普通 5 财 2 粮 / 精英 10 财 3 粮 2 建材 / 首领 20-25 财 5 粮 3-4 建材）+ 卡牌选项（普通全普通、精英/首领至少 1 稀有，按区域池）；`ClaimCard`（领取后清空待选）/`SkipReward`/`Clear`。
  - `RunSession.cs` — 新增 `CampaignDeck` 属性，`InitTestCombat` 从战役牌组复制。
  - `CombatManager.cs` — 新增 `CurrentEncounterType`；胜利时调用 `PartnerRoster.SyncFromCombat` + `RewardResolver.GenerateRewards`，记录"奖励已生成"日志。
  - `BattleView.cs` — 结算状态（`RefreshPostCombat`）：胜利/失败提示、奖励卡牌用 `HandCard.prefab` 展示（点击领取加入牌组）、`SkipRewardButton.prefab` 跳过、`_postCombatBuilt` 防重入、`RebuildPostCombatNextFrame` 延迟一帧重建避免 EventSystem 吞点击。
  - 规则调整（用户确认）：`PickDefaultTarget` 改为队伍第一位存活单位优先（同生命百分比平局取索引小者）；`TargetType.Self`（格挡等）改为第一位存活单位。
  - Prefab：`SkipRewardButton.prefab`（新，5 个 Prefab 共 6 个）。
  - `CampaignDeckTests.cs`（新）— 12 个 EditMode 用例（牌组初始化/添加上限/移除下限/独立副本/奖励生成/领取/跳过）。
- 修复：HUD 被 BattlePage 覆盖→Show 后把 TestHud 移到最上层；`RefreshPostCombat` 重复构建按钮堆积→`_postCombatBuilt` 防重入；领取奖励后返回按钮无反应→`Destroy` 延迟销毁 + 延迟一帧重建。
- 验证：Test Runner 全绿（`CampaignDeckTests` 12 用例）；Play 模式战斗胜利显示结算+奖励卡牌领取加入牌组、跳过奖励、返回菜单正常。

## 协作规则（用户 2026-08-05 确认）
- 每步完成后：更新文档与开始下一步**分开**，均需用户明确告知后才实施。

## 2026-08-12 · A2-17 构建草原区域节点地图（用户已验证）
- 完成：
  - `RegionMap.cs`（新）— 区域节点地图：`RegionMapNode`（Id/Layer/Type/敌人池/事件池/NextIndexes）+ `RegionMap` 静态管理器：`Generate`（4 层：L1 战斗/事件/营地、L2 战斗/事件/精英、L3 战斗/事件/营地、L4 首领，层内顺序与连接由种子决定，连接保证上下层入度/出度≥1）、`TryMoveTo`（下一层/相连/未访问三重校验，失败给 reason 且状态不变，成功写 RunRecord 地图分支）、`ReachableNext`（UI 高亮）、`Clear`。
  - `RunSession.cs` — `StartNewGame`/`EnterTestPage(Map)` 生成草原地图；`Reset` 清理地图与 `RewardResolver.Clear()`。
  - `GameUi.cs` — 新字段 `_mapNodeContainer`；地图页显示节点列表按钮（当前层 ◆/可移动高亮/不可移动置灰）、`BuildMapDescription`（当前位置/路径/剩余层数/资源/风险提示）；`OnMapNodeClicked` 移动并记录。
  - 场景：`TestPage` 下新增 `MapNodes` 容器（VerticalLayoutGroup），`_mapNodeContainer` 已连接。
  - `RegionMapTests.cs`（新）— 13 个 EditMode 用例：10 固定种子结构/构成/池引用、BFS 可达首领、每节点出边≥1、无回退、第三层全连首领、同种子同图、起点仅限第一层、未连接拒绝、已访问拒绝、跨层拒绝、合法移动、完整路径到首领。
- 修复：测试 SetUp 二次 `StartNewGame` 因 Map→NewGame 非法转移被拒→`GenerateMap` 改直接调 `RegionMap.Generate`。
- 验证：Test Runner 全绿（200 用例，含新增 13）；核心逻辑冒烟：入口生成 10 节点/起点 3 可达/移动成功写记录/重复移动拒绝/清理干净。
- 修复（Test Runner 报 4 错后）：
  - 根因 1：`StartNewGame` 新增地图生成消耗种子随机序列，旧断言依赖「种子 1 手牌第一张是剑击」失效 → `CardCatalogTests` 三个用例（C01_SwordStrike/FromHand_ConsumesEnergy/NotEnoughEnergy）改为固定手牌（Clear+Add C01），不依赖随机洗牌。
  - 根因 2：`Move_FromStart_OnlyLayerOneAllowed` 循环内移动成功后当前节点已变化，后续同层节点判定失败 → 每次尝试前重新 `GenerateMap(1)`。
- 说明：移动消耗粮食与风险结算留待 A2-18；`_mapNodeContainer` 为场景引用。

## 2026-08-12 · A2-18 实现移动消耗与粮食耗尽处理（用户已验证）
- 完成：
  - `GameStartParameters.cs` — 新增风险常量：草原移动 +1、密林 +2（预留）、精英额外 +1、营地结算 -2（A2-21 用）。
  - `RunSession.cs` — 新增战役状态：`Food`/`Wealth`/`Reputation`/`Materials`（起始资源）、`Risk`（0-10）、`PlayerFatigue`（主角战役疲劳）、`AmbushPending`（危机伏击标记）；`StartNewGame`/`EnterTestPage` 初始化，`Reset` 清零；`TryMoveToNode`（移动结算：粮食消耗→不足惩罚（粮归 0、主角疲劳 +1、风险 +2）→风险增长（基础 +1、精英额外 +1）→阈值 10 重置 5 并标记伏击，返回可读结算文本，拒绝时资源不变）；`SyncPlayerFromCombat`（胜利后同步主角疲劳）；测试辅助 `SetFoodForTest`/`SetRiskForTest`。
  - `CombatManager.cs` — 胜利时调用 `RunSession.SyncPlayerFromCombat`（主角疲劳闭环）。
  - `GameUi.cs` — 地图描述显示真实资源/风险（含危机伏击警告）；`OnMapNodeClicked` 改走 `TryMoveToNode` 并展示结算。
  - `MoveCostTests.cs`（新）— 9 个 EditMode 用例：充足/刚好/无粮惩罚/连续惩罚/精英额外/拒绝不变/全程总消耗/阈值重置+伏击标记/资源初始化。
- 说明：危机伏击的强制战斗触发留待节点内容接入（A2-19 起）；营地结算 -2 留待 A2-21；密林消耗 A2-23。
- 验证：Test Runner 全绿（209 用例，含新增 9）；冒烟：移动粮 14→13 风险 0→1；无粮移动粮 0 不为负、疲劳 0→1、风险 1→4；Reset 全部归零。用户已确认四种粮食状态场景由自动化用例覆盖（测试直接 SetFoodForTest 构造，Play 模式无 UI 改粮入口）。

## 进行中
- 下一步：A2-19 实现 20 个 MVP 事件（等待用户指示开始）。

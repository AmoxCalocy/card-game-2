---
id: kd_97c777f7-a9cc-42e0-b361-a45e568afc0d
injectMode: inherit
aiEditMode: inherit
---

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

## 2026-08-12 · A2-19 实现 20 个 MVP 事件（用户已验证）
- 完成：
  - `EventCatalog.cs`（新）— 20 个事件静态目录（配置表 §6）：`EventDef`（Id/DisplayName/Description/Region/Category/Options）、`EventOptionDef`（Label/ResultText/Condition/支付/条件引用/即时结果/招募/获得卡与遗物/移除卡/升级/状态移除/事件战斗与胜利奖励）、`EventOptionCondition` 8 种条件（PayResource/HasPartnerAndReputation/HasPartnerOrReputation/HasPartnerOrCard/HasPartnerOrPartner/ReputationAtLeast/HasRemoveableCard/HasPartner）、`EventStatusChoice` 3 种状态移除（FatigueSingle/DiseaseAll/DiseaseOrFatigueSingle）；草原 E01-E10、密林 E11-E20，每事件 2-3 个选项。
  - `CampaignDeck.cs` — 初始牌组锁定（`IsInitialLockedCard`，不可被事件移除）、`HasRemoveableCard`/`RemoveableCards`（事件移除卡选项条件）、`UpgradedCards`+`UpgradeCard`（E07 升级标记，同一张卡只能升级一次）。
  - `RunSession.cs` — 事件状态机：`CurrentEvent`/`PendingEventChoice`/`PendingEventOptionIndex`/`Relics`（遗物持有记录，效果 A2-22 接入）；`StartEventFromNode`（地图事件节点按种子抽取）/`StartEvent`（测试/节点进入）/`EventOptionBlockReason`（条件校验，返回禁用原因）/`ChooseEventOption`（支付→战斗或子选择或即时结算）/`ChooseEventCard`（移除/升级）/`ChooseEventStatusUnit`（疲劳/疾病移除）/`CancelEventChoice`/`ApplyPendingEventCombatRewards`（胜利额外奖励：财/建材/声望/卡/遗物/伙伴，仅结算一次）/`ClearPendingEventCombatRewards`（失败清除）；资源钳制方法（Food/Wealth/Reputation/Materials/Risk 均带边界，声望 0-100 等）；`PlayerDisease`（主角战役疾病）；`SyncPlayerFromCombat` 同步疲劳+疾病；`StartNewGame`/`EnterTestPage(Event)` 初始化战役牌组；测试辅助 `SetWealthForTest`/`SetReputationForTest`/`SetMaterialsForTest`/`SetPlayerFatigueForTest`/`SetPlayerDiseaseForTest`。
  - `EnemyUnit.cs` — `CreateById`（EN01-EN10 工厂映射，事件战斗用）。
  - `CombatManager.cs` — 胜利时调用 `RunSession.ApplyPendingEventCombatRewards()`；主角死亡（Defeat 分支）调用 `ClearPendingEventCombatRewards`。
  - `GameUi.cs` — 新字段 `_eventOptionContainer`；事件页（标题/描述/资源显示）、`RefreshEventOptions`（选项按钮：支付标注、条件不满足置灰）、子选择渲染（移除卡/升级卡/单位列表含疾病疲劳）、`OnMapNodeClicked` 改走 `RunSession.TryMoveToNode`（修复 A2-18 UI 未接入）并在事件节点进入事件页；`BuildMapDescription` 显示真实资源（修复 A2-18 用常量显示）。
  - 场景：`TestPage` 下新增 `EventOptions` 容器（VerticalLayoutGroup），`_eventOptionContainer` 已连接。
  - `EventTests.cs`（新）— 57 个 EditMode 用例：目录完整性（20 个/ID 唯一/≥2 选项/草原 10+密林 10/引用可解析）、E01-E20 每个选项结算与条件不满足、事件战斗触发+胜利奖励（财/建材/卡/遗物/伙伴）、失败无奖励、子选择（移除卡/升级/单位状态）、已招募伙伴忠诚 +10、死亡伙伴禁用、资源钳制不为负、无效事件拒绝。
- 修复：
  - `FinishEvent` 置空 `CurrentEvent` 后仍用 `CurrentEvent.DisplayName` → NRE（三处，先存 evtName 再结算）。
  - 事件战斗胜利奖励最初只走 `ApplyEventOptionEffects`（不含 VictoryBonus* 字段）→ 独立实现 `ApplyPendingEventCombatRewards` 应用胜利额外奖励。
  - `StartEvent` 失败时未清空 `CurrentEvent`（测试入口残留事件）→ 失败置空。
  - `EnterTestPage(Event)` 随机事件编号拼接错误（E0+10=E010）→ 特判 E10。
  - 测试断言 E04 声望期望值错误（应为钳制后 0）。
- 工程教训：对同一文件的多个 edit 并行调用可能互相覆盖（旧快照回写）——大文件修改后必须 grep 验证落盘再编译（本次 evtName/字段声明两次未落盘导致假编译通过）。
- 说明：遗物仅记录持有（R01/R02 效果 A2-22 接入）；卡牌升级仅标记（升级效果后续步骤接入）；事件战斗按普通遭遇奖励+事件额外奖励结算。
- 验证：Test Runner 全绿（266 用例，含新增 57）。

## 2026-08-17 · A2-20 接入财富、声望和建筑材料资源（用户已验证）
- 完成：
  - `RunSession.cs` — `ApplyCombatRewards`（战斗胜利资源钳制入账：财富/粮食/建材，来源/变化量/变化后总量入集中结算记录）+ `LastCombatRewardText`（结算页展示，Reset 清理）。
  - `CombatManager.cs` — 胜利时调用 `ApplyCombatRewards`；`RewardResolver.SkipReward` 不再清资源（资源已在胜利时入账）。
  - 独立奖励页（代码注释 A2-20.5）：`RewardPage.prefab`（新）+ `BattleView.ShowRewardPage`（标题「战斗胜利」/资源明细/卡牌 3 选 1/跳过/继续按钮）；真实出牌胜利与模拟胜利两条路径均自动弹出；战斗页结算区精简（胜利时左上角不再显示资源文本，失败提示保留）。
  - `GameUi.cs` — 事件测试入口/地图页资源行统一为 `BuildResourceLine`（四资源含上限）；出牌与模拟胜利的记录不再覆盖「战斗奖励」结算记录（HUD 第 6 行显示最近结算）；事件测试入口补资源行。
  - `CombatRewardTests.cs`（新）— 10 个 EditMode 用例（普通/精英/首领入账数值、上限钳制、防重复、结算记录内容、事件战斗叠加、跳过不丢资源、Reset 清理）。
  - 场景：`TestPage` 按钮布局微调（用户多轮要求）：EncounterSwitchRow 上移至写入结算按钮上方、写入/返回按钮经 `SettlementRow` 容器控制间距（先紧贴后 8px）、整体上移（padding 30→20→0 + RectTransform 偏移 20px）、EncounterSwitchRow 高度 100→40（修复按钮溢出）。
- 修复（原工作区半成品遗留的 3 个测试失败）：E13 建材期望 +3→+1（事件战斗按普通遭遇，普通奖励无建材）；`Rewards_ClampedAtCap` 补建材上限前置；`EventCombatVictory_AddsNormalAndEventRewards` 战斗态无法直接进事件页（GameFlow 禁止 Combat→Event），改用 `Reset()` 回主菜单再进。
- 工程问题：`CardOptions`（无 Image/CanvasRenderer 的中间容器，仅挂 HorizontalLayoutGroup）下实例化的卡片**不渲染**——实测改色、强制 Canvas 重建、加透明/不透明 Image 均无效，reparent 到有 Image 的 RewardPage 根即正常 → 卡片改为挂奖励页根并手动布局（3 张间隔 224 居中）；`RewardDetail` 明细文字宽 200→全宽、高度 50→80→120、标题栏加高 300→360 且明细上移（避免第二行与卡片顶部重叠）。
- 验证：Test Runner 全绿（276 用例，含新增 10）；Play 模式奖励页标题/明细/卡牌/跳过/继续、失败保留战斗页均正常。

## 2026-08-17 · A2-21 实现营地与城镇一阶建筑（用户已验证）
- 完成：
  - `BuildingCatalog.cs`（新）— 5 座一阶建筑静态目录（配置表 §8）：B01 储粮帐篷（3 建材）/ B02 野战医棚（20 财 3 建材）/ B03 铁匠铺（30 财 5 建材 5 声望，需首领）/ B04 医馆（25 财 4 建材 5 声望，需首领）/ B05 市集（30 财 5 建材 5 声望，需首领），含成本/前置/效果文本。
  - `RunSession.cs` — 建筑系统：`BuiltBuildings`/`HasBuilding`/`BuildBlockReason`（成本/前置/重复三查）/`TryBuildBuilding`（成功才扣资源+记录）/`MarkGrasslandBossDefeated`（首领遭遇胜利解锁城镇建筑）/`EnterCampNode`（营地节点结算：风险 -2 + B01 首次粮食 +4）/`CampfireRest`（S01 篝火休整：移除 1 疲劳；生命 25% 部分留待战役生命系统）/`CampClinic`（B02 移除疾病）/`TownClinic`（B04 移除疾病或疲劳）/`FreeUpgradeCard`（B03 首次建成免费升级 1 张卡）；B05 事件财富首次 +5 接入 `ApplyEventOptionEffects`。
  - `CombatManager.cs` — 首领遭遇（Boss）胜利调用 `MarkGrasslandBossDefeated`。
  - `RewardResolver.cs` — 池逻辑重构为 `BuildRegionPool`（区域来源 + 建筑奖励卡 B03：C04/C11、B04：C34/C37/C40）；新增 `RewardPoolContains`（池内容直查）。
  - `GameUi.cs` — 营地页：`ShowCampPage`/`RefreshCampButtons`（篝火休整/查看牌组（含数量与升级标记 ★）/建筑建造与服务入口/免费升级/离开营地，子选择模式 Rest/ClinicCamp/ClinicTown/FreeUpgrade/DeckView）；`OnMapNodeClicked` 营地节点进入（状态转移 Move→Camp）；营地页隐藏测试按钮行与其他动态容器。
  - `BattleView.cs`/`BattlePage.prefab` — 战斗页顶部新增「◀ 上一组 / 下一组 ▶ / 模拟胜利 / 模拟失败」按钮（TestPage 在战斗中隐藏，原按钮不可见）；模拟胜利弹奖励页、失败留战斗页。
  - 场景：`CampOptions` 容器（Canvas 固定中部面板，营地页显示）；TestHud 文本 raycastTarget 关闭（拦截战斗页按钮点击）。
  - `BuildingTests.cs`（新）— 20 个 EditMode 用例（目录完整性/建造成功扣费/资源不足拒绝/重复建造/首领前置/首领后建造/B01 首次粮/B01 二次不给/营地风险 -2/篝火休整/医棚/免费升级/建筑卡跨区域进池（用户补充 2 例）/B05 事件财富 +5/Reset 清理）。
- 修复：`RelaunchTestCombat`（状态已在 Combat 时直接重开，绕过 Combat→Combat 非法转移）；遭遇索引负值越界（安全取模）；首领击败标记跨测试入口保留（Reset 不清、StartNewGame 清零——战斗胜利 → 营地页联动）；营地测试入口补调试资源（建材 10/财富 50/声望 10）便于 Play 验证建造。
- 说明：城镇建筑前置「草原首领已击败」由首领遭遇胜利置位；城镇独立页面入口留待 A2-23 区域过渡；存档持久化留待 A2-25；S01 生命恢复与 B02 受伤移除留待战役生命系统。
- 验证：Test Runner 全绿（296 用例，含新增 20）；Play 链路：战斗测试 → 首领胜利 → 营地页城镇建筑解锁可建造 ✓。

## 2026-08-20 · A2-22 制作 8 件 MVP 遗物（用户已验证）
- 完成：
  - `RelicCatalog.cs`（工作区已有，补全）— 8 件遗物静态目录（配置表 §7）：R01 旅人罗盘（地图显示全部节点，MVP 天然生效）/ R02 铁锅（每区域首次进营地粮 +4）/ R03 琥珀护符（每场战斗开始全队 +3 护甲）/ R04 医师药箱（每区域首次进营地移除疾病或疲劳）/ R05 商队印记（每区域首次事件财富 +5）/ R06 狼牙坠饰（每场首次普通伤害 +3）/ R07 指挥旗（每场首张战术卡 -1 费）/ R08 不熄灯（BossOnly，首领战开始 +2 士气）。
  - `RunSession.cs` — `AddRelic`/`HasRelic`/`AddRelicForTest`；R02 接入 `EnterCampNode`（与 B01 叠加 +8）；R04 `RelicClinicAvailable`/`RelicClinic`（区域级一次）；R05 接入 `ApplyEventOptionEffects`（与 B05 市集叠加，事件财富首次 +10）。
  - `CombatManager.cs` — `ApplyCombatStartRelics`（战斗开始：R03 全队 +3 护甲、R08 首领战 +2 士气）；战斗级标记 `RelicWolfUsedThisCombat`/`RelicFlagUsedThisCombat`（End 重置）；`Init` 内 `CurrentEncounterType` 先于战斗开始应用（R08 判断首领战）；RunSession 两处战斗初始化改为先设遭遇类型再 Init。
  - `CombatResolver.cs` — R06（首次玩家伤害 +3，需持有遗物 + 战斗级标记）+ `RelicWolfBonus` 常量；R07（首张战术卡 C25-C32 减 1 费，最低 0）+ `IsTacticalCard`。
  - `RewardResolver.cs` — **遗物奖励接入**（配置表 §5.1 此前未实现）：精英 +2 件、首领 +3 件（`GenerateRelicOptions`，未持有池、已持有不重复、BossOnly 仅首领出）；`RewardOption.RelicId` + `ClaimRelic`。
  - `BattleView.cs`/`RelicReward.prefab`（新）— 奖励页金色遗物条目（prefab：名称 + 效果文字，TMP 字体内置，替代动态创建修复 NRE）；点击领取加入 `RunSession.Relics`。
  - `GameUi.cs` — 营地页 R04 医师药箱服务入口（`ClinicRelic` 子模式）。
  - `RelicTests.cs`（新）— 26 个 EditMode 用例（目录 8 件唯一/R08 仅首领、每件触发时点与一次性、R02+B01 与 R05+B05 叠加、遗物奖励生成/领取/不重复、战斗级标记重置、Reset 保留 + 新局清零）。
- 修复：
  - R06 缺 `HasRelic` 检查 → 污染全部玩家伤害测试（+3 无条件触发）；`_testEncounterIndex` 未随 Reset 归零 → 翻页索引跨测试泄漏。
  - R08 测试 while 死循环（`NextEncounter` 不更新 `CurrentEncounterType`）→ 固定翻页；曾卡死 Unity 主线程，用户重启后恢复。
  - 奖励页清理条件 `StartsWith("Reward_")` 匹配不到遗物名 `RewardRelic_*` → 改 `StartsWith("Reward")`（点击遗物后残留两个条目的 bug）。
  - 遗物跨测试入口保留策略：`Reset`/`InitCampaignResources` 不再清空 `Relics`（与首领标记一致，测试入口共享战役进度），仅 `StartNewGame` 清零；四个测试类 SetUp 补显式隔离；`Reset_ClearsRelics` → `Reset_KeepsRelics_NewGameClears`。
  - 事件页出现「剑击+费用」手牌按钮：`ShowPage` 非营地页强制显示 `HandCards` 容器覆盖了 `RefreshHandCards` 的「仅战斗显示」→ 改为仅营地页强制隐藏。
  - 遗物条目动态创建 NRE（`_rewardTitleText.font` 为 null）→ 改 prefab 方案。
- 说明：多遗物叠加测试已覆盖 R02+B01、R05+B05；R06+士气、R07+C19 叠加未补测（用户确认不需要）；「重新读档不重复触发」留待 A2-25 存档；Play 无遗物调试入口（用户确认不需要，仅靠精英/首领随机奖励获得）。
- 验证：Test Runner 全绿（318 用例，含新增 26）；Play：精英/首领胜利奖励页金色遗物条目（prefab 名称+效果）、点击领取无报错、卡牌与遗物同时清理、持有遗物后首领战士气 2 / 营地页医师药箱入口 / R05 事件财富 +5 均生效 ✓。

## 2026-08-20 · A2-23 构建密林区域与区域切换（用户已验证）
- 完成：
  - `RegionMap.cs` — `Generate` 支持密林（配置表 §9）：L1 战斗/事件/营地、L2 战斗/事件/精英、L3 战斗/事件/营地、L4 首领；敌人池草原 EN01/02/04 + EN03 + EN05 / 密林 EN06/07/09 + EN08 + EN10；事件池 E01-E10 / E11-E20；层内随机、连通性不变。
  - `RunSession.cs` — `StartNodeCombat`（战斗/精英/首领节点按种子抽敌人→进入战斗，遭遇类型 Normal/Elite/Boss，伏击优先）；`StartAmbushCombat`（§9.1：草原 EN01+EN02 / 密林 EN06+EN08，按精英奖励结算，触发后清标记）；`AdvanceToNextRegion`（草原首领胜利→密林：保留牌组/伙伴/资源/遗物/建筑，重置风险与区域级一次性标记，Combat→Reward）；`RegionDisplayName`（奖励区域池）；`TryMoveToNode` 补 Map→Move 状态转移（幂等，支持连续移动）。
  - `CombatManager.cs` — 胜利奖励区域化 `RewardResolver.GenerateRewards(type, RegionDisplayName())`；首领胜利调用 `AdvanceToNextRegion`。
  - `GameUi.cs` — `OnMapNodeClicked` 战斗/精英/首领节点进入节点战斗、事件节点伏击优先；`BuildMapDescription` 区域化（区域名/风险提示/伏击警告 ⚠）；新增 `ReturnToMap()`（奖励结算/区域切换后继续地图，BattleView 继续按钮按是否有地图分流）。
  - `RegionTransitionTests.cs`（新）— 8 个 EditMode 用例：密林结构/池引用/连通性（10 种子）、节点战斗、精英类型、危机伏击（含精英奖励建材 +2）、草原首领胜利→密林保留战役状态（牌组/遗物/建筑/首领标记/风险重置/粮食+首领奖励）、密林移动消耗（粮 -2 风险 +2）。
- 修复：
  - `TryMoveToNode` 缺状态转移 → 补 Map→Move 且 Move 态幂等（连续移动/测试链不中断；曾导致 MoveCostTests 4 例失败）。
  - 测试断言：首领胜利保留粮食需含首领奖励 +5 粮；`CombatNode_Start` 断言状态 Combat（修复状态转移后恢复）。
- 说明：危机伏击的密林版本（EN06+EN08）已实现但 Play 触发路径依赖风险累计；密林首领垂直切片结局留待 A2-24。
- 验证：Test Runner 全绿（326 用例，含新增 8）；Play：新游戏逐层到首领→胜利→奖励页→继续→密林地图（保留战役状态）✓。

## 2026-08-20 · A2-23 补充：奖励页卡牌与遗物各选一个（用户已验证）
- `RewardResolver.ClaimCard` 领取后只移除卡牌选项（遗物保留）、`ClaimRelic` 只移除遗物选项（卡牌保留）；两者都选完（或跳过）→ 继续按钮。
- 测试：`Reward_ClaimCardThenRelic_BothGranted`（先卡后遗物都入账）、`ClaimCard_Elite_KeepsRelicOptions`（领卡后遗物仍在）、`Reward_ClaimRelic_AddsToRunSession` 更新（领遗物后卡牌保留）。
- 验证：328 用例全绿；Play 精英/首领胜利先领卡再领遗物、两者都消失后出现继续按钮 ✓。

## 2026-08-24 · A2-24 定义 MVP 单局结局与结算页（用户已验证）
- 完成：
  - `RunSession.cs` — 结算系统：`SettlementSummary`（结果/原因/用时/区域进度/最终牌组/伙伴/资源/建筑/遗物/种子）+ `LastSettlement` 快照 + `EnterSettlement(victory, reason)`（Victory/Defeat→Settlement 转移）+ `EnterVictoryState`（密林首领胜利）+ `EnterDefeatState`（主角死亡）+ `EnterRewardState`（普通/精英胜利→Reward）+ `MarkSessionStart`（会话计时）+ `RegionMapRegion`；`RestartWithSameSeed` 流程。
  - `CombatManager.cs` — 胜利分支统一状态转移：首领（密林→Victory / 草原→切区域）、普通/精英→`EnterRewardState`（修复战斗胜利后状态残留 Combat 的 bug）；主角死亡分支自动 `EnterDefeatState`。
  - `GameUi.cs` — `ShowSettlement`（摘要文本）+ `RefreshSettlementButtons`（结算页真实按钮：返回主菜单 / 同种子重开，复用营地按钮容器）+ `RestartWithSameSeed`（用本局种子新开局）；结算态显隐（隐藏测试按钮行，显示结算按钮容器）。
  - `BattleView.cs` — `OnSimulateDefeat` 对齐完整结算逻辑（ForceDefeat→End→Defeat→结算页）；`RefreshPostCombat` 真实主角死亡自动进结算页；`OnRewardContinue` 分流 Victory→结算 / Reward+地图→ReturnToMap / Reward 无地图→ReturnToMenu。
  - `SettlementTests.cs`（新）— 5 个 EditMode 用例：密林首领胜利→Victory→结算、草原首领胜利→切区域（非 Victory）、主角死亡→Defeat→结算、结算摘要（资源/牌组/建筑/遗物/种子）、Reset 清摘要。
- 修复：
  - `StartNodeCombat`/`StartAmbushCombat` 未检查 `TryTransition(Combat)` 结果 → Reward 态进入战斗静默失败（密林首领胜利无法进 Victory）；补转移失败 End+return false。
  - 模拟失败按钮绑 BattleView 旧逻辑（只显示失败文本不进结算页）→ 对齐完整结算流程。
  - 战斗胜利后 GameFlow 状态残留 Combat（TestPage 半战斗态、按钮消失）→ 胜利统一转移 Reward/Victory。
  - 结算页「返回主菜单/同种子重开」从描述文字改为真实按钮（复用营地容器）。
- 说明：正式战役胜利（击败边境首领）留待完整功能阶段；同种子重开走 StartNewGame（清空战役进度）。
- 验证：Test Runner 全绿（333 用例，含新增 5）；Play 完整链路：新游戏→草原首领胜利→切密林→密林首领胜利→奖励页→结算页（摘要+真实按钮）→返回主菜单/同种子重开 ✓；模拟失败→结算页 ✓；真实主角死亡→结算页 ✓。

## 2026-08-31 · A3-25 实现安全存档与继续游戏（用户已验证）
- 完成：
  - `CampaignSaveData.cs`（新）— 版本 1 战役存档 DTO：安全检查点（Map/NodeEntry/Camp）、随机状态、累计用时、资源/风险、牌组与升级、8 名伙伴状态与上阵顺序、遗物/建筑/事件标记、区域级一次性标记、完整地图拓扑/路径及本局记录。
  - `CampaignSaveService.cs`（新）— 本地 `Application.persistentDataPath` JSON 存档；外层版本号 + SHA-256 完整性哈希；临时文件写入后替换主档并保留备份；主档损坏时回退备份并恢复主档；损坏/缺字段/旧版本均明确拒绝；结算后删除活动存档；测试可注入隔离目录。
  - `GameRandom.cs` — 从不可读取状态的 `System.Random` 包装改为兼容经典序列的显式状态实现；新增 `GameRandomState`、`CaptureState`/`TryCreate`，读档后继续原随机序列而非仅按种子重置。
  - `RunSession.cs` — 新增 `EventFlags`、完整存档捕获/事务恢复、`TryContinue`、节点入口恢复、`CompleteRewardAndReturnToMap` 与安全点校验；新游戏/地图节点、事件完成、营地进入与操作、奖励完成后自动存档；战斗/事件选择/奖励选择中不覆盖检查点，继续时从节点内容开头重启；结算时清理战斗并删除存档。
  - `RegionMap.cs` / `PartnerRoster.cs` / `RunRecord.cs` — 分别新增地图拓扑与路径、伙伴全状态与上阵顺序、本局记录的快照/恢复；`GameFlow.cs` 新增仅允许 Map/Move/Camp 的受限安全状态恢复。
  - `GameBootstrap.cs` — 启动初始化存档状态；`GameUi.cs` — 绑定继续游戏、存档状态提示及按恢复状态分流页面；奖励完成返回地图前先彻底结束战斗并保存。
  - `Assets/Scenes/SampleScene.unity` — 主菜单新增「继续游戏」按钮与存档状态文字，`GameUi._continueButton` / `_saveStatusText` 引用已连接。
  - `CampaignSaveTests.cs`（新）— 13 个 EditMode 用例：完整状态与随机序列往返、事件/战斗/营地节点入口恢复、事件/营地/奖励安全点自动存档、战斗中拒绝覆盖、主档损坏回退备份、双档损坏/缺字段/旧版本拒绝、结算删除存档。
  - `GameRandomTests.cs` — +2 用例：随机内部状态往返、与经典 `System.Random` 序列兼容。
- 安全语义：磁盘始终只保留完整战斗外状态；进入节点后先保存 NodeEntry，若在战斗、事件或奖励中退出，则继续游戏会用保存时 RNG 从该节点内容开头重启；营地入口结算后升级为 Camp 检查点，避免重复触发区域首次效果。
- 验证：Unity 与生成项目程序集编译 0 错误（仅既有 `PlayTestCard` 过时警告）；用户确认 Test Runner 与功能验证通过。

## 2026-09-01 · 界面优化：营地与事件页 Prefab 化（用户已验证）
- 营地页：
  - `Assets/Prefabs/CampOptions.prefab`（新）— 营地面板从场景对象转换为 Prefab 实例，采用左侧队伍状态/目标选择、右侧设施与建筑入口的双栏滚动布局；结算按钮区拆为场景独立 `SettlementActions`，不再与营地内容共用层级。
  - `CampTeamCard.prefab` + `CampTeamCardView.cs`（新）— 可复用伙伴卡：头像占位、名称、上阵/后备/阵亡、HP/忠诚、疲劳/疾病及最多两个操作按钮；主角和全部已招募伙伴均由同一 Prefab 填充。
  - `CampFacilityCard.prefab` + `CampFacilityCardView.cs`（新）— 可复用设施卡：图标占位、名称、成本/条件/效果、锁定/已建/可服务状态及点击入口；篝火、牌组、B01-B05、R04 与离开营地均复用该 Prefab。
  - 营地与独立结算范围的文字全部改为 `TextMeshProUGUI`，统一使用 `Assets/Fonts/SIMHEI SDF.asset`，消除中文缺字；`GameUi.cs` 从纯代码搭层级改为实例化两个卡片 View。
- 事件页：
  - `Assets/Prefabs/EventPage.prefab` + `EventPageView.cs`（新）— 杀戮尖塔式整体框架：左侧事件插画占位，中间标题/叙事/资源/当前提示，右侧可滚动选项列表；场景旧 `EventOptions` 被该 Prefab 实例替代。
  - `Assets/Prefabs/EventOptionCard.prefab` + `EventOptionCardView.cs`（新）— Across the Obelisk 式角色/条件选项卡：角色或类型徽标、选项名、条件/成本、预期结果、锁定原因；支持普通选项、事件战斗、伙伴条件以及移除卡/升级卡/状态治疗子选择。
  - `GameUi.cs` — 保留 E01-E20、上一事件/下一事件、地图事件、事件战斗和全部子选择业务逻辑；事件状态下隐藏旧标题/描述/战斗按钮区，事件结束后恢复原页面布局。
- 修复：
  - `RunSession.EventOptionBlockReason` 新增状态目标校验：E10 无疲劳目标时禁用「休整」；E14 全队无疲劳/疾病时禁用「配药」并显示明确原因，不再进入空子选择导致卡死，仍可选择「采药出售」完成事件。
  - `EventTests.cs` +2 回归用例：E10 无疲劳目标拒绝、E14 无可治疗目标拒绝且可改选出售；事件测试共 59 个。
- 验证：核心程序集编译 0 错误；Play 冒烟覆盖营地伙伴/设施 Prefab、休整/建造、事件 E01/E03/E07/E14、锁定提示与子选择；完整 `EventTests` 59/59 通过；用户确认布局、交互和修复均通过。

## 2026-09-02 · 界面优化：地图、失败、战斗与奖励页（用户已验证）
- 地图页：
  - `Assets/Prefabs/MapPage.prefab` + `MapPageView.cs`（新）— 将旧纵向节点按钮清单替换为完整区域地图：顶部集中显示区域、层数、四资源与风险提示；下方全宽路线区显示起点、四层节点与连接线，当前路径/可达路线高亮，未来路线弱化；移除“旅程信息”和独立“路线选择”侧栏。
  - `Assets/Prefabs/MapNode.prefab` + `MapNodeView.cs`（新）— 可复用节点卡：战斗/事件/营地/精英/首领徽标，未来/可达/当前/已访问四种状态；可达节点首次点击显示“再次点击前往”，第二次点击才执行移动，不再显示“下一层选择”按钮。
  - `GameUi.cs` / `Assets/Scenes/SampleScene.unity` — 旧 `MapNodes` 运行时按钮生成被 `MapPageView.SetMap` 取代，场景持有 `MapPage` Prefab 实例；地图推进、事件/营地/战斗分流、存档规则均未改变。
- 失败页：
  - `Assets/Prefabs/FailurePage.prefab` + `FailurePageView.cs`（新）— Canvas 直属全屏覆盖层，失败卡严格位于画面中心，显示失败原因、区域/用时/种子，提示下方提供“开始新游戏”按钮。
  - `GameUi.cs` — 失败结算单独分流到 `FailurePageView`；按钮先 `RunSession.Reset()` 再开启随机新局，确保旧地图/结算清空；胜利结算仍沿用原摘要与按钮流程。
- 战斗与奖励页：
  - `BattlePage.prefab` / `UnitCard.prefab` / `EnemyCard.prefab` / `BattleView.cs` — 在不改 RectTransform、布局组和战斗规则的前提下统一为深色底、金色强调、友方蓝/敌方红层级；运行时单位、状态、意图、手牌、选卡和目标高亮同步使用边框与统一 TMP 字体。
  - `RewardPage.prefab` / `RelicReward.prefab` / `BattleView.ShowRewardPage` — 顶部展示资源入账与当前资源；中央将 3 张卡牌和遗物拆为独立区，卡牌显示攻击/防御/策略/战术/后勤及稀有度，遗物使用横向金色槽；领取后按剩余类别自动居中，两类均处理后显示完成提示与“继续旅程”。
  - 奖励卡直接实例化 `HandCard.prefab`，不再覆盖 RectTransform 或 LayoutElement；战斗手牌与奖励卡均保持 Prefab 原始 `200×300`、缩放 1、相同布局参数，类型/稀有度以卡名行小字显示，避免挤占效果文本。
- 验证：普通/精英/首领奖励、先卡后遗物、放弃剩余奖励、完成提示、真实地图战斗返回地图、失败后新开局、战斗选卡/选目标/结束回合均通过；奖励相关 `CombatRewardTests` + `CampaignDeckTests` + `RelicTests` 共 47/47 通过，Console 0 错误；用户确认本轮页面与交互通过。

## 进行中
- 下一步：A3-26 完成基础引导与规则说明（等待用户明确指示开始）。

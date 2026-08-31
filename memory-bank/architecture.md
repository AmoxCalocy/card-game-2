---
id: kd_16876443-f2bb-491f-9661-53b0cba3b249
injectMode: inherit
aiEditMode: inherit
---

# 架构与文件职责（截至 2026-08-31）

## 目录与程序集
- `Assets/Scripts/Core/` — 核心运行时逻辑（程序集 OneJourney.Core，无外部引用）。
- `Assets/Tests/EditMode/` — EditMode 测试（OneJourney.EditModeTests.asmdef，NUnit 风格）。

## 运行时入口与场景
- `GameBootstrap.cs` — `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`：依次初始化 `GameConfigProvider`、`ContentRegistry.LoadAll()`、`RunSession.Reset()` 与 `CampaignSaveService.Initialize()`。**不创建 UI**——UI 由场景承载（MVP 单场景，UI 不跨场景存活）。
- `Assets/Scenes/SampleScene.unity` — 唯一主场景：Main Camera + GameUi 层级（GameUi / Canvas / TestHud / MainMenu / TestPage / EventSystem）；MainMenu 内含场景化「新游戏 / 继续游戏 / 存档状态」控件，引用由 `GameUi` 序列化持有。

## 配置系统
- `GameMode.cs` — 枚举 Development / Testing / Release。
- `GameConfig.cs` — ScriptableObject：`_mode`、`_showTestHud`、`_enableTestEntries`；静态工厂 `Create(mode, showTestHud, enableTestEntries)`。
- `GameConfigProvider.cs` — 静态提供者：按启动参数（`-releaseMode` 锁定、`-testMode`）或 `Application.isEditor` 决定启动模式；从 `Resources/Configs/GameConfig_<Mode>.asset` 加载，缺失时回退 `GameConfig.Create` 默认值并打警告；`IsReleaseLocked` 时忽略非 Release 切换。
- `Assets/Data/Resources/Configs/GameConfig_{Development,Testing,Release}.asset` — 三套配置资产（Release：HUD 与测试入口均隐藏；开发/测试：均开启）。

## 会话与流程
- `GameFlow.cs` — 流程状态机：`CurrentState`（唯一状态源）、11 状态转移表（`IsAllowed`）、`TryTransition(to, reason)`（非法转移拒绝且无副作用并打警告）、状态日志 `Log`（上限 100 条，`LastTransition` 取最新）、`Reset` 清空日志、`Changed` 事件；A3-25 的 `RestoreSafeState` 仅允许读档恢复到 Map/Move/Camp，绕过普通转移表但仍重建一条可读恢复日志。`GameState` 枚举值：None/MainMenu/Combat/Map/Event/Camp/NewGame/Move/Reward/Victory/Defeat/Settlement（新状态为追加，旧值不变）。
- `RunSession.cs` — 静态会话数据：随机种子（支持 `-seed <n>` 与 UI 种子输入）、`CurrentState` 委托 `GameFlow`、结算记录列表（上限 20 条）；`StartNewGame`（含阻塞检查 + `GameRandom` 初始化 + `RunRecord.Clear()` + `InitCampaignResources()` + `RegionMap.Generate(Plains)`）、`EnterTestPage`（Combat 时调用 `InitTestCombat` 创建测试战斗；Map 时生成地图；Camp 时补建造调试资源）、`RecordResolution`、`Reset`（清理 `Random`/`RunRecord`/`Records`/`GameFlow`/`PartnerRoster`/`RewardResolver`/`RegionMap`/资源/风险/疲劳；**遗物与首领击败标记跨测试入口保留**，仅新游戏 `StartNewGame` 清零）；`Random` 静态属性；`Changed` 事件驱动 UI 刷新。
- 战役状态（A2-18）：`Food`/`Wealth`/`Reputation`/`Materials`（起始资源）、`Risk`（0-10 区域风险）、`PlayerFatigue`（主角战役疲劳）、`AmbushPending`（危机伏击标记）；`TryMoveToNode(nodeIndex)`（移动结算：Map→Move 状态转移（幂等）→粮食消耗（草原 1/密林 2）→不足惩罚（粮归 0/主角疲劳 +1/风险 +2）→风险增长（基础 +1/+2、精英额外 +1）→阈值 10 重置 5 并标记伏击，返回可读文本，拒绝时资源不变）；`SyncPlayerFromCombat`（胜利回写主角疲劳+疾病）；测试辅助 `SetFoodForTest`/`SetRiskForTest`/`SetWealthForTest`/`SetReputationForTest`/`SetMaterialsForTest`/`SetPlayerFatigueForTest`/`SetPlayerDiseaseForTest`。
- 节点战斗与区域切换（A2-23）：`StartNodeCombat(node)`（战斗/精英/首领节点按种子抽敌人→进入战斗，遭遇类型 Normal/Elite/Boss，伏击优先；**状态转移失败检查**，失败 End+return false）、`StartAmbushCombat`（§9.1：草原 EN01+EN02 / 密林 EN06+EN08，按精英奖励结算）、`AdvanceToNextRegion`（草原首领胜利→密林：保留牌组/伙伴/资源/遗物/建筑，重置风险与区域级一次性标记，Combat→Reward）、`RegionDisplayName()`（奖励区域池）。
- 结局与结算（A2-24）：`SettlementSummary`（结果/原因/用时/区域进度/最终牌组/伙伴/资源/建筑/遗物/种子）+ `LastSettlement` 快照 + `EnterSettlement(victory, reason)`（Victory/Defeat→Settlement）+ `EnterVictoryState`（密林首领胜利）/`EnterDefeatState`（主角死亡）/`EnterRewardState`（普通/精英胜利）+ `MarkSessionStart`（会话计时）+ `RegionMapRegion`；胜利分支统一状态转移（首领密林→Victory、草原→切区域、普通/精英→Reward，修复战斗胜利后状态残留 Combat）；A3-25 起进入结算时先清理战斗临时态并删除活动存档，避免结局后继续到旧检查点。
- 存档协调（A3-25，`RunSession.cs` 内）：`CanCaptureCheckpoint`/`CaptureSaveData`/`TryRestoreFromSaveData` 负责把各静态系统聚合为完整战役快照并在全量校验后一次性恢复；`TryContinue` 按 Map/NodeEntry/Camp 分流，`CompleteRewardAndReturnToMap` 保证奖励完成→清战斗→回地图→存档的顺序；`EventFlags` 保存已完成事件 ID。自动存档调用只放在明确的领域完成点，不订阅通用 `Changed` 事件。
- `GameStartParameters.cs` — 第一版起始参数与全局规则常量（唯一代码来源，对应《MVP 配置表》）：主角 45 血/6 指令伤害、上阵 4 人、牌组 10–30、手牌 3/1/5、能量 3、起始资源（粮 14 财 30 声望 0 建材 0）、粮食不足惩罚（+1 疲劳、风险 +2）、风险常量（阈值 10、危机后重置 5、草原/密林移动 +1/+2、精英额外 +1、营地结算 -2）、起始牌组 10 张（C01×4、C09×3、C17、C33、C36）、垂直切片目标 EN10。

## 内容系统
- `ContentBase`（`ContentModels.cs`）— 内容数据基类 `ScriptableObject`（id/displayName/description）。七类内容：`CardData`（卡牌，cost 0–4、targetType、effectText）、`PartnerData`（伙伴，maxHp 1–100、commandDamage 0–20、role/joinCardId）、`EnemyData`（敌人，maxHp 1–200、intents）、`EventData`（事件，options≥2）、`RelicData`（遗物，effectText）、`NodeData`（节点，enemyPoolIds/eventPoolIds）、`BuildingData`（建筑，四项成本 0–999、effectText）。
- `ContentCatalog.cs` — 静态 ID 清单与引用关系表（伙伴加入卡/建筑解锁卡/事件授予卡/事件战斗敌人），与《MVP 配置表》保持一致。
- `ContentValidator.cs` — 校验纯函数：必填/范围/引用/ID 唯一；`ValidationIssue` 结构可定位到类型[ID]字段。`ContentRegistry`：从 `Resources/Content` 加载全部资产并校验；`HasBlockingIssues` 为 true 时 `StartNewGame` 被阻止；`Clear()` 供测试隔离与修复后重加载。
- `ContentValidationTests.cs` — 12 个 EditMode 用例覆盖全部校验场景，`LogAssert.Expect` 声明预期错误日志。
- `design/content-validation-spec.md` — 校验规范（必填字段表、校验流程、错误格式、新增内容要求）。

## 随机与记录
- `GameRandom.cs` — 可复现且可持久化的随机数器：显式实现与经典 `System.Random` 兼容的内部 56 项状态，提供 `Next`/`NextFloat`/`Shuffle`/`WeightedPick`；A3-25 新增 `GameRandomState`、`CaptureState`/`TryCreate`，使读档后从原随机序列继续，而不是只用种子重开序列。
- `RunRecord.cs` — 本局详细记录（上限 200 条）：5 类 `RecordCategory`（抽牌/敌人意图/地图分支/事件选项/奖励选择）+ 一般；`Log` 写入有序条目、`Clear` 清空、`Entries` 只读访问；溢满时截断并重新编号；A3-25 的 `CaptureSaveData`/`RestoreSaveData` 使继续游戏保留可复现记录。
- `GameRandomTests.cs` — 14 个 EditMode 用例：同种子/异种子、洗牌、加权边界、内部状态往返及经典 `System.Random` 序列兼容。
- `RunRecordTests.cs` — 5 个 EditMode 用例：记录顺序/清空/超上限截断/分类名。

## 安全存档系统（A3-25）
- `CampaignSaveData.cs` — 纯 `[Serializable]` DTO 定义，不执行文件 IO 或游戏逻辑。`CampaignSaveData` 聚合检查点、保存时间、种子与 RNG 内部状态、累计用时、资源、牌组与升级、伙伴与上阵顺序、遗物、建筑、事件标记、区域级一次性标记、地图拓扑/路径和本局记录；`SaveCheckpointKind` 仅有 Map / NodeEntry / Camp 三种安全恢复语义。
- `CampaignSaveService.cs` — 存档基础设施边界：路径固定在 `Application.persistentDataPath`，外层 `SaveEnvelope` 包含结构版本与 SHA-256；写入先落临时文件，再替换主档并保留备份；读取顺序为主档→备份，备份成功时恢复主档；`CampaignSaveValidator` 在修改运行时状态前检查关键字段、范围、静态目录 ID、队伍、牌组、地图连通性与检查点一致性。文件损坏、字段缺失或版本不支持只返回明确错误，不钳制或猜测修复。
- 安全点语义：新游戏地图、移动完成后的节点入口、事件完成、营地入口/操作、奖励全部完成并返回地图后写盘。NodeEntry 保存发生在随机抽取节点内容之前，因此战斗/事件/奖励中退出不会保存半结算状态，继续时会恢复 RNG 并从同一节点内容开头重启；Camp 检查点在入口效果结算后覆盖 NodeEntry，避免区域首次效果重复触发。
- 明确不持久化：战斗单位副本、手牌/抽牌堆/弃牌堆/消耗区、回合/能量/护甲/流血/敌人意图、事件待选项、待领奖励和 UI 实例。它们要么由 NodeEntry 重新创建，要么必须在升级为下一安全点前完整清理。
- `CampaignSaveTests.cs` — 13 个 EditMode 用例：全状态与 RNG 往返、三类节点入口恢复、事件/营地/奖励自动存档、战斗中拒绝覆盖、主档回退、双档损坏、缺字段、旧版本及结算删档；测试通过可注入目录隔离真实玩家存档。

## 卡牌系统（A1-13）
- `CardDef.cs` — 卡牌效果类型枚举（28 种 `CardEffectType`：Damage/GainArmor/Heal/Draw/ApplyBleed/ApplyDisease/ApplyFatigue/AddMorale/RemoveBleed/RemoveDisease/RemoveFatigue/RemoveArmor/ReduceIntent/SelfArmor/PartnerArmor/BonusDrawNextTurn/CostReduction/Exhaust/SupplyFood/FocusFire/Taunt/RemoveCapture/RemoveInjury/PartnerDamage/AllPartnerDamage/DrawThenDiscard/DiscardThenDraw/ExhaustThenDraw/Choice）、效果条件枚举（TargetBleedGE2/SelfArmorGE10）、`CardEffect` 结构（Type/P0/P1/Condition）、`CardDef` 数据类（Id/DisplayName/Cost/TargetType/Rarity/Effects）。
- `CardCatalog.cs` — 40 张卡牌静态目录：`All` 只读列表（按配置表 §3 分五类 C01-C40）、`Find(id)`/`Exists(id)` 查找。MVP 阶段为代码内硬编码，后续可迁移至 ScriptableObject。

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
- `RewardResolver.cs` — 战斗奖励（A2-16）：按遭遇类型生成资源+卡牌选项；`PendingOptions`/`ClaimCard`（领取后只移除卡牌选项，遗物保留——A2-23 起卡牌与遗物各选一个）/`ClaimRelic`（领取后只移除遗物选项，卡牌保留）/`SkipReward`/`Clear`；A2-20：资源奖励由 `RunSession.ApplyCombatRewards` 在胜利时钳制入账（跳过只放弃卡牌）；A2-21：池逻辑重构为 `BuildRegionPool(region, commonOnly)`（区域来源卡 + 建筑奖励卡 B03：C04/C11、B04：C34/C37/C40 跨区域进池），新增 `RewardPoolContains(region, cardId)` 池内容直查；A2-22：遗物奖励（配置表 §5.1）精英 +2 件、首领 +3 件（`GenerateRelicOptions`：未持有池、已持有不重复、BossOnly 仅首领出），`RewardOption.RelicId`。
- `PartnerDef.cs` / `PartnerRoster.cs` — 伙伴系统（A2-15）：8 名伙伴静态定义 + 运行时状态（招募/上阵/HP/疾病/疲劳/忠诚度）；`BuildCombatTeam`（伙伴在前旅人第二位）/`SyncFromCombat`/`InitTestRoster`；A3-25 的 `CaptureSaveData`/`RestoreSaveData` 保存全部伙伴状态及 `_activeTeam` 的有序 ID，避免只恢复招募标记而丢失编队顺序。
- `CardCatalogTests.cs` — 27 个 EditMode 用例（A1-13）：目录完整性（6）/ 出牌基础流程（5）/ 各类卡牌效果（14）/ 边界与特殊机制（2）。
- `PartnerRosterTests.cs` — 15 个 EditMode 用例（A2-15）：数据完整性/招募/上阵上限/未招募拒绝/死亡拒绝/BuildCombatTeam/SyncFromCombat/Clear。
- `CampaignDeckTests.cs` — 12 个 EditMode 用例（A2-16）：牌组初始化/上下限/独立副本/奖励生成/领取/跳过。
- `EnemyUnit.cs` — 敌人单位（继承 CombatUnit）：`EnemyIntentExec`（攻击/全体攻击/防御/掠夺 + 副作用 BleedStacks/DiseaseStacks + 诱饵标记 `TargetsPlayer`）+ `RollIntent`（种子驱动加权抽取）+ 10 种敌人工厂（EN01-EN10，按配置表 §5）；`Clone` 重写深拷贝意图。
- `EnemyIntentTests.cs` — 12 个 EditMode 用例：同意图/零权重/首回合揭示/攻击/防御/掠夺/AOE/死敌跳过/无目标/默认目标选择/士气隔离。
- `EncounterConfig.cs` — 9 组遭遇表（草原普通×2/精英/首领，密林普通×3/精英/首领，含标签/区域/类型）；`RunSession._testEncounterIndex` 翻页选择。

## 区域地图（A2-17 / A2-23）
- `RegionMap.cs` — 区域节点地图：`RegionMapNode`（Id/Layer/Type/EnemyPoolIds/EventPoolIds/NextIndexes）+ 静态管理器 `RegionMap`：`Generate(region, rng)` 支持草原与密林（配置表 §9：L1 战斗/事件/营地、L2 战斗/事件/精英、L3 战斗/事件/营地、L4 首领；草原敌人池 EN01/02/04+EN03+EN05 事件 E01-E10，密林 EN06/07/09+EN08+EN10 事件 E11-E20；层内随机、连通性保证：上下层出入度≥1、第三层全连首领、无回退）、`TryMoveTo`（下一层/相连/未访问三查）、`ReachableNext`（UI 高亮）、`Clear`、`Region`（当前区域）；A3-25 的 `CaptureSaveData`/`RestoreSaveData` 直接保存并恢复节点顺序、内容池、连接、当前位置、访问集合和路径，不依赖重新生成地图。

## 遗物系统（A2-22）
- `RelicCatalog.cs` — 8 件遗物静态目录（配置表 §7）：`RelicDef`（Id/DisplayName/EffectText/BossOnly）；R01 旅人罗盘（地图显示全部节点，MVP 天然生效）/R02 铁锅（每区域首次进营地粮 +4）/R03 琥珀护符（每场战斗开始全队 +3 护甲）/R04 医师药箱（每区域首次进营地移除疾病或疲劳）/R05 商队印记（每区域首次事件财富 +5）/R06 狼牙坠饰（每场首次普通伤害 +3）/R07 指挥旗（每场首张战术卡 -1 费）/R08 不熄灯（BossOnly，首领战开始 +2 士气）。
- 触发接入：`RunSession.Relics`（持有记录，**跨测试入口保留**、新游戏 `StartNewGame` 清零）+ `AddRelic`/`HasRelic`；R02 在 `EnterCampNode`（与 B01 叠加 +8）、R04 `RelicClinicAvailable`/`RelicClinic`（区域级一次）、R05 在 `ApplyEventOptionEffects`（与 B05 市集叠加，事件财富首次 +10）；`CombatManager.ApplyCombatStartRelics`（R03 全队护甲、R08 首领战士气）+ 战斗级标记 `RelicWolfUsedThisCombat`/`RelicFlagUsedThisCombat`（End 重置）；`CombatResolver` R06（首伤 +3，`RelicWolfBonus` 常量）+ R07（首张战术卡 C25-C32 减 1 费）+ `IsTacticalCard`；`Init` 内 `CurrentEncounterType` 先于战斗开始应用（R08 判断首领战）。
- 奖励接入：`RewardResolver.GenerateRelicOptions`（精英 2 / 首领 3，已持有不重复）；`BattleView.ShowRewardPage` 渲染金色遗物条目（`RelicReward.prefab`，点击 `ClaimRelic` 加入持有）。
- `RelicTests.cs` — 26 个 EditMode 用例：目录/每件触发时点与一次性/叠加（R02+B01、R05+B05）/奖励生成与领取/战斗级标记重置/Reset 保留+新局清零。

## 建筑系统（A2-21）
- `BuildingCatalog.cs` — 5 座一阶建筑静态目录（配置表 §8）：`BuildingDef`（Id/DisplayName/Type 营地或城镇/CostWealth/CostMaterial/CostReputation/RequiresBossDefeated/EffectText）；B01 储粮帐篷（3 建材）/B02 野战医棚（20 财 3 建材）/B03 铁匠铺（30 财 5 建材 5 声望，需首领）/B04 医馆（25 财 4 建材 5 声望，需首领）/B05 市集（30 财 5 建材 5 声望，需首领）；`Find(id)`。
- 建造与效果（RunSession 内）：`BuiltBuildings`（去重）/`HasBuilding`/`BuildBlockReason`（成本/前置/重复三查，返回禁用原因）/`TryBuildBuilding`（校验→扣资源→登记→记录）/`MarkGrasslandBossDefeated`（首领遭遇胜利置位，**跨测试入口保留**、新游戏清零）/`EnterCampNode`（营地节点：风险 -2 + B01 首次粮食 +4）/`CampfireRest`（S01 篝火休整：移除 1 疲劳，生命 25% 留待战役生命系统）/`CampClinic`（B02 移除疾病）/`TownClinic`（B04 移除疾病或疲劳）/`FreeUpgradePending`+`FreeUpgradeCard`（B03 首次建成免费升级 1 张卡）；B05 事件财富首次 +5 在 `ApplyEventOptionEffects` 接入。
- 营地测试入口补调试资源（建材 10/财富 50/声望 10）便于 Play 验证建造。
- `BuildingTests.cs` — 20 个 EditMode 用例：目录/建造/成本不足/重复/首领前置/建筑效果/B05/Reset。

## 事件系统（A2-19）
- `EventCatalog.cs` — 20 个事件静态目录（配置表 §6，与 CardCatalog/EnemyUnit 同模式：代码内硬编码，可复现）：`EventDef`（Id/DisplayName/Description/Region/Category/Options）、`EventOptionDef`（条件/支付/即时结果/招募/获得卡与遗物/移除卡/升级/状态移除/事件战斗与胜利额外奖励）、`EventOptionCondition`（PayResource/HasPartner*/ReputationAtLeast/HasRemoveableCard 等 8 种）、`EventStatusChoice`（FatigueSingle/DiseaseAll/DiseaseOrFatigueSingle）；`Find(id)`。
- 事件结算（RunSession 内）：`StartEventFromNode`（地图事件节点按种子抽取）/`StartEvent`（测试与节点进入，Event 状态幂等）/`EventOptionBlockReason`（条件校验返回禁用原因，供 UI 置灰）/`ChooseEventOption`（支付→战斗或子选择或即时结算）/`ChooseEventCard`（移除/升级子选择）/`ChooseEventStatusUnit`（单位状态移除）/`CancelEventChoice`/`ApplyPendingEventCombatRewards`（胜利额外奖励仅结算一次，由 CombatManager 胜利分支调用）/`ClearPendingEventCombatRewards`（失败清除，防残留）；`Relics` 遗物持有记录；`EventFlags` 记录已完成事件 ID；事件完整结束回 Map 后触发安全存档，事件选择或事件战斗中不写盘；`PlayerDisease` 为主角战役疾病。
- 资源钳制：事件与移动结算统一用 `Clamp` 保证粮 0-30/财 0-999/声望 0-100/建材 0-99/风险 0-10 不为负；招募伙伴已招募→忠诚 +10、阵亡→选项禁用（配置表 §6 通用规则）。
- `EventTests.cs` — 57 个 EditMode 用例：目录完整性/每选项结算/条件不满足/事件战斗胜利与失败/子选择/忠诚规则/钳制。

## UI 结构（场景组件化 + Prefab 驱动）
- `GameUi.cs` — 场景 UI 驱动：`[SerializeField]` 持有面板/HUD/标题/描述/按钮/`_battleView`/`_mapNodeContainer`/`_eventOptionContainer`/`_campOptionContainer`；A3-25 新增 `_continueButton`/`_saveStatusText`、`OnContinueGame` 与 `RefreshSaveUi`，按恢复结果显示 Map/Event/Camp/Combat；`ReturnToMap` 在奖励完成时委托 `RunSession.CompleteRewardAndReturnToMap`，确保战斗清理与存档顺序；其余职责包括页面显隐、地图节点、事件选项、营地服务、结算页及测试入口的 UI 编排。
- `BattleView.cs` — 战斗界面独立控制器（A1-14）：从 `_battlePagePrefab` 运行时实例化 BattlePage；管理手牌/单位卡片生命周期；处理出牌/选目标/结束回合/返回菜单交互；A2-20：`ShowRewardPage`（独立奖励页：标题/资源明细/卡牌/跳过/继续，胜利后自动弹出，`_combatWon` 标记支撑模拟胜利路径）；A2-21：顶部测试按钮（◀ 上一组/下一组 ▶ 翻页重开、模拟胜利/失败）；A2-22：奖励页金色遗物条目（`_relicRewardPrefab` 实例化，名称/效果填充、点击领取）；A2-24：`OnSimulateDefeat` 对齐完整结算流程（ForceDefeat→End→Defeat→结算页）、`RefreshPostCombat` 真实主角死亡自动进结算页、`OnRewardContinue` 分流（Victory→结算 / Reward+地图→ReturnToMap / Reward 无地图→ReturnToMenu）。子组件通过 `ResolveRefs()` 自动解析。
- HUD（TestHud，尺寸 680×300）：7 行文本——随机种子 / 当前状态 / 当前配置 / 最近一次规则结算 / 最近状态切换（最近 3 条）/ 内容校验状态（OK 或 N 个问题+首个）/ 本局记录（N 条+最新类别 #序号）。
- Canvas：ScreenSpaceOverlay + CanvasScaler（1920×1080，match 0.5）。**子对象顺序即渲染顺序**：MainMenu → BattlePage(运行时) → TestPage → TestHud（HUD 在顶层）。
- MainMenu：ScrollRect + Viewport(RectMask2D) + Content(VerticalLayoutGroup)，场景内固定「新游戏 / 继续游戏 / 存档状态 / 测试入口 / 运行配置 / 退出」，增删按钮自动重排。
- TestPage：旧版战斗测试页（A1-14 后战斗中隐藏，BattleView 替代）。
- EventSystem：EventSystem + StandaloneInputModule（Legacy Input）。

## 战斗界面 Prefab（A1-14 / A2-20 / A2-21）
- `Assets/Prefabs/BattlePage.prefab` — 五区块布局（全部 TMP）：TopBar（TurnInfo/Energy/Morale/Plunder/EndTurnBtn + 测试按钮：◀ 上一组/下一组 ▶/模拟胜利/模拟失败）/ MainArea（TeamPanel/EnemyPanel/RightPanel+ReturnBtn）/ BottomBar（DrawPile/HandCards/DiscardPile）。
- `Assets/Prefabs/RewardPage.prefab` — 独立奖励页（A2-20）：TitleBar（战斗胜利标题 + 资源明细，明细文字全宽）/ CardOptions（空容器，卡片改为挂奖励页根手动布局——无 CanvasRenderer 中间容器下实例化 UI 不渲染的工程坑）/ BottomBar（跳过/继续）。
- `Assets/Prefabs/RelicReward.prefab` — 遗物奖励条目（A2-22）：金色条目（名称 + 效果文字，TMP 字体内置），奖励页实例化并手动定位。
- `Assets/Prefabs/HandCard.prefab` — 手牌卡片（TMP）：TopBar(类型色)/ CostRow(Cost/Name) / Effect。
- `Assets/Prefabs/UnitCard.prefab` — 队伍单位卡片（TMP）：TopBar / Name / HP / Status / Intent。
- `Assets/Prefabs/EnemyCard.prefab` — 敌人卡片（TMP，结构同 UnitCard，独立样式）。

## 事件流
- `RunSession.Changed` / `GameFlow.Changed` → `GameUi.Refresh()`（HUD：种子/状态/配置/最近结算/最近状态切换）；`GameConfigProvider.Changed` → `GameUi.RefreshConfigUi()`；`CampaignSaveService.Changed` → `GameUi.RefreshSaveUi()`（继续按钮可用性与明确存档状态）。
- `GameBootstrap`（BeforeSceneLoad）→ `GameConfigProvider.Initialize()` + `ContentRegistry.LoadAll()` + `RunSession.Reset()` + `CampaignSaveService.Initialize()` → 场景 `GameUi.Awake` 接管 UI。

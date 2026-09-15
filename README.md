# 一人旅途（One Journey）

《一人旅途》是一款面向 PC 的单机 Roguelike 牌组构筑游戏，结合节点探索、回合制战斗、事件抉择、伙伴队伍、资源管理与营地建设。

玩家从独行旅人出发，在不同区域中规划路线、管理粮食与风险、招募伙伴、调整牌组并建设营地，逐步完成从生存远征到建立势力的成长。

## 核心循环

```text
选择地图路线
  → 支付移动消耗并累积风险
  → 进入战斗、事件或营地
  → 获得卡牌、遗物、伙伴与资源
  → 调整牌组和建设设施
  → 击败区域首领并进入下一地区
```

当前垂直切片包含草原与密林两个区域，以击败密林首领作为本局胜利条件。主角阵亡会进入失败结算。

## 当前 MVP 内容

| 内容 | 数量/范围 |
|---|---|
| 区域 | 草原、密林，共 2 个 |
| 基础卡牌 | 40 张 |
| 伙伴 | 8 名，主角加最多 3 名伙伴上阵 |
| 敌人 | 10 种 |
| 事件 | 20 个 |
| 遗物 | 8 件 |
| 一阶建筑 | 5 座 |
| 基础遭遇配置 | 9 组 |

## 已实现系统

### 地图与远征

- 按种子生成的四层节点地图，支持战斗、事件、营地、精英和首领节点。
- 路径连通性校验、二次确认移动、已访问与可达状态显示。
- 草原和密林拥有独立敌人池、事件池、移动消耗与风险增长。
- 风险达到阈值会触发区域危机伏击。

### 回合制卡牌战斗

- 玩家回合、共享能量、抽牌堆、手牌、弃牌堆与消耗区。
- 单体、群体、自身和友方目标选择。
- 护甲、流血、士气、疾病、疲劳、集火与掠夺等状态。
- 可读的敌人意图系统，支持攻击、全体攻击、防御和掠夺。
- 战斗奖励包含资源、卡牌与遗物；精英和首领奖励规则不同。

### 伙伴、事件与建设

- 8 名伙伴的招募、上阵、生命、忠诚、疲劳和疾病状态。
- 20 个事件支持资源支付、伙伴条件、战斗、移除卡、升级卡和治疗选择。
- 营地提供休整、牌组查看、建筑建设与建筑服务。
- 建筑可改变资源收益、治疗能力、升级机会和后续奖励卡池。

### 存档与继续游戏

- 存档位于 `Application.persistentDataPath`。
- 主存档：`campaign-save.json`。
- 备份存档：`campaign-save.backup.json`。
- 使用版本号和 SHA-256 完整性校验。
- 主档损坏时尝试从备份恢复；缺字段、损坏或旧版本会明确拒绝。
- 只在地图、节点入口和营地等安全点保存，不保存半完成的战斗、事件选择或奖励状态。

### 引导、规则与输入

- 8 个首次机制引导：地图、粮食、出牌、能量、敌人意图、奖励、事件与营地建筑。
- 所有正式页面均可打开规则说明。
- 支持鼠标和键盘完成核心操作。
- 焦点操作有高对比度描边；不可逆操作使用默认聚焦“取消”的确认框。
- 地图、事件和营地采用 `1920×1080` 全屏页面布局。

## 环境要求

- **Unity：** `2022.3.62f3 LTS`
- **目标平台：** Windows
- **渲染管线：** Built-in Render Pipeline
- **输入：** Legacy Input Manager，键盘与鼠标
- **主要 UI：** uGUI + TextMeshPro
- **测试框架：** Unity Test Framework `1.1.33`

建议使用 Unity Hub 以项目锁定版本打开仓库，避免由其他 Unity 版本触发资源或场景升级。

## 快速启动


## 操作方式

| 输入 | 功能 |
|---|---|
| 鼠标左键 | 选择按钮、卡牌、目标和地图节点 |
| 方向键 | 在 UI 控件之间移动焦点 |
| `Tab` / `Shift+Tab` | 正向/反向循环焦点 |
| `Enter` / `Space` | 确认当前选项 |
| `Esc` | 取消选牌、目标或事件子选择 |
| `H` / `F1` | 打开规则说明 |

地图中的可达节点需要确认两次：第一次选择路线，第二次才会移动并结算资源。

## 项目目录

```text
Assets/
├─ Data/                         配置和 Resources 内容
├─ Fonts/                        TextMeshPro 中文字体资源
├─ Prefabs/                      页面、卡牌、单位和选项 Prefab
├─ Scenes/
│  └─ SampleScene.unity          唯一启动场景
├─ Scripts/
│  └─ Core/                      OneJourney.Core 运行时程序集
└─ Tests/
   └─ EditMode/                  NUnit EditMode 测试

Locus/knowledge/
├─ design/                       游戏设计、术语和配置规范
└─ memory/unity-project-understanding/
   ├─ architecture.md            当前代码与资源架构
   ├─ implementation-plan.md     分阶段实施计划
   ├─ progress.md                已完成内容和验证状态
   └─ tech-stack.md              技术栈决策记录
```

## 关键代码入口

- `Assets/Scripts/Core/GameBootstrap.cs`：游戏启动初始化。
- `Assets/Scripts/Core/GameFlow.cs`：全局流程状态机。
- `Assets/Scripts/Core/RunSession.cs`：战役状态、资源、事件和流程协调。
- `Assets/Scripts/Core/CombatManager.cs`：战斗生命周期与回合。
- `Assets/Scripts/Core/CombatResolver.cs`：卡牌效果和伤害结算。
- `Assets/Scripts/Core/RegionMap.cs`：地图生成与移动规则。
- `Assets/Scripts/Core/CampaignSaveService.cs`：安全存档与恢复。
- `Assets/Scripts/Core/GameUi.cs`：静态页面路由和交互绑定。
- `Assets/Scripts/Core/BattleView.cs`：运行时战斗与奖励页面。

## 设计与开发文档

- [游戏设计文档](Locus/knowledge/design/game-design-document.md)
- [MVP 配置表](Locus/knowledge/design/mvp-configuration-tables.md)
- [术语表](Locus/knowledge/design/glossary.md)
- [游戏状态流转](Locus/knowledge/design/game-state-flow.md)
- [架构与文件职责](Locus/knowledge/memory/unity-project-understanding/architecture.md)
- [实施计划](Locus/knowledge/memory/unity-project-understanding/implementation-plan.md)
- [实施进度](Locus/knowledge/memory/unity-project-understanding/progress.md)

## 当前限制

- 仅以 Windows、键盘和鼠标为首发目标。
- 尚未适配手柄、触屏和移动端。
- 不包含联网、云存档、远程热更新、分析埋点或第三方崩溃上报。
- 当前美术和部分图标仍为 MVP 占位资源。
- 当前可玩内容只覆盖草原与密林垂直切片，完整帝国阶段和更多区域属于后续范围。
- 尚未完成正式独立 Player 的发布验收与长流程平衡测试。

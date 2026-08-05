---
id: kd_16876443-f2bb-491f-9661-53b0cba3b249
type: memory
path: unity-project-understanding/architecture.md
title: architecture
inheritInjectMode: true
summaryEnabled: false
commandEnabled: false
readOnly: false
inheritAiConfig: true
createdAt: 1785840995170
updatedAt: 1785900348039
---

# architecture

<!-- locus:body:start -->
# 架构与文件职责（截至 2026-08-05）

## 目录与程序集
- `Assets/Scripts/Core/` — 核心运行时逻辑（程序集 OneJourney.Core，无外部引用）。
- `Assets/Tests/EditMode/` — EditMode 测试（OneJourney.EditModeTests.asmdef，NUnit 风格）。

## 运行时入口与场景
- `GameBootstrap.cs` — `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`：初始化 `GameConfigProvider` 与 `RunSession.Reset()`。**不创建 UI**——UI 由场景承载（MVP 单场景，UI 不跨场景存活）。
- `Assets/Scenes/SampleScene.unity` — 唯一主场景：Main Camera + GameUi 层级（GameUi / Canvas / TestHud / MainMenu / TestPage / EventSystem）。

## 配置系统
- `GameMode.cs` — 枚举 Development / Testing / Release。
- `GameConfig.cs` — ScriptableObject：`_mode`、`_showTestHud`、`_enableTestEntries`；静态工厂 `Create(mode, showTestHud, enableTestEntries)`。
- `GameConfigProvider.cs` — 静态提供者：按启动参数（`-releaseMode` 锁定、`-testMode`）或 `Application.isEditor` 决定启动模式；从 `Resources/Configs/GameConfig_<Mode>.asset` 加载，缺失时回退 `GameConfig.Create` 默认值并打警告；`IsReleaseLocked` 时忽略非 Release 切换。
- `Assets/Data/Resources/Configs/GameConfig_{Development,Testing,Release}.asset` — 三套配置资产（Release：HUD 与测试入口均隐藏；开发/测试：均开启）。

## 会话与流程
- `RunSession.cs` — 静态会话状态：随机种子（支持 `-seed <n>` 命令行参数，`RequestedSeedFromArgs`）、`CurrentState`（GameState 枚举）、结算记录列表（上限 20 条，`LastResolution` 取最新）；`StartNewGame` / `EnterTestPage` / `RecordResolution` / `Reset`；`Changed` 事件驱动 UI 刷新。

## UI 结构（场景组件化）
- `GameUi.cs` — 场景 UI 驱动：全部元素通过 `[SerializeField]` 持有（面板、HUD 文本、页面标题/描述、按钮数组、按配置显隐的元素列表）；`Awake` 绑定按钮回调并订阅 `RunSession.Changed` / `GameConfigProvider.Changed`；`RefreshConfigUi` 按配置控制测试入口/模式切换区/HUD 显隐。
- Canvas：ScreenSpaceOverlay + CanvasScaler（1920×1080，match 0.5）。**子对象顺序即渲染顺序**：MainMenu → TestPage → TestHud（HUD 在顶层）。
- MainMenu：ScrollRect + Viewport(RectMask2D) + Content(VerticalLayoutGroup，childControlWidth=false，元素宽 700，内容高 706)，增删按钮自动重排。
- TestPage：VerticalLayoutGroup，默认 Inactive，由按钮进入；标题/描述/记录按钮/返回按钮。
- EventSystem：EventSystem + StandaloneInputModule（Legacy Input）。

## 事件流
- `RunSession.Changed` / `GameConfigProvider.Changed` → `GameUi.Refresh()`（HUD：种子/状态/配置/最近结算）/ `RefreshConfigUi()`（显隐控制）。
- `GameBootstrap`（BeforeSceneLoad）→ `GameConfigProvider.Initialize()` + `RunSession.Reset()` → 场景 `GameUi.Awake` 接管 UI。
<!-- locus:body:end -->

# 最简但健壮的 Unity 技术栈（Roguelike 卡牌）

## 已锁定的 MVP 决策

- **编辑器：Unity 2022.3.62f3 LTS**；首发平台仅 Windows。
- **输入：仅键盘与鼠标**；手柄、触屏和移动端适配留待后续阶段。
- **存档：JSON**，保存在本地；MVP 不启用云存档、联网、分析埋点或崩溃上报服务。
- **资源分发：** MVP 随包发布资源，不实施远程热更；Addressables 仅在确有异步加载需求时接入。

## 引擎与渲染
- **Unity 2022.3.62f3 LTS + URP 2D Renderer**：版本已锁定，原生支持 2D 光照/后处理，满足卡面特效与轻量地图表现。
- **Input System**：事件驱动输入，易做手柄/触屏/键鼠共存与重绑定。
- **Cinemachine**：无代码相机过渡，适合事件演出与战斗镜头。

## 游戏框架（单机优先）
- **数据驱动**：`ScriptableObject` 定义卡牌/事件/伙伴，配合 JSON 本地存档；MVP 不实施加密。
- **状态管理**：轻量 MVC（或 UniRx/事件总线）驱动 UI 与逻辑解耦；避免过早引入 ECS。
- **动画/过渡**：DOTween（免费）处理卡牌出牌、抽牌、UI 缩放与插值。

## 资源与内容分发
- **Addressables 2.x（后续可选）**：待内容体量需要异步加载或后续热更时接入；MVP 不依赖远程内容分发。
- **美术管线**：PSD 导入（Sprite Editor）+ 统一 2048 图集；音频用 .ogg 压缩并走 Addressables。

## UI
- **UI Toolkit**：菜单、卡库、设定等静态/列表界面；样式用 USS，复用模板。
- **TextMeshPro**：统一字体渲染与富文本；数值/关键词高亮。

## 存档与配置
- **本地存档**：`Application.persistentDataPath` 下 JSON；关键字段做校验哈希与版本号。
- **云存档（后续可选）**：MVP 不接入；若后续需要，可评估 Unity Cloud Save 或自建服务。

## 网络（可选扩展）
- **Netcode for GameObjects (NGO)**：非 MVP 范围。未来若加合作或异步竞速，可用 NGO + Unity Relay；保持客户端权威以简化作弊防护。

## 构建与 CI/CD
- **版本控制**：Git + Git LFS（大图/音频）；分支策略 main/dev/feature。
- **自动化构建**：GitHub Actions + game-ci/unity-builder，MVP 只输出 Windows；后续再增加 Linux、macOS 与 Addressables 构建。
- **质量工具**：NUnit + PlayMode 测试；Profiler/Memory Profiler；Code Coverage（编辑器包）。

## 监控与分析（可选）
- **Unity Analytics**：MVP 不启用；后续如需埋点再评估。
- **Crash 报告**：MVP 不接入第三方服务，使用本地可读日志；后续可评估 Backtrace 或 Unity Cloud Diagnostics。

## 推荐最小依赖列表
1) 核心：Unity 2022.3.62f3 LTS、URP、Input System、TextMeshPro、Cinemachine  
2) 工具：DOTween、NUnit、Profiler/Memory Profiler  
3) 后续可选：Addressables、Netcode for GameObjects + Relay、Unity Cloud Save、Analytics

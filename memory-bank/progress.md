# 实施进度

## 2026-08-05 · A0-1 固定项目运行基线（UI 部分，用户已验证）
- 完成：
  - `GameUi.cs` 由运行时动态构建改为场景组件驱动（`[SerializeField]` 引用 + Awake 按钮绑定）。
  - `GameBootstrap.cs` 不再动态创建 GameRoot/GameUi，只保留配置与会话初始化。
  - `Assets/Scenes/SampleScene.unity` 搭建完整 UI 层级（GameUi/Canvas/TestHud/MainMenu/TestPage/EventSystem），引用已连接并保存。
  - 修复 TestHud 被 MainMenu 遮挡：移至 Canvas 子对象最后（渲染顶层）。
- 用户验证通过：主菜单、新游戏、四个测试入口、三套配置切换、HUD 显示均正常。
- 配置事实：Release 配置 `_showTestHud=false`、`_enableTestEntries=false`（HUD 与测试入口隐藏）；开发/测试配置均开启。

## 进行中
- A0-2 定义游戏术语和状态边界：**已实现，等待用户验证**（GameFlow.cs 状态机 + 状态日志、GameState 枚举扩展、GameUi HUD 状态切换行、GameFlowTests 8 个用例；设计文档 design/glossary.md、design/game-state-flow.md）。用户验证通过后更新本文档并进入 A0-3。

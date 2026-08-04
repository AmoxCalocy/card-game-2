# 一人旅途（Card2）项目运行基线

本文档对应实施计划第 1 步「固定项目运行基线」。具体规则与数值以 `memory-bank/mvp-configuration-tables.md` 为唯一来源。

## 启动方式

- 使用 Unity Hub 打开 `Card2/` 目录，编辑器版本为 **Unity 2022.3.62f3 LTS**。
- 打开 `Assets/Scenes/SampleScene.unity`，点击 Play。
- 启动后进入主菜单（`SampleScene` 已加入 Build Settings）。

## 窗口分辨率

- 默认窗口分辨率：1920×1080（全屏窗口）。
- 分辨率适配目标：1280×720、1920×1080、超宽窗口；界面适配将在后续战斗界面步骤中验证。

## 构建方式

- 当前尚未提供批处理构建脚本，按仓库约定使用 **File > Build Settings > Windows** 构建。
- 干净环境连续构建两次，两次均应从启动画面进入主菜单。

## 三套运行配置

配置资产位于 `Assets/Data/Resources/Configs/`：

| 配置 | 测试 HUD | 测试入口 | 说明 |
|---|---|---|---|
| GameConfig_Development | 显示 | 开放 | 编辑器开发默认 |
| GameConfig_Testing | 显示 | 开放 | 独立构建默认，用于验收测试 |
| GameConfig_Release | 隐藏 | 隐藏 | 仅保留新游戏入口 |

启动时按以下顺序选择配置：

1. 启动参数 `-releaseMode` → Release（锁定，不再显示测试入口与配置切换）。
2. 启动参数 `-testMode` → Testing。
3. Unity 编辑器 → Development。
4. 独立构建（无参数）→ Testing。

非 Release 锁定状态下，主菜单底部可手动切换三套配置。测试配置下左上角 HUD 显示：随机种子、当前状态、当前配置、最近一次规则结算结果。

## 测试入口

主菜单提供：

- **新游戏**：创建会话并进入地图占位页。
- **测试：战斗 / 地图 / 事件 / 营地**：直接进入对应占位页。
- **写入测试结算记录**：在占位页写入一条示例结算，用于验证 HUD 刷新。
- **返回主菜单**：重置会话（种子、状态、结算记录全部清空），确认退出后无残留状态。

可通过启动参数 `-seed 12345` 固定随机种子，用于复现测试。

## 自动化测试

- EditMode 测试位于 `Assets/Tests/EditMode/`（`OneJourney.EditModeTests` 程序集）。
- 在 Unity Test Runner 中运行 EditMode 测试，覆盖会话种子/状态/结算记录与三套配置资产。

## 目录说明

- `Assets/Scripts/Core/`：运行基线脚本（配置、会话、启动引导、UI 占位），属于 `OneJourney.Core` 程序集。
- `Assets/Data/Resources/Configs/`：三套 GameConfig 资产。
- `Assets/Tests/EditMode/`：运行基线 EditMode 测试。

---
id: kd_cfa819c9-77a9-4815-91f7-b377e22c7ab0
type: design
path: game-state-flow.md
title: game-state-flow
inheritInjectMode: true
summaryEnabled: true
commandEnabled: false
readOnly: false
inheritAiConfig: true
createdAt: 1785900496524
updatedAt: 1785900496525
---

# game-state-flow

## Summary
一局游戏状态机定义：11 个状态、转移表、每状态允许/禁止操作与不变式（实施计划 A0-2 交付物，代码在 GameFlow.cs）。

## Content
# 一局游戏的状态流转（A0-2 交付物）

状态机代码：`Assets/Scripts/Core/GameFlow.cs`（转移表、状态日志、CurrentState）。状态日志上限 100 条；重置（新会话）时清空。所有切换必须经 `GameFlow.TryTransition(to, reason)` 校验，非法转移被拒绝且不产生任何副作用，并写警告日志。

## 状态一览

| 状态 | 枚举 | 进入条件 | 离开条件 | 允许操作 | 禁止操作 |
|---|---|---|---|---|---|
| 主菜单 MainMenu | 1 | 启动、从结算返回 | 点新游戏 / 测试入口 | 新游戏、测试入口、切换配置、退出 | 任何流程内操作 |
| 新局初始化 NewGame | 6 | 主菜单/结算发起新游戏 | 初始化完成 | 生成种子、初始化会话 | 中途插入其他流程 |
| 地图选择 Map | 3 | 初始化完成、事件/奖励/营地结束 | 选择节点或进入营地 | 查看地图、选择相连节点、进入营地 | 跨层/已访问节点移动 |
| 移动结算 Move | 7 | 地图选择节点 | 移动结算完成 | 结算粮食消耗与惩罚 | 同时开启另一流程 |
| 事件 Event | 4 | 移动到事件节点 | 选择完成 | 按条件选择选项 | 条件不满足时确认 |
| 战斗 Combat | 2 | 移动到战斗节点/事件触发 | 胜利/失败 | 出牌、选目标、结束回合 | 敌方行动时出牌等 |
| 奖励 Reward | 8 | 普通战斗胜利 | 选择完成 | 选择/跳过奖励 | 跳过后再领取 |
| 营地或城镇 Camp | 5 | 地图入口/移动至营地节点 | 操作完成 | 恢复、牌组管理、建造 | 重复建造、资源不足建造 |
| 胜利 Victory | 9 | 击败区域首领（垂直切片结局） | 进入结算 | 查看结局 | 继续流程 |
| 失败 Defeat | 10 | 主角死亡/战斗失败 | 进入结算 | 查看失败原因 | 继续流程 |
| 结算 Settlement | 11 | 胜利/失败后 | 返回主菜单或同种子重开 | 返回主菜单、同种子重开 | 结算未完成时打开另一流程 |

## 转移表（代码 IsAllowed 与之保持一致）

- MainMenu → NewGame / Combat / Map / Event / Camp（后四者为测试入口）
- NewGame → Map
- Map → Move / Camp
- Move → Event / Combat / Camp
- Event → Map / Combat
- Combat → Reward / Victory / Defeat
- Reward → Map
- Camp → Map
- Victory / Defeat → Settlement
- Settlement → MainMenu / NewGame（同种子重开）
- 其余组合一律拒绝；Reset 为特殊操作：任意状态 → MainMenu 并清空状态日志（新会话）

## 不变式
- 每次切换只能发生一次（一次 TryTransition 至多一条日志，同状态重复切换被拒绝）。
- 没有无法返回的中间状态：任一状态经合法转移可达主菜单或结算。
- 结算未完成时（Settlement）禁止进入任何其他流程页面。
- 非法转移不改变当前状态、资源或牌组。

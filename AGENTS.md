# Repository Guidelines

## Project Structure & Module Organization

The Unity project lives in `Card2/`. Keep playable scenes in `Card2/Assets/Scenes/`; the current entry scene is `SampleScene.unity`. Add runtime code under `Card2/Assets/Scripts/`, organized by feature (for example, `Combat/`, `Map/`, `Cards/`, and `UI/`). Put ScriptableObject definitions and gameplay data in `Card2/Assets/Data/`, and art, audio, and prefabs in clearly named sibling folders.

`Card2/Packages/` and `Card2/ProjectSettings/` are project configuration and must be versioned. Do not commit Unity-generated `Library/`, `Temp/`, `Logs/`, `obj/`, or `UserSettings/` directories. Design and balancing sources live in `memory-bank/`; treat `mvp-configuration-tables.md` as the canonical MVP data specification and keep its workbook export aligned.

## Build, Test, and Development Commands

Open `Card2/` in Unity Hub with Unity **2022.3.62f3 LTS** and run the active scene with Play mode. Until a batch build method exists, create Windows builds through **File > Build Settings**.

Run tests in the Unity Test Runner, or from a configured Unity installation:

`"<Unity.exe>" -batchmode -quit -projectPath "Card2" -runTests -testPlatform EditMode -logFile -`

Replace `EditMode` with `PlayMode` for scene-level tests. The Test Framework package is installed, but no test assemblies exist yet.

## Coding Style & Naming Conventions

Use C# with four-space indentation, braces on new lines, and one public type per file. Use `PascalCase` for types, methods, properties, ScriptableObjects, and scene assets; use `camelCase` for parameters and locals; prefix private serialized fields with `_` (for example, `_health`). Name data assets with stable IDs, such as `Card_C01_SwordStrike` and `Enemy_EN01_Bandit`.

Keep gameplay rules deterministic and data-driven. Do not embed card, event, enemy, or reward values in UI scripts; define them in data assets and update the matching `memory-bank` table in the same change. No formatter or linter is configured; follow existing Unity and C# conventions consistently.

## Testing Guidelines

Create EditMode tests in `Card2/Assets/Tests/EditMode/` for pure rules and configuration validation. Create PlayMode tests in `Card2/Assets/Tests/PlayMode/` for scene flow, UI, and persistence. Name tests `Method_Scenario_ExpectedResult`. Cover normal, invalid, and boundary cases; use fixed random seeds for map, reward, and combat tests.

## Commit & Pull Request Guidelines

This repository has no commit history yet, so no local convention exists. Use concise imperative Conventional Commit messages, for example `feat: add card draw resolver` or `fix: clamp food at zero`. Keep each commit focused. Pull requests should describe gameplay impact, list test results, link the relevant issue or design-table rows, and include screenshots or a short capture for scene or UI changes.

# 重要提示：(Always)
# 写任何代码前必须完整阅读 memory-bank/@architecture.md（包含完整数据库结构）
# 写任何代码前必须完整阅读 memory-bank/@game-design-document.md
# 每完成一个重大功能或里程碑后，必须更新 memory-bank/@architecture.md
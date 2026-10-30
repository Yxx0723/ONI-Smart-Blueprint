# Game-Build-Matrix.md —— 游戏构建与工具链矩阵

> 最后更新：2026-10-30（P0）｜ 数据来源以"实测"列为准，未实测的一律标注

## 1. 版本矩阵

| 项目 | 稳定版（本项目目标） | 公开测试分支（不目标） |
|---|---|---|
| ONI Build | **744825** | 761015 |
| 分支标识 | `U59-744825-S (release)` | `public_testing` |
| 发布形态 | Steam 正式版 | 质量更新预发布 |
| 本机状态 | **已安装，实测运行过** | 未安装，未切换 |
| 存档兼容 | 本项目唯一支持目标 | 官方声明存在向后不兼容风险 |
| 证据级别 | **E3**（`Player.log` 中的 Build 行） | 方案文档记载，本项目未核实 |

**规则**：本项目所有验收只在 744825 上做。切换 Steam 分支属于用户决定，Agent 不得擅自操作。

## 2. 运行时组件

| 组件 | 版本 | 来源 |
|---|---|---|
| Unity | **6000.3.5f2**（`3fa8bc678cb0`） | `Player.log` `Initialize engine version` |
| Harmony | **2.4.2.0** | `0Harmony.dll` 程序集版本 |
| Mono | 随游戏内置 `Contents/MonoBleedingEdge` | 安装目录 |
| `Assembly-CSharp.dll` | Version 0.0.0.0，SHA-256 前 32 位 `9b6e9ef6d1cef9477d20a7cbb70680e8` | 实读 |

> **注意**：Unity 程序集不携带有意义版本号（恒为 0.0.0.0）。判断"游戏是否变化"必须用
> **Build 号 + DLL 哈希**双指标，不能看版本号。

## 3. 构建工具链

| 项目 | 版本 | 状态 |
|---|---|---|
| OS | macOS 27.0.1 / arm64（Apple M1） | 实测 |
| .NET SDK | **10.0.301**（唯一已装 SDK） | 实测 |
| 可用 TargetFramework | `netstandard2.1` | **实测 PASS** |
| `dotnet build` | 可用 | 实测 PASS |
| `ilspycmd` | 11.0.0.9375（`~/.dotnet/tools`） | 实测 PASS |
| NuGet 公网 | **不可达**（代理 127.0.0.1:1082 未运行） | 实测 FAIL |
| NuGet 本地缓存 | 含 `PLib 4.25.0`、`lib.harmony.ref` 等 54 个包 | 实测 |
| `xunit` / `Microsoft.NET.Test.Sdk` | **缓存中不存在** | 实测（故测试改为零依赖运行器） |

## 4. 路径矩阵

| 用途 | 路径 | 状态 |
|---|---|---|
| 游戏安装 | `~/Library/Application Support/Steam/steamapps/common/OxygenNotIncluded` | 实测存在 |
| Managed 程序集 | `.../OxygenNotIncluded.app/Contents/Resources/Data/Managed` | 实测存在 |
| Dev MOD 目录 | `~/Library/Application Support/unity.Klei.Oxygen Not Included/mods/Dev` | **实测生效** |
| Local MOD 目录 | `~/Library/Application Support/unity.Klei.Oxygen Not Included/mods/Local` | **实测不存在** |
| Steam MOD 目录 | `.../mods/Steam`（空） | 实测存在 |
| Mod 启用状态文件 | `.../mods/mods.json` | 实测存在 |
| 游戏日志 | `~/Library/Logs/Klei/Oxygen Not Included/Player.log` | 实测存在 |
| 上次日志 | `~/Library/Logs/Klei/Oxygen Not Included/Player-prev.log` | 实测存在 |
| 存档目录 | `.../unity.Klei.Oxygen Not Included/save_files` | **本项目不读写** |

> 方案文档 12.2 节写的 macOS 路径是 `mods/Local`，本机实测不成立。本项目统一使用 `mods/Dev`。

## 5. 构建配置矩阵

| 配置 | TargetFramework | 输出路径 | 用途 |
|---|---|---|---|
| OSB.Mod `Debug` | netstandard2.1 | 游戏 `mods/Dev/ONISmartBlueprint/` | 开发迭代 |
| OSB.Mod `Release` | netstandard2.1 | `OSB.Mod/bin/Release/` | 打包发布 |
| OSB.Domain.Tests | net10.0 | `tests/.../bin/` | 离线单测（不接触游戏） |

**为什么 Domain 是 netstandard2.1 而不是 net10.0**：它要被 netstandard2.1 的 MOD 程序集引用。
测试工程用 net10.0 是为了能跑 `dotnet run`，它引用 netstandard2.1 库是合法的。

## 6. 兼容性验收状态

| 场景 | 状态 |
|---|---|
| 原版（无 DLC） | NOT TESTED |
| Spaced Out!（`EXPANSION1_ID`） | NOT TESTED |
| 与 Blueprints Expanded 共存 | NOT TESTED |
| 与用户现有 `AtmoSuitTeleport` 共存 | NOT TESTED |
| 741xxx 及更早构建 | NOT SUPPORTED（未验证，`minimumSupportedBuild` 已声明 744825） |
| 761015 公开测试分支 | NOT SUPPORTED（明确不目标） |

## 7. 更新后的复核清单

游戏更新后，按顺序重跑并更新本文件：

1. `grep -m2 "Build:" "$HOME/Library/Logs/Klei/Oxygen Not Included/Player.log"`
2. `shasum -a 256 <Managed>/Assembly-CSharp.dll`
3. 重跑 `ilspycmd -t KMod.DLLLoader` 等关键类型，比对 `docs/API-Audit.md`
4. 启动游戏，确认 `GameProbe` 的 `签名核对通过` 全部仍为通过
5. 若任何一条变为 `签名不匹配`，对应功能必须降级或停用，并在报告中标明

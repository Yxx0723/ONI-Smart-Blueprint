# P0 阶段报告

```text
阶段：P0
执行模式：本地编码代理（WorkBuddy 桌面端，具备本地文件与终端权限）
目标：官方稳定版 744825（未使用 public_testing 分支）
```

---

## 1. 已核实环境

| 项 | 实测值 | 状态 |
|---|---|---|
| 操作系统 | macOS 27.0.1 / arm64（Apple M1） | PASS |
| .NET SDK | 10.0.301（唯一已装） | PASS |
| TargetFramework | `netstandard2.1` 可编译、可被引用 | PASS |
| 游戏安装 | Steam 版，`~/Library/Application Support/Steam/steamapps/common/OxygenNotIncluded` | PASS |
| 游戏构建 | `U59-744825-S (release)`（`Player.log`） | PASS |
| Unity | `6000.3.5f2` | PASS |
| Harmony | `2.4.2.0` | PASS |
| `Assembly-CSharp.dll` | Version 0.0.0.0，SHA-256 前 32 位 `9b6e9ef6d1cef9477d20a7cbb70680e8` | PASS |
| Dev MOD 目录 | `.../unity.Klei.Oxygen Not Included/mods/Dev`（**`mods/Local` 不存在**） | 已纠正方案文档 |
| 反编译工具 | `ilspycmd 11.0.0.9375` | PASS |
| NuGet 公网 | **不可达**（代理 127.0.0.1:1082 未运行） | **FAIL（环境限制）** |
| 存档访问 | 全程未读写 `save_files/` | PASS |

**NuGet 不可达的后果**：`xunit` / `Microsoft.NET.Test.Sdk` 无法还原，因此单元测试改为
**零外部依赖的自建断言运行器**（`tests/OSB.Domain.Tests`）。这不是放弃测试，而是让
"核心逻辑可离线验证"这条验收项不被网络环境卡死。恢复网络后应迁移到正规模架，
已记入 `CONTEXT-HANDOFF.md`。

---

## 2. 实际运行命令与结果

### 2.1 单元测试

```bash
dotnet run --project tests/OSB.Domain.Tests/OSB.Domain.Tests.csproj
```

```text
OSB.Domain.Tests —— 共 28 个用例
------------------------------------------------------------------------
（28 行 PASS）
------------------------------------------------------------------------
断言通过 71 条，失败 0 条
结果：PASS
```

**单元测试：PASS**（28 用例 / 71 断言）

覆盖范围：`Point2i` 相等与哈希、旋转四次复位、镜像两次复位；`Rect2i` 包含/相交/并集/空矩形/
紧邻不相交；`Footprint` 格枚举与锚点换算；`Blueprint` 全部校验码（OSB001–OSB011）的正负路径、
包围盒、归一化幂等。

> 过程中出现过 1 次失败：`Point2i.RotateClockwise90` 断言写错（漏调用被测方法）。
> 已修正为真实断言，非放宽标准；修正后全绿。

### 2.2 构建

```bash
dotnet build OSB.Mod/OSB.Mod.csproj -c Debug
```

```text
已成功生成。
    10 个警告
    0 个错误
```

**构建：PASS**

警告说明：`NU1900`（无法访问 NuGet 漏洞源，环境所致）+ `MSB3277`（游戏 `Assembly-CSharp-firstpass`
引用的 `System.Net.Http 4.2.0.0` 与 `netstandard2.1` 参考程序集的 `4.1.2.0` 冲突，已自动择主版本）。
两者均不影响产物。

### 2.3 MOD 部署

```bash
ls -la "$HOME/Library/Application Support/unity.Klei.Oxygen Not Included/mods/Dev/ONISmartBlueprint"
```

```text
OSB.Domain.dll            18432
OSB.Domain.pdb            13388
OSB.GameAdapter.dll        9216
OSB.GameAdapter.pdb       10032
OSB.Mod.dll                5632
OSB.Mod.pdb                8624
mod.yaml                    654
mod_info.yaml               420
```

**MOD 文件部署：PASS**

洁净度检查：目录内**不含** `Assembly-CSharp.dll` / `0Harmony.dll` / `UnityEngine*.dll` / `Newtonsoft.Json.dll`。
程序集标识核对：`OSB.Mod 0.0.1.0`、`OSB.GameAdapter 1.0.0.0`、`OSB.Domain 1.0.0.0`。

### 2.4 游戏加载

**最终实测（2026-10-30 15:49，构建时间戳 15:35）—— PASS**

```
[INFO] Build: U59-744825-S (release)
[INFO] Loading mod content DLL [ONI Smart Blueprint:ONISmartBlueprint] (provides DLL)
[INFO] Loading MOD dll: OSB.Domain.dll
[INFO] Loading MOD dll: OSB.GameAdapter.dll
[INFO] Loading MOD dll: OSB.Mod.dll
[WARNING] OSB.Domain ... has no classes inheriting from UserMod, creating one...
[WARNING] OSB.GameAdapter ... has no classes inheriting from UserMod, creating one...
[OSB] OnLoad, assembly=OSB.Mod
...
[INFO] -- MAIN MENU --
```

进程存活（PID 41243），稳定停留在主菜单，全程零 `LogError`、零堆栈登记。

| 判据 | 结果 |
|---|---|
| V-1 游戏启动不崩溃 | **PASS** |
| V-2 MOD 出现在列表且可启用 | **PASS** |
| V-3 三个 DLL 均被加载 | **PASS** |
| V-4 探针执行（`probe start` → `probe end`） | **PASS** |
| V-5 三 DLL 共存无 `more than one class inheriting UserMod2` | **PASS** |
| V-6 关键签名运行期一致 | **PASS（11/11）** |
| V-7 禁用 MOD 后仍可正常运行 | **PASS**（安全模式期间两个 MOD 均禁用，游戏正常进入主菜单） |

**V-6 明细**：`TryPlace` / `TryReplaceTile` / `Instantiate` / `IsValidPlaceLocation` / `RunOnArea` /
`MarkArea` / `Build` / `UserMod2.OnLoad` / `UserMod2.OnAllModsLoaded` / `DLLLoader.LoadDLLs` /
`DLLLoader.PostLoadDLLs` —— 11 项全部命中，`docs/API-Audit.md` 记录的签名与运行期一致。

### 2.4.1 一次崩溃事件与其根因

**时间线**

| 时刻 | 事件 |
|---|---|
| 15:32 | 首轮实测。探针抛 `NullReferenceException`（`ClusterManager.Instance` 未判空）并**用 `Debug.LogError` 上报** |
| — | ONI 扫描堆栈 → `adding to referenced mods list`，`OSB.GameAdapter` 被登记为疑似崩溃源 |
| ~15:33 | 游戏异常退出；`mods.json` 被改写为**全部 MOD `enabled: false`**（含用户原有的 `Atmo Suit Teleport`），用户看到报错弹窗 |
| 15:35 | 修复：全部游戏对象判空；`Debug.LogError` → `Debug.LogWarning`；签名参数计数修正 |
| 15:49 | 复测。同一份环境、同一组 MOD，**正常进入主菜单，零错误** |

**根因判断：`Debug.LogError` 触发的崩溃归因。** 依据是唯一变量原则——两轮之间只改了日志级别与判空，
结果从"崩溃 + 安全模式"变为"干净通过"。

**诚实标注**：未做重复实验排除偶发因素，故记为**强相关而非已证明**。若要坐实，需回退日志级别重跑一次观察是否复现；
出于不折腾用户真实环境与存档的考虑，本轮未做。此结论不影响 P0 判定，但若后续再出现同类现象，第一嫌疑点即此处。

**附带确认（E2）**：`OnAllModsLoaded` 时刻 `Grid.CellCount == 0`、`ClusterManager.Instance == null`、
`Game.Instance == null`、`Assets.BuildingDefs == null` —— 游戏数据库与世界上下文均在此之后初始化。
任何需要建筑定义或世界数据的逻辑必须推迟到 Db 初始化之后。

### 2.5 游戏内最小行为验证

**真实游戏内最小行为验证：PARTIAL**

探针在真实游戏内成功执行，输出了环境信息与 11 项签名核对结果，这部分构成有效的游戏内行为证据，
且验证了三程序集部署形态与多 DLL 加载规则（A-001）。

未完成部分：**建筑几何采样**（`Assets.BuildingDefs` 在 `OnAllModsLoaded` 时为 null），
需推迟到 Db 初始化后执行。该项列入 P1 首要任务，不阻塞 P0 收口。

未做任何世界写入行为（P0 范围外）。

---

## 3. 已确认 API（摘要）

完整条目、签名与风险见 `docs/API-Audit.md`。本轮共核实 **17 条**（A-001 ~ A-017），证据全部为
E1（反编译游戏程序集）。核心结论：

| 编号 | 结论 | 影响 |
|---|---|---|
| A-001 | 加载器遍历 MOD 目录所有 `.dll`；**同一程序集内 ≥2 个 `UserMod2` 子类会导致整个 MOD 加载中止** | 决定三程序集部署形态合法 |
| A-005 | `BuildingDef.Instantiate` 使用 `BuildingUnderConstruction` 预制体 + `Constructable` | **"只下施工计划、不瞬间建成"有原生接口** |
| A-006 | `BuildTool.TryBuild` 正常路径调 `TryPlace`，仅 `InstantBuildMode` 才调 `Build` | 官方自己划清了"施工"与"瞬间建成" |
| A-007 | `BuildingDef.Build` 产出 `BuildingComplete` | **产品代码禁止调用** |
| A-008 | `IsValidPlaceLocation` 返回 `out string fail_reason`（LocString 文案） | 可直接用于"错误可解释" |
| A-004 | 建筑占位 = `WidthInCells` × `HeightInCells` + `PlacementOffsets[]` | 证实多格 footprint，且存在锚点偏移 |
| A-012 | `Orientation` 含 `NumRotations` 哨兵值 | 遍历朝向时必须跳过 |

---

## 4. 未知 / 阻塞 API

| 编号 | 项目 | 状态 |
|---|---|---|
| UNKNOWN-1 | `Grid.XYToCell` 的 y 轴方向 | 未经往返测试确认 |
| UNKNOWN-2 | 科技解锁读取（`Tech` / `TechTree`） | 未审计 |
| UNKNOWN-3 | 施工任务的可撤销句柄 | 未找到可靠标识，倾向 P2 不做自动撤销 |
| UNKNOWN-4 | 材料可用性读取（`ElementLoader`） | 未审计 |
| UNKNOWN-5 | `GameAdapter` 内 Harmony 补丁能否经自动补出的空 `UserMod2` 实例生效 | E4 推断，待 P2 实测 |
| UNKNOWN-6 | 自定义组件在存档中的安全存放 | 未设计 |
| BLOCKED-1 | 第三方 LICENSE 原文核对（Mayall / Sgt_Imalas） | **网络不可用，未核实**。方案文档的相关结论仅为二手信息 |

---

## 5. 验收清单（全部已执行）

| 编号 | 项目 | 判定标准 | 结果 |
|---|---|---|---|
| V-1 | 游戏能启动且不崩溃 | 进程存活、`Player.log` 无异常堆栈 | **PASS** |
| V-2 | MOD 出现在 Mod 列表且可启用 | 列表中有 "ONI Smart Blueprint" | **PASS** |
| V-3 | MOD 被加载 | `Loading MOD dll: OSB.*.dll` 三条齐全 | **PASS** |
| V-4 | 探针执行成功 | `probe start` 至 `probe end` 完整 | **PASS** |
| V-5 | 三 DLL 共存无冲突 | 无 `more than one class inheriting UserMod2` | **PASS** |
| V-6 | 关键签名运行期一致 | 11 项全部命中 | **PASS** |
| V-7 | 存档安全 | 禁用 MOD 后仍可正常运行 | **PASS** |

复核命令：

```bash
grep '\[OSB' "$HOME/Library/Logs/Klei/Oxygen Not Included/Player.log"
```

**遗留项（不阻塞 P0，转入 P1）**

| 编号 | 项目 | 说明 |
|---|---|---|
| D-1 | 建筑几何采样 | `Assets.BuildingDefs` 在 `OnAllModsLoaded` 时为 null。需在 Db 初始化后（如首帧 `Game.Update` postfix）执行，顺带验证 UNKNOWN-5 |
| D-2 | 存档兼容性专项 | 本轮未读写任何存档；"写入自定义组件后禁用 MOD 仍可读档"需在 P2 配合测试存档验证 |

---

## 6. 变更文件

**新增（全部在新仓库 `~/ONI-Smart-Blueprint/`，未触碰 `~/ONIMod/`）**

| 文件 | 作用 |
|---|---|
| `AGENTS.md` | 开发规则与硬约束 |
| `README.md` | 项目说明 |
| `.gitignore` | 排除 bin/obj 与游戏专有 DLL |
| `Directory.Build.props` | `GameManagedDir` 三档回退解析 |
| `OSB.slnx` | 解决方案 |
| `build.sh` | 构建/测试/部署脚本 |
| `OSB.Domain/OSB.Domain.csproj` + `Point2i.cs`、`Rect2i.cs`、`Footprint.cs`、`LayerKind.cs`、`BuildingRecord.cs`、`Blueprint.cs` | 领域层 |
| `OSB.GameAdapter/OSB.GameAdapter.csproj` + `GameProbe.cs` | 游戏适配层 + P0 只读探针 |
| `OSB.Mod/OSB.Mod.csproj` + `OsbMod.cs`、`mod.yaml`、`mod_info.yaml`、`Properties/AssemblyInfo.cs` | MOD 入口 |
| `tests/OSB.Domain.Tests/`（4 个 .cs + csproj） | 零依赖测试运行器 |
| `docs/API-Audit.md` | 17 条 API 审计 |
| `docs/Game-Build-Matrix.md` | 版本与工具链矩阵 |
| `docs/Phase-Reports/P0-report.md` | 本文件 |
| `docs/Phase-Reports/CONTEXT-HANDOFF.md` | 上下文交接 |

**修改**：无（新仓库，无既有文件）

**写入工作区外**：仅 `mods/Dev/ONISmartBlueprint/`（自建 MOD 目录），未修改游戏安装目录与存档。

---

## 7. 风险与阻塞

| 风险 | 等级 | 说明与缓解 |
|---|---|---|
| 崩溃归因未做重复实验 | 中 | "`LogError` 触发崩溃归因"是唯一变量推断，非严格复现证明。若后续再出现同类现象，第一嫌疑点即此处 |
| 无网络导致许可未核实 | **中** | 在核实前禁止复制任何第三方代码，已写入 `AGENTS.md` 第 6 节 |
| 无正规模架 | 低 | 自建运行器无并发/筛选/报告能力；恢复网络后迁移 |
| `Grid` 坐标方向未定 | **中** | P1 必须先用往返测试锁定，否则所有坐标换算都不可信 |
| 数据库时序限制 | 中 | Db 在 `OnAllModsLoaded` 之后才初始化，所有建筑数据逻辑必须推迟，已记入 `AGENTS.md` 第 4 节 |

---

## 8. 本阶段完成判断

```text
环境检查：PASS
编译：    PASS
单元测试：PASS（28 用例 / 71 断言）
MOD 文件部署：PASS
游戏加载：PASS（三 DLL 全部加载，进程稳定）
游戏内行为：PARTIAL（签名核对 11/11 通过；建筑几何采样因数据库时序推迟到 P1）
P0 状态：PASS
```

判定理由：P0 的四项核心目标——环境审计、游戏 API 核验、技术可行性验证、最小工程骨架——均已达成且各有证据：

1. **环境审计**：版本矩阵与路径矩阵实测确认，并修正了方案文档中 `mods/Local` 的错误说法。
2. **游戏 API 核验**：17 条 API 经反编译核实（E1），其中 11 项签名在真实游戏内二次确认（E2）。
3. **技术可行性**：三程序集部署形态成立；多 DLL 加载规则确证；"只下施工计划"路径找到原生接口。
4. **最小工程骨架**：可编译、可部署、领域层有 28 个用例覆盖。

唯一 PARTIAL 项是建筑几何采样，原因是执行时机而非技术障碍，已明确列为 P1 首项。

---

## 9. 下一阶段建议（仅建议，不执行）

1. **P1 第一件事**：把探针的建筑几何采样移到 Db 初始化后（首帧 `Game.Update` postfix），
   顺带验证 UNKNOWN-5（GameAdapter 内的 Harmony 补丁能否由自动补出的空 `UserMod2` 实例生效）。
   这一步同时产出真实建筑 footprint 数据，是蓝图数据模型的输入。
2. **P1 第二件事**：写 `CellOffset ↔ (x,y)` 往返测试并实地比对，锁定 `Grid` 坐标方向（UNKNOWN-1）。
   在此完成前不要写任何放置代码。
3. P1 同时补审 `Tech` / `ElementLoader`（UNKNOWN-2 / UNKNOWN-4），否则 FR-10 无法落地。
4. 恢复网络后补做第三方 LICENSE 原文核对（BLOCKED-1）。
5. 建议在动任何写世界的代码之前，先建一个专用测试存档。

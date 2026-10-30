# CONTEXT-HANDOFF.md —— 上下文交接

> 每轮开工先读本文件。它记录"上一轮真实做到哪一步、哪些是已证实的、哪些只是假设"。
> 最后更新：2026-10-30（P1 完成，状态 **PASS**）

---

## 1. 三十秒摘要

- 项目已推进到 **P1**：在真实游戏内采集到 **424 个建筑的完整几何**，Grid 坐标规则 **16/16 项核实通过**。
- 环境全部实测：ONI **744825**、Unity **6000.3.5f2**、Harmony **2.4.2.0**、SDK **10.0.301**、TFM `netstandard2.1`。
- **三程序集部署形态成立**，补丁显式挂载（非特性路径）已在游戏内生效。
- 五条最关键的技术结论：
  1. 多 DLL 部署合法（每程序集最多一个 `UserMod2` 子类）；
  2. "只下施工计划"用 `BuildingDef.TryPlace`，不是 `Build`；
  3. 采集时机必须挂在 `Assets.CreatePrefabs` 之后（`OnAllModsLoaded` 时数据库还没建）；
  4. **建筑水平居中锚定**：宽度 ≥3 时 `PlacementOffsets` 的 x 为负，落位必须用 `AnchorInFootprint`；
  5. **`WidthInCells`/`HeightInCells` 只描述视觉**，实际占位以 `PlacementOffsets` 为准
     （吊兰声明 1x2 实际占 1x1）。
- **踩过两个会造成实际损失的坑**：
  1. MOD 加载期用 `Debug.LogError` 会让 ONI 把该 MOD 登记为疑似崩溃源，触发安全模式禁用全部 MOD；
  2. 用"偏移必须从 (0,0) 起算"判占位合法性 → 424 个建筑里误报 149 个。
  两条都已写成项目级硬规则（`AGENTS.md` 第 4 节与 `docs/API-Audit.md`）。

---

## 2. 环境事实（复用这些，不要重新摸索）

```
游戏构建   U59-744825-S (release)      ← Player.log
Unity      6000.3.5f2
Harmony    2.4.2.0                     ← 0Harmony.dll
SDK        10.0.301（唯一）
TFM        netstandard2.1              ← 实测可用
Managed    ~/Library/Application Support/Steam/steamapps/common/OxygenNotIncluded/OxygenNotIncluded.app/Contents/Resources/Data/Managed
Dev MOD    ~/Library/Application Support/unity.Klei.Oxygen Not Included/mods/Dev/ONISmartBlueprint
日志       ~/Library/Logs/Klei/Oxygen Not Included/Player.log
反编译     ~/.dotnet/tools/ilspycmd (11.0.0.9375)
```

**四个环境坑（每个都会浪费你半小时）**

1. **网络不通**：`curl` 全部走 `127.0.0.1:1082` 代理且代理未启动。NuGet 拉不到包，第三方 LICENSE 也查不了。
   本地 NuGet 缓存有 `PLib 4.25.0` 和 `lib.harmony.ref`，但没有 xunit / Test.Sdk。
2. **macOS 没有 `mods/Local`**，只有 `mods/Dev` 和 `mods/Steam`。方案文档 12.2 节那条路径是错的。
3. **`OnAllModsLoaded` 时游戏数据库还没初始化**：`Grid.CellCount==0`、`ClusterManager.Instance==null`、
   `Game.Instance==null`、`Assets.BuildingDefs==null`。需要建筑或世界数据的逻辑必须推到更晚。
4. **Assembly-CSharp 里有一个全局 `Action` 枚举**（`public Action HotKey = Action.NumActions;`），
   会遮蔽 `System.Action`。在 GameAdapter 里写委托参数必须写 `System.Action`，否则编译报
   `无法从"方法组"转换为"Action"` + `CS0149`。
5. **系统代理是死的，但直连是通的**。shell 里 `HTTP_PROXY=127.0.0.1:1082` 指向一个未启动的代理，
   导致 `curl`/`git` 默认全部失败。`curl --noproxy '*'` 与 `git` 绕过代理后访问 GitHub 正常。
   本仓库已设 `local http.proxy=""` / `https.proxy=""` 固化这一点；若换机器或重装需重新设置。

---

## 2.1 远程仓库

```
origin  https://github.com/Yxx0723/ONI-Smart-Blueprint.git   (Apache-2.0)
```

- 凭证：macOS keychain（`credential.helper=osxkeychain`），HTTPS 推送无需 token。
- 提交身份已设为 `Yxx0723 <103908637+Yxx0723@users.noreply.github.com>`。
- 游戏专有 DLL、`bin/`、`obj/`、反编译产物均在 `.gitignore` 中，**不得提交**。

---

## 3. 仓库现状

```
~/ONI-Smart-Blueprint/
├── AGENTS.md                    开发规则（每轮必读）
├── README.md
├── build.sh                     debug / test / release 三种模式
├── Directory.Build.props        GameManagedDir 三档解析
├── OSB.slnx
├── OSB.Domain/                  6 个 .cs，零游戏依赖
├── OSB.GameAdapter/             GameProbe.cs（P0 只读探针）
├── OSB.Mod/                     OsbMod.cs + mod.yaml + mod_info.yaml
├── tests/OSB.Domain.Tests/      零依赖测试运行器（28 用例 / 71 断言）
└── docs/
    ├── API-Audit.md             17 条已核实 API + 6 个 UNKNOWN
    ├── Game-Build-Matrix.md
    └── Phase-Reports/
        ├── P0-report.md
        └── CONTEXT-HANDOFF.md   ← 本文件
```

git 已 `init`，**尚未提交**。是否提交由用户决定。

---

## 4. 下一轮开工清单

### 4.1 先做事（按顺序）

1. **D-1：把建筑几何采样移到 Db 初始化之后**（`P0-report.md` 第 5 节）。
   建议用首帧 `Game.Update` postfix，跑一次即自我停用。这一步同时验证 UNKNOWN-5
   （GameAdapter 内补丁能否由自动补出的空 `UserMod2` 实例生效）——这是 P1 其余工作的前提。
2. **UNKNOWN-1：锁定 `Grid` 坐标方向**。写完往返测试并实地比对之前，不要写任何放置代码。
3. 补审 `Tech` / `ElementLoader`（UNKNOWN-2 / UNKNOWN-4）。

### 4.2 不要做的事

- 不要用 `Debug.LogError` 上报可预期的诊断信息（会导致 ONI 把 MOD 登记为崩溃嫌疑人）。
- 不要在 `OnAllModsLoaded` 里访问 `Assets.BuildingDefs` / `ClusterManager.Instance` / `Game.Instance`。
- 不要在 UNKNOWN-1 未解决时假设 `XYToCell(x,y)` 的 y 轴方向。
- 不要调用 `BuildingDef.Build`（瞬间建成，产品禁止）。
- 不要从 Mayall / Sgt_Imalas 仓库复制代码 —— 许可原文还没核对过。
- 不要动 `~/ONIMod`（用户的另一个 MOD 工程，有 13 处未提交改动）。
- 不要读写用户真实存档；需要游戏内验证时先建专用测试存档。

---

## 5. 设计决策记录（为什么这么做）

| 决策 | 理由 |
|---|---|
| 三程序集而非单程序集 | **已实测成立**：三个 DLL 同时加载无冲突（V-5 PASS） |
| `OsbMod` 类名而非 `Mod` | `OSB.Mod` 命名空间下再叫 `Mod` 会与 `KMod.Mod` 混淆 |
| `GameProbe` 用 `try/catch` 包裹每段 | 探针失败绝不允许影响游戏启动 |
| 探针全程 `Log`/`LogWarning`，绝不用 `LogError` | 实测教训：`LogError` 会让 ONI 把 MOD 登记为崩溃嫌疑人，触发安全模式禁用全部 MOD |
| 自建测试运行器 | 无网络的务实选择，非偷懒。恢复网络后迁移 |
| `Instantiate`/`TryPlace` 而非 `Build` | 官方 `BuildTool` 自己就是这么分的（A-006） |
| Domain 的 `RotateClockwise90` 只用于规划 | 真落位必须走 `Rotatable.GetRotatedCellOffset`（A-013） |

---

## 6. 尚未决策、留给用户的事

| 议题 | 说明 |
|---|---|
| 是否 `git commit` 首版 | 仓库已 init 未提交 |
| UI 挂载点方案 | 三条候选（PlanScreen 加按钮 / 独立 KScreen + 热键 / 接管 BuildTool），P2 才需定 |
| 施工任务撤销 | UNKNOWN-3 未找到可靠标识；方案文档倾向"不可靠就不做" |
| 与 Blueprints Expanded 的差异定位 | 功能高度重叠，需产品层面决策 |

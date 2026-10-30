# AGENTS.md — ONI Smart Blueprint 开发规则

本仓库开发《缺氧》（Oxygen Not Included）MOD「ONI Smart Blueprint（OSB）」。

## 0. 每轮开工前必读

1. `docs/API-Audit.md` —— 已核实与未核实的游戏 API 清单，任何游戏类型调用都要先在这里找到依据。
2. `docs/Game-Build-Matrix.md` —— 目标游戏构建与工具链矩阵。
3. `docs/Phase-Reports/` 下最新一期报告与 `CONTEXT-HANDOFF.md` —— 上轮真实进度与阻塞。
4. 本文件。

## 1. 阶段纪律

1. 只执行当前收到的阶段任务；未经用户明确授权不得提前实施后续阶段。
2. 每轮只推进一个阶段（及其细分任务），完成即停止并等待验收。
3. 如果发现任务超出已确认边界，先在报告中说明阻塞与替代方案，不静默扩大修改范围。

## 2. 硬性技术约束

1. **任何游戏内部类、方法、字段名称必须从实际引用的游戏 DLL 核实，不可猜测。**
   本项目已建立核实手段：`ilspycmd` 反编译 + `docs/API-Audit.md` 登记。新 API 必须先登记再使用。
2. `OSB.Domain` 不得引用 Unity、Harmony 或 `Assembly-CSharp`。这条一旦破坏，核心逻辑就无法离线单测。
3. 所有 Harmony 补丁集中在 `OSB.GameAdapter`，必须有兼容检测、错误日志与失败降级。
4. 预览阶段严禁修改世界；最终提交前必须复验当前世界状态；建造只能通过游戏原生施工任务接口。
5. 所有数据格式必须版本化，并提供读写、异常输入与迁移测试。
6. 每个改动按最小功能切片推进：先测试，后实现，再构建、回归、报告。

## 3. 部署与程序集规则（已实证，见 docs/API-Audit.md A-001）

1. ONI 加载器会 `Assembly.LoadFrom` MOD 目录下**每一个** `.dll`。
2. **同一程序集内最多只能有一个 `UserMod2` 子类**，出现两个会导致整个 MOD 加载中止。
3. 不含 `UserMod2` 子类的程序集在 Dev 模式下会被自动补一个空实例，其 `OnLoad` 会执行该程序集的 `PatchAll` —— 这正是 `OSB.GameAdapter` 可以承载 Harmony 补丁的原因。
4. 因此部署形态固定为：`OSB.Domain.dll` + `OSB.GameAdapter.dll` + `OSB.Mod.dll` + `mod.yaml` + `mod_info.yaml`。
5. 游戏程序集引用一律 `Private=False`，**绝不**把 Klei 的 DLL 复制进 MOD 目录或提交到仓库。

## 4. 日志纪律（首轮实测踩过的坑，会造成实际损失）

1. **可预期的诊断信息一律用 `Debug.Log` / `Debug.LogWarning`，禁止用 `Debug.LogError`。**
   ONI 会扫描异常堆栈并把命中的程序集登记进"疑似肇事 MOD"列表：

   ```
   ONI Smart Blueprint's assembly declared the method ... in the stack trace,
   adding to referenced mods list
   ```

   一旦被登记，游戏因**任何**原因崩溃时该 MOD 都会被算作崩溃源，进而触发安全模式
   （`mods.json` 中全部 MOD 被置为 `enabled: false`）。实测验证：同一份 DLL 仅把
   `LogError` 换成 `LogWarning`，结果从"崩溃 + 全 MOD 被禁用"变为"正常进入主菜单"。
2. **访问任何游戏对象前先判空。** `OnAllModsLoaded` 阶段 `ClusterManager.Instance`、
   `Game.Instance`、`Assets.BuildingDefs` 均为 null，游戏数据库尚未初始化。
3. MOD 加载期的日志前缀统一为 `[OSB]`，便于 `grep '\[OSB' Player.log` 定位。

## 5. 安全边界

1. 不更改游戏 DLL 或 Steam 原始安装内容。
2. 不读取、不覆盖、不迁移用户真实存档。需要游戏内验证时先提出独立测试存档方案并等待授权。
3. 不执行 `rm -rf`、`git clean -xfd`、`git reset --hard` 等破坏性命令。
4. 不把 API Key、令牌或其他凭证写入源码、日志、文档或 Git。
5. 不推送远程、不创建 PR、不发布 Steam Workshop，除非用户明确要求。

## 6. 许可边界

1. 不复制许可不允许复用的第三方源码。
2. **不得假定 Blueprints Expanded 全部代码可用**。注意：本仓库当前**未能**在线核对任何第三方 LICENSE 原文（首次审计时网络不可用），引用前必须重新核对。
3. 研究公开实现的结构与思路属于合理范围；复制代码不属于。

## 7. 报告纪律

1. 编译成功 ≠ 游戏内运行成功。必须分列：环境检查 / 编译 / 单元测试 / MOD 部署 / 游戏加载 / 游戏内行为，各自 PASS、FAIL 或 NOT RUN。
2. 无法在游戏里测试时，如实标注 `NOT RUN` 或 `BLOCKED`，不得用占位实现或伪造日志让验收通过。
3. 每阶段产出 `docs/Phase-Reports/P?-report.md` 与更新 `docs/Phase-Reports/CONTEXT-HANDOFF.md`。

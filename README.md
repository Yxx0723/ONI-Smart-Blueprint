# ONI Smart Blueprint（OSB）

《缺氧》（Oxygen Not Included）智能蓝图与模块自动生成 MOD。

> **当前状态：P0 技术验证中，不可用于正常游戏。** 本版本只做只读诊断（把游戏版本、
> 建筑几何特征、关键 API 签名写入日志），不创建建筑、不修改存档、不改变游戏行为。

## 目录结构

```
OSB.Domain/        纯 C# 领域模型与规则（禁止引用游戏程序集，可离线单测）
OSB.GameAdapter/   唯一接触 ONI 类型的层；Harmony 补丁集中于此
OSB.Mod/           MOD 入口（UserMod2）+ mod.yaml / mod_info.yaml
tests/             零依赖测试运行器
docs/              API 审计、构建矩阵、阶段报告
```

## 快速开始

```bash
./build.sh test      # 只跑单元测试（不需要游戏）
./build.sh           # 编译 Debug 并部署到游戏 Dev mod 目录
./build.sh release   # 编译 Release 到 bin/Release
```

部署目标：`~/Library/Application Support/unity.Klei.Oxygen Not Included/mods/Dev/ONISmartBlueprint/`

## 查看运行日志

```bash
grep '\[OSB' "$HOME/Library/Logs/Klei/Oxygen Not Included/Player.log"
```

## 前置依赖

- 本机已安装 ONI（Steam 版），构建时从游戏 Managed 目录引用 `Assembly-CSharp.dll` 等程序集；
  **这些程序集不会被复制进仓库或 MOD 目录**。
- .NET SDK 8+（本项目在 10.0.301 上验证）。

## 文档索引

| 文件 | 内容 |
|---|---|
| `AGENTS.md` | 开发规则与硬约束（每轮开工必读） |
| `docs/API-Audit.md` | 已核实的游戏 API 清单与风险 |
| `docs/Game-Build-Matrix.md` | 版本、工具链、路径矩阵 |
| `docs/Phase-Reports/` | 各阶段报告与上下文交接 |

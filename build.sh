#!/bin/bash
# =============================================================================
# OSB 一键构建 + 部署脚本
#
#   用法：
#     ./build.sh            编译并部署 Debug 版到游戏 Dev mod 目录
#     ./build.sh test       只跑单元测试
#     ./build.sh release    编译 Release 版到 bin/Release（供打包）
#
#   环境变量（可选，覆盖默认路径）：
#     ONI_GAME_MANAGED_DIR   游戏 Managed 程序集目录
#     ONI_DEV_MOD_DIR        游戏 Dev mod 目录
#
#   注意：本脚本只写自己的 MOD 目录，不触碰游戏安装目录与存档。
# =============================================================================
set -euo pipefail

cd "$(dirname "$0")"

MODE="${1:-debug}"

run_tests() {
    echo "==> 单元测试（OSB.Domain，无需游戏环境）"
    dotnet run --project tests/OSB.Domain.Tests/OSB.Domain.Tests.csproj -v q --nologo
}

case "$MODE" in
    test)
        run_tests
        ;;

    release)
        echo "==> 编译 Release"
        dotnet build OSB.Mod/OSB.Mod.csproj -c Release -v q --nologo
        echo "==> 产物：OSB.Mod/bin/Release/"
        ls -1 OSB.Mod/bin/Release/ | sed 's/^/    /'
        ;;

    debug)
        run_tests

        echo "==> 编译 Debug 并部署到游戏 Dev 目录"
        dotnet build OSB.Mod/OSB.Mod.csproj -c Debug -v q --nologo

        echo "==> 部署结果"
        DEV_DIR="${ONI_DEV_MOD_DIR:-$HOME/Library/Application Support/unity.Klei.Oxygen Not Included/mods/Dev/ONISmartBlueprint}"
        ls -la "$DEV_DIR" | sed 's/^/    /'

        echo
        echo "==> 完成。启动游戏后在 Mod 列表中应看到 \"ONI Smart Blueprint\"。"
        echo "    日志：$HOME/Library/Logs/Klei/Oxygen Not Included/Player.log"
        echo "    检索：grep '\\[OSB' \"$HOME/Library/Logs/Klei/Oxygen Not Included/Player.log\""
        ;;

    *)
        echo "未知模式：$MODE（可用：debug / test / release）" >&2
        exit 2
        ;;
esac

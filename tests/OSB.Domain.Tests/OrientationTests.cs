using System;
using System.Collections.Generic;

namespace OSB.Domain.Tests
{
    /// <summary>
    /// 朝向变换测试。
    ///
    /// 期望值**不是**我自己推导的，而是逐条抄自游戏
    /// <c>Rotatable.GetRotatedCellOffset</c> 的反编译结果（docs/API-Audit.md R-001）。
    /// 任何一条不过，说明领域层与游戏不一致，必须停下来修。
    /// </summary>
    internal static class OrientationTests
    {
        internal static void RotateMatchesGameFormulas()
        {
            var p = new Point2i(3, 5);

            // 游戏源码：R90 => (y, -x)
            Check.Equal(new Point2i(5, -3), GeometryOrientation.Rotate(p, BuildingOrientation.R90), "R90 => (y, -x)");
            // R180 => (-x, -y)
            Check.Equal(new Point2i(-3, -5), GeometryOrientation.Rotate(p, BuildingOrientation.R180), "R180 => (-x, -y)");
            // R270 => (-y, x)
            Check.Equal(new Point2i(-5, 3), GeometryOrientation.Rotate(p, BuildingOrientation.R270), "R270 => (-y, x)");
            // FlipH => (-x, y)
            Check.Equal(new Point2i(-3, 5), GeometryOrientation.Rotate(p, BuildingOrientation.FlipH), "FlipH => (-x, y)");
            // FlipV => (x, -y)
            Check.Equal(new Point2i(3, -5), GeometryOrientation.Rotate(p, BuildingOrientation.FlipV), "FlipV => (x, -y)");
            // Neutral => 恒等
            Check.Equal(new Point2i(3, 5), GeometryOrientation.Rotate(p, BuildingOrientation.Neutral), "Neutral => 恒等");
        }

        internal static void SentinelIsRejectedNotSilentlyIdentity()
        {
            // 游戏 default 分支会把哨兵当恒等变换静默放行，本领域层刻意改为抛异常
            Check.Throws<ArgumentOutOfRangeException>(
                () => GeometryOrientation.Rotate(new Point2i(1, 1), BuildingOrientation.NumRotations),
                "NumRotations 必须抛异常，不能静默返回原值");

            Check.True(GeometryOrientation.IsSentinel(BuildingOrientation.NumRotations), "IsSentinel 应识别哨兵");
            Check.False(GeometryOrientation.IsSentinel(BuildingOrientation.R90), "R90 不是哨兵");
            Check.False(GeometryOrientation.IsSentinel(BuildingOrientation.FlipV), "FlipV 不是哨兵");
        }

        internal static void RotationCompositionInvariants()
        {
            var p = new Point2i(4, -7);

            Point2i r90 = GeometryOrientation.Rotate(p, BuildingOrientation.R90);
            Point2i r90Twice = GeometryOrientation.Rotate(r90, BuildingOrientation.R90);
            Point2i r90FourTimes = GeometryOrientation.Rotate(GeometryOrientation.Rotate(r90Twice, BuildingOrientation.R90), BuildingOrientation.R90);
            Check.Equal(p, r90FourTimes, "R90 应用四次必须回到原点");

            Point2i r180Twice = GeometryOrientation.Rotate(
                GeometryOrientation.Rotate(p, BuildingOrientation.R180),
                BuildingOrientation.R180);
            Check.Equal(p, r180Twice, "R180 应用两次必须回到原点");

            // R90 后再 R270 应还原
            Point2i r90ThenR270 = GeometryOrientation.Rotate(r90, BuildingOrientation.R270);
            Check.Equal(p, r90ThenR270, "R90 后接 R270 必须还原");

            Point2i flipHTwice = GeometryOrientation.Rotate(
                GeometryOrientation.Rotate(p, BuildingOrientation.FlipH),
                BuildingOrientation.FlipH);
            Check.Equal(p, flipHTwice, "FlipH 两次必须还原");

            Point2i flipVTwice = GeometryOrientation.Rotate(
                GeometryOrientation.Rotate(p, BuildingOrientation.FlipV),
                BuildingOrientation.FlipV);
            Check.Equal(p, flipVTwice, "FlipV 两次必须还原");

            // FlipH 后接 FlipV 等价于 R180
            Point2i flipHThenV = GeometryOrientation.Rotate(
                GeometryOrientation.Rotate(p, BuildingOrientation.FlipH),
                BuildingOrientation.FlipV);
            Check.Equal(
                GeometryOrientation.Rotate(p, BuildingOrientation.R180),
                flipHThenV,
                "FlipH ∘ FlipV 应等价于 R180");
        }

        internal static void ValidOrientationSets()
        {
            Check.Equal(1, PermittedRotationInfo.ValidOrientations(PermittedRotation.Unrotatable).Count, "Unrotatable 只有 1 种朝向");
            Check.Equal(2, PermittedRotationInfo.ValidOrientations(PermittedRotation.R90).Count, "R90 有 2 种朝向");
            Check.Equal(4, PermittedRotationInfo.ValidOrientations(PermittedRotation.R360).Count, "R360 有 4 种朝向");
            Check.Equal(2, PermittedRotationInfo.ValidOrientations(PermittedRotation.FlipH).Count, "FlipH 有 2 种朝向");
            Check.Equal(2, PermittedRotationInfo.ValidOrientations(PermittedRotation.FlipV).Count, "FlipV 有 2 种朝向");

            // 任何形态的合法集合都不得包含哨兵
            foreach (PermittedRotation permitted in Enum.GetValues(typeof(PermittedRotation)))
            {
                IReadOnlyList<BuildingOrientation> set = PermittedRotationInfo.ValidOrientations(permitted);
                foreach (BuildingOrientation orientation in set)
                {
                    Check.False(GeometryOrientation.IsSentinel(orientation),
                        permitted + " 的合法朝向集合不应包含 NumRotations");
                }
            }

            Check.True(PermittedRotationInfo.IsValid(PermittedRotation.R360, BuildingOrientation.R270), "R360 允许 R270");
            Check.False(PermittedRotationInfo.IsValid(PermittedRotation.R90, BuildingOrientation.R180), "R90 不允许 R180");
            Check.False(PermittedRotationInfo.IsValid(PermittedRotation.R360, BuildingOrientation.NumRotations), "任何形态都不允许哨兵");
        }

        internal static void NextOrientationMirrorsGame()
        {
            // R360：Neutral → R90 → R180 → R270 → Neutral
            Check.Equal(BuildingOrientation.R90, PermittedRotationInfo.Next(PermittedRotation.R360, BuildingOrientation.Neutral), "R360 第一步");
            Check.Equal(BuildingOrientation.R180, PermittedRotationInfo.Next(PermittedRotation.R360, BuildingOrientation.R90), "R360 第二步");
            Check.Equal(BuildingOrientation.R270, PermittedRotationInfo.Next(PermittedRotation.R360, BuildingOrientation.R180), "R360 第三步");
            Check.Equal(BuildingOrientation.Neutral, PermittedRotationInfo.Next(PermittedRotation.R360, BuildingOrientation.R270), "R360 回到 Neutral");

            // R90 / FlipH / FlipV：两态切换
            Check.Equal(BuildingOrientation.R90, PermittedRotationInfo.Next(PermittedRotation.R90, BuildingOrientation.Neutral), "R90 切换");
            Check.Equal(BuildingOrientation.Neutral, PermittedRotationInfo.Next(PermittedRotation.R90, BuildingOrientation.R90), "R90 切回");
            Check.Equal(BuildingOrientation.FlipH, PermittedRotationInfo.Next(PermittedRotation.FlipH, BuildingOrientation.Neutral), "FlipH 切换");
            Check.Equal(BuildingOrientation.FlipV, PermittedRotationInfo.Next(PermittedRotation.FlipV, BuildingOrientation.Neutral), "FlipV 切换");

            // Unrotatable 永不改变
            Check.Equal(BuildingOrientation.Neutral, PermittedRotationInfo.Next(PermittedRotation.Unrotatable, BuildingOrientation.Neutral), "Unrotatable 保持不变");

            // 非法输入（哨兵 / 不属于该形态）先归一到 Neutral，再按形态给出下一朝向，不抛异常
            Check.Equal(BuildingOrientation.R90, PermittedRotationInfo.Next(PermittedRotation.R90, BuildingOrientation.NumRotations), "哨兵输入应归一到 Neutral 再切换");
            Check.Equal(BuildingOrientation.FlipH, PermittedRotationInfo.Next(PermittedRotation.FlipH, BuildingOrientation.R180), "不属于该形态的朝向应归一到 Neutral，再给出该形态的首个非中性朝向");

            // 转满一圈必须回到起点
            BuildingOrientation cursor = BuildingOrientation.Neutral;
            for (int i = 0; i < 4; i++)
            {
                cursor = PermittedRotationInfo.Next(PermittedRotation.R360, cursor);
            }

            Check.Equal(BuildingOrientation.Neutral, cursor, "R360 连按四次应回到 Neutral");
        }

        internal static void RotateAllPreservesOrderAndCount()
        {
            var offsets = new List<Point2i>
            {
                new Point2i(0, 0),
                new Point2i(1, 0),
                new Point2i(0, 1)
            };

            Point2i[] rotated = GeometryOrientation.RotateAll(offsets, BuildingOrientation.R90);
            Check.Equal(3, rotated.Length, "旋转后数量不变");
            Check.Equal(new Point2i(0, 0), rotated[0], "下标 0 对应原下标 0");
            Check.Equal(new Point2i(0, -1), rotated[1], "下标 1 对应原下标 1");
            Check.Equal(new Point2i(1, 0), rotated[2], "下标 2 对应原下标 2");
        }

        internal static void BoundsOfRotatedOffsets()
        {
            // 2x1 的水平占位，转 90° 后应变 1x2
            var offsets = new List<Point2i> { new Point2i(0, 0), new Point2i(1, 0) };

            Check.Equal(new Rect2i(0, 0, 2, 1), GeometryOrientation.BoundsOf(offsets, BuildingOrientation.Neutral), "Neutral 包围盒 2x1");

            Rect2i rotated = GeometryOrientation.BoundsOf(offsets, BuildingOrientation.R90);
            Check.Equal(1, rotated.Width, "R90 后宽变 1");
            Check.Equal(2, rotated.Height, "R90 后高变 2");

            Check.True(GeometryOrientation.BoundsOf(new List<Point2i>(), BuildingOrientation.Neutral).IsEmpty, "空集合包围盒为空");
        }

        internal static void FitsInSizeChecks()
        {
            var horizontal = new List<Point2i> { new Point2i(0, 0), new Point2i(1, 0) };

            // Neutral 下 FitsInSize 是有效的占位判定
            Check.True(GeometryOrientation.FitsInSize(horizontal, BuildingOrientation.Neutral, 2, 1), "2x1 占位装进 2x1");
            Check.False(GeometryOrientation.FitsInSize(horizontal, BuildingOrientation.Neutral, 1, 2), "2x1 占位装不进 1x2");
            Check.False(GeometryOrientation.FitsInSize(horizontal, BuildingOrientation.Neutral, 0, 1), "零尺寸一律不合格");
            Check.False(GeometryOrientation.FitsInSize(new List<Point2i>(), BuildingOrientation.Neutral, 2, 1), "空占位不合格");

            // 关键陷阱：旋转后原始偏移会出现负值（(1,0) 在 R90 下变 (0,-1)），
            // 所以 FitsInSize 在旋转朝向上会误判为"装不进"——这是正常的，
            // 说明它不该被用于旋转有效性判定。旋转有效性应看包围盒尺寸。
            Check.False(GeometryOrientation.FitsInSize(horizontal, BuildingOrientation.R90, 1, 2),
                "R90 后原始偏移含负值，FitsInSize 必然为 false —— 因此不可用于旋转判定");

            Rect2i rotated = GeometryOrientation.BoundsOf(horizontal, BuildingOrientation.R90);
            Check.Equal(1, rotated.Width, "R90 包围盒宽 1（长宽互换）");
            Check.Equal(2, rotated.Height, "R90 包围盒高 2（长宽互换）");
        }

        internal static void GameValueConversion()
        {
            Check.Equal(PermittedRotation.Unrotatable, PermittedRotationInfo.FromGameValue(0), "0 → Unrotatable");
            Check.Equal(PermittedRotation.R90, PermittedRotationInfo.FromGameValue(1), "1 → R90");
            Check.Equal(PermittedRotation.R360, PermittedRotationInfo.FromGameValue(2), "2 → R360");
            Check.Equal(PermittedRotation.FlipH, PermittedRotationInfo.FromGameValue(3), "3 → FlipH");
            Check.Equal(PermittedRotation.FlipV, PermittedRotationInfo.FromGameValue(4), "4 → FlipV");

            Check.Throws<ArgumentOutOfRangeException>(() => PermittedRotationInfo.FromGameValue(-1), "负数应抛异常");
            Check.Throws<ArgumentOutOfRangeException>(() => PermittedRotationInfo.FromGameValue(5), "越界值应抛异常");
        }

        internal static void OrientationEnumMatchesGameNumbers()
        {
            // 游戏 Orientation 枚举数值：Neutral=0,R90=1,R180=2,R270=3,NumRotations=4,FlipH=5,FlipV=6
            Check.Equal(0, (int)BuildingOrientation.Neutral, "Neutral 必须为 0");
            Check.Equal(1, (int)BuildingOrientation.R90, "R90 必须为 1");
            Check.Equal(2, (int)BuildingOrientation.R180, "R180 必须为 2");
            Check.Equal(3, (int)BuildingOrientation.R270, "R270 必须为 3");
            Check.Equal(4, (int)BuildingOrientation.NumRotations, "NumRotations 必须为 4");
            Check.Equal(5, (int)BuildingOrientation.FlipH, "FlipH 必须为 5");
            Check.Equal(6, (int)BuildingOrientation.FlipV, "FlipV 必须为 6");
        }
    }
}

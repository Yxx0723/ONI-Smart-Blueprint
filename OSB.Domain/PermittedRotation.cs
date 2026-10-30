using System;
using System.Collections.Generic;

namespace OSB.Domain
{
    /// <summary>
    /// <see cref="PermittedRotation"/> 的语义表。
    ///
    /// 合法朝向集合直接由游戏行为反推（已反编译核实 <c>Rotatable.Rotate</c>）：
    ///   Unrotatable → 只有 Neutral，调用 Rotate 不变
    ///   R90         → Neutral ↔ R90
    ///   R360        → Neutral → R90 → R180 → R270 → Neutral（实现为 (int)(orientation+1)%4）
    ///   FlipH       → Neutral ↔ FlipH
    ///   FlipV       → Neutral ↔ FlipV
    /// </summary>
    public static class PermittedRotationInfo
    {
        private static readonly BuildingOrientation[] OnlyNeutral =
        {
            BuildingOrientation.Neutral
        };

        private static readonly BuildingOrientation[] QuarterTurn =
        {
            BuildingOrientation.Neutral,
            BuildingOrientation.R90
        };

        private static readonly BuildingOrientation[] FullTurn =
        {
            BuildingOrientation.Neutral,
            BuildingOrientation.R90,
            BuildingOrientation.R180,
            BuildingOrientation.R270
        };

        private static readonly BuildingOrientation[] HorizontalFlip =
        {
            BuildingOrientation.Neutral,
            BuildingOrientation.FlipH
        };

        private static readonly BuildingOrientation[] VerticalFlip =
        {
            BuildingOrientation.Neutral,
            BuildingOrientation.FlipV
        };

        /// <summary>返回该旋转形态下全部合法朝向，首项恒为 Neutral。</summary>
        public static IReadOnlyList<BuildingOrientation> ValidOrientations(PermittedRotation permitted)
        {
            switch (permitted)
            {
                case PermittedRotation.R90:
                    return QuarterTurn;
                case PermittedRotation.R360:
                    return FullTurn;
                case PermittedRotation.FlipH:
                    return HorizontalFlip;
                case PermittedRotation.FlipV:
                    return VerticalFlip;
                default:
                    return OnlyNeutral;
            }
        }

        /// <summary>
        /// 该朝向是否属于合法集合。<see cref="BuildingOrientation.NumRotations"/> 永远返回 false。
        /// </summary>
        public static bool IsValid(PermittedRotation permitted, BuildingOrientation orientation)
        {
            if (GeometryOrientation.IsSentinel(orientation))
            {
                return false;
            }

            IReadOnlyList<BuildingOrientation> valid = ValidOrientations(permitted);
            for (int i = 0; i < valid.Count; i++)
            {
                if (valid[i] == orientation)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 模拟一次"玩家按 R 键"，返回下一个朝向。镜像游戏 <c>Rotatable.Rotate</c> 的行为。
        /// 若传入的当前朝向不在合法集合内（含哨兵值），先归一到 Neutral 再计算，不抛异常。
        /// </summary>
        public static BuildingOrientation Next(PermittedRotation permitted, BuildingOrientation current)
        {
            if (!IsValid(permitted, current))
            {
                current = BuildingOrientation.Neutral;
            }

            switch (permitted)
            {
                case PermittedRotation.R360:
                    // 游戏实现为 (Orientation)((int)(orientation + 1) % 4)，此处等价展开
                    switch (current)
                    {
                        case BuildingOrientation.Neutral:
                            return BuildingOrientation.R90;
                        case BuildingOrientation.R90:
                            return BuildingOrientation.R180;
                        case BuildingOrientation.R180:
                            return BuildingOrientation.R270;
                        default:
                            return BuildingOrientation.Neutral;
                    }

                case PermittedRotation.R90:
                case PermittedRotation.FlipH:
                case PermittedRotation.FlipV:
                    // 这三种形态都是"两态切换"
                    return current == BuildingOrientation.Neutral
                        ? FirstNonNeutral(permitted)
                        : BuildingOrientation.Neutral;

                default:
                    return BuildingOrientation.Neutral;
            }
        }

        private static BuildingOrientation FirstNonNeutral(PermittedRotation permitted)
        {
            switch (permitted)
            {
                case PermittedRotation.R90:
                    return BuildingOrientation.R90;
                case PermittedRotation.FlipH:
                    return BuildingOrientation.FlipH;
                case PermittedRotation.FlipV:
                    return BuildingOrientation.FlipV;
                default:
                    return BuildingOrientation.Neutral;
            }
        }

        /// <summary>把游戏的 PermittedRotations 整数值转成领域枚举，非法值抛异常。</summary>
        public static PermittedRotation FromGameValue(int value)
        {
            if (value < 0 || value > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "不是合法的 PermittedRotations 取值（0..4）");
            }

            return (PermittedRotation)value;
        }
    }
}

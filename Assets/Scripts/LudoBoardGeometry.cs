using System.Collections.Generic;
using UnityEngine;

namespace PremiumLudo
{
    public static class LudoBoardGeometry
    {
        public const int BoardSize = 15;
        public const int TokensPerColor = 4;
        public const int CommonPathLength = 52;
        public const int HomeLaneLength = 6;
        public const int FinalProgress = CommonPathLength + HomeLaneLength - 1;

        public static readonly Vector2Int GoalCoordinate = new Vector2Int(7, 7);

        private static readonly Vector2Int[] s_CommonPath =
        {
            new Vector2Int(6, 1), new Vector2Int(6, 2), new Vector2Int(6, 3), new Vector2Int(6, 4),
            new Vector2Int(6, 5), new Vector2Int(5, 6), new Vector2Int(4, 6), new Vector2Int(3, 6),
            new Vector2Int(2, 6), new Vector2Int(1, 6), new Vector2Int(0, 6), new Vector2Int(0, 7),
            new Vector2Int(0, 8), new Vector2Int(1, 8), new Vector2Int(2, 8), new Vector2Int(3, 8),
            new Vector2Int(4, 8), new Vector2Int(5, 8), new Vector2Int(6, 9), new Vector2Int(6, 10),
            new Vector2Int(6, 11), new Vector2Int(6, 12), new Vector2Int(6, 13), new Vector2Int(6, 14),
            new Vector2Int(7, 14), new Vector2Int(8, 14), new Vector2Int(8, 13), new Vector2Int(8, 12),
            new Vector2Int(8, 11), new Vector2Int(8, 10), new Vector2Int(8, 9), new Vector2Int(9, 8),
            new Vector2Int(10, 8), new Vector2Int(11, 8), new Vector2Int(12, 8), new Vector2Int(13, 8),
            new Vector2Int(14, 8), new Vector2Int(14, 7), new Vector2Int(14, 6), new Vector2Int(13, 6),
            new Vector2Int(12, 6), new Vector2Int(11, 6), new Vector2Int(10, 6), new Vector2Int(9, 6),
            new Vector2Int(8, 5), new Vector2Int(8, 4), new Vector2Int(8, 3), new Vector2Int(8, 2),
            new Vector2Int(8, 1), new Vector2Int(8, 0), new Vector2Int(7, 0), new Vector2Int(6, 0),
        };

        private static readonly IReadOnlyList<LudoTokenColor> s_ClockwiseColors = new[]
        {
            LudoTokenColor.Red,
            LudoTokenColor.Green,
            LudoTokenColor.Yellow,
            LudoTokenColor.Blue,
        };

        private static readonly Vector2Int[] s_RedHomeLane =
        {
            new Vector2Int(1, 7), new Vector2Int(2, 7), new Vector2Int(3, 7),
            new Vector2Int(4, 7), new Vector2Int(5, 7), new Vector2Int(6, 7),
        };

        private static readonly Vector2Int[] s_GreenHomeLane =
        {
            new Vector2Int(7, 13), new Vector2Int(7, 12), new Vector2Int(7, 11),
            new Vector2Int(7, 10), new Vector2Int(7, 9), new Vector2Int(7, 8),
        };

        private static readonly Vector2Int[] s_YellowHomeLane =
        {
            new Vector2Int(13, 7), new Vector2Int(12, 7), new Vector2Int(11, 7),
            new Vector2Int(10, 7), new Vector2Int(9, 7), new Vector2Int(8, 7),
        };

        private static readonly Vector2Int[] s_BlueHomeLane =
        {
            new Vector2Int(7, 1), new Vector2Int(7, 2), new Vector2Int(7, 3),
            new Vector2Int(7, 4), new Vector2Int(7, 5), new Vector2Int(7, 6),
        };

        private static readonly Vector2Int[] s_RedRoute = BuildRoute(GetStartPathIndex(LudoTokenColor.Red), s_RedHomeLane);
        private static readonly Vector2Int[] s_GreenRoute = BuildRoute(GetStartPathIndex(LudoTokenColor.Green), s_GreenHomeLane);
        private static readonly Vector2Int[] s_YellowRoute = BuildRoute(GetStartPathIndex(LudoTokenColor.Yellow), s_YellowHomeLane);
        private static readonly Vector2Int[] s_BlueRoute = BuildRoute(GetStartPathIndex(LudoTokenColor.Blue), s_BlueHomeLane);

        private static readonly Vector2[] s_RedHomeCircles =
        {
            new Vector2(1.68f, 11.98f),
            new Vector2(3.38f, 11.98f),
            new Vector2(1.68f, 10.28f),
            new Vector2(3.38f, 10.28f),
        };

        private static readonly Vector2[] s_GreenHomeCircles =
        {
            new Vector2(10.70f, 11.98f),
            new Vector2(12.46f, 11.98f),
            new Vector2(10.70f, 10.28f),
            new Vector2(12.46f, 10.28f),
        };

        private static readonly Vector2[] s_BlueHomeCircles =
        {
            new Vector2(1.68f, 2.98f),
            new Vector2(3.38f, 2.98f),
            new Vector2(1.68f, 1.28f),
            new Vector2(3.38f, 1.28f),
        };

        private static readonly Vector2[] s_YellowHomeCircles =
        {
            new Vector2(10.70f, 2.98f),
            new Vector2(12.44f, 2.98f),
            new Vector2(10.70f, 1.28f),
            new Vector2(12.44f, 1.28f),
        };

        private static readonly Dictionary<LudoTokenColor, Vector2Int[]> s_Routes = new Dictionary<LudoTokenColor, Vector2Int[]>(4)
        {
            { LudoTokenColor.Red, s_RedRoute },
            { LudoTokenColor.Green, s_GreenRoute },
            { LudoTokenColor.Blue, s_BlueRoute },
            { LudoTokenColor.Yellow, s_YellowRoute },
        };

        private static readonly HashSet<Vector2Int> s_CommonPathSet = new HashSet<Vector2Int>(s_CommonPath);
        private static readonly HashSet<Vector2Int> s_HomeLaneSet = new HashSet<Vector2Int>(s_RedHomeLane);
        private static readonly HashSet<Vector2Int> s_SafeCells = new HashSet<Vector2Int>
        {
            new Vector2Int(1, 8),
            new Vector2Int(8, 13),
            new Vector2Int(13, 6),
            new Vector2Int(6, 1),
        };

        static LudoBoardGeometry()
        {
            AddHomeLane(s_GreenHomeLane);
            AddHomeLane(s_YellowHomeLane);
            AddHomeLane(s_BlueHomeLane);
        }

        public static IReadOnlyList<Vector2Int> CommonPath
        {
            get { return s_CommonPath; }
        }

        public static IReadOnlyList<LudoTokenColor> ClockwiseColors
        {
            get { return s_ClockwiseColors; }
        }

        public static Vector2Int GetHomeCoordinate(LudoTokenColor color)
        {
            switch (color)
            {
                case LudoTokenColor.Red:
                    return new Vector2Int(2, 12);
                case LudoTokenColor.Green:
                    return new Vector2Int(12, 12);
                case LudoTokenColor.Blue:
                    return new Vector2Int(2, 2);
                default:
                    return new Vector2Int(12, 2);
            }
        }

        public static Vector2Int GetStartCoordinate(LudoTokenColor color)
        {
            return GetRouteCoordinate(color, 0);
        }

        public static Vector2 GetHomeCircleCoordinate(LudoTokenColor tokenColor)
        {
            return GetHomeCircleCoordinate(tokenColor, 0);
        }

        public static Vector2 GetHomeCircleCoordinate(LudoTokenColor tokenColor, int tokenIndex)
        {
            int clampedIndex = Mathf.Clamp(tokenIndex, 0, TokensPerColor - 1);
            switch (tokenColor)
            {
                case LudoTokenColor.Red:
                    return s_RedHomeCircles[clampedIndex];
                case LudoTokenColor.Green:
                    return s_GreenHomeCircles[clampedIndex];
                case LudoTokenColor.Blue:
                    return s_BlueHomeCircles[clampedIndex];
                default:
                    return s_YellowHomeCircles[clampedIndex];
            }
        }

        public static Vector2Int GetNinthBoxCoordinate(LudoTokenColor tokenColor)
        {
            int startIndex = GetStartPathIndex(tokenColor);
            int targetIndex = (startIndex + 8) % CommonPathLength;
            return s_CommonPath[targetIndex];
        }

        public static Vector2Int GetRouteCoordinate(LudoTokenColor color, int progress)
        {
            if (progress < 0)
            {
                return GetHomeCoordinate(color);
            }

            if (progress > FinalProgress)
            {
                return GoalCoordinate;
            }

            return s_Routes[color][Mathf.Clamp(progress, 0, FinalProgress)];
        }

        public static bool IsCommonPath(Vector2Int coordinate)
        {
            return s_CommonPathSet.Contains(coordinate);
        }

        public static bool IsHomeLane(Vector2Int coordinate)
        {
            return s_HomeLaneSet.Contains(coordinate);
        }

        public static bool IsSafeCell(Vector2Int coordinate)
        {
            return s_SafeCells.Contains(coordinate);
        }

        public static string GetPlayerName(LudoTokenColor color)
        {
            switch (color)
            {
                case LudoTokenColor.Red:
                    return "Red";
                case LudoTokenColor.Green:
                    return "Green";
                case LudoTokenColor.Blue:
                    return "Blue";
                default:
                    return "Yellow";
            }
        }

        public static string GetDefaultPlayerLabel(LudoTokenColor color)
        {
            switch (color)
            {
                case LudoTokenColor.Red:
                    return "Player 2";
                case LudoTokenColor.Green:
                    return "Player 3";
                case LudoTokenColor.Blue:
                    return "Player 1";
                default:
                    return "Player 4";
            }
        }

        public static int GetStartPathIndex(LudoTokenColor tokenColor)
        {
            switch (tokenColor)
            {
                case LudoTokenColor.Red:
                    return 13;
                case LudoTokenColor.Green:
                    return 39;
                case LudoTokenColor.Blue:
                    return 0;
                default:
                    return 26;
            }
        }

        private static void AddHomeLane(IReadOnlyList<Vector2Int> homeLane)
        {
            for (int i = 0; i < homeLane.Count; i++)
            {
                s_HomeLaneSet.Add(homeLane[i]);
            }
        }

        private static Vector2Int[] BuildRoute(int startIndex, IReadOnlyList<Vector2Int> homeLane)
        {
            Vector2Int[] route = new Vector2Int[CommonPathLength + homeLane.Count];
            for (int i = 0; i < CommonPathLength; i++)
            {
                route[i] = s_CommonPath[(startIndex + i) % CommonPathLength];
            }

            for (int i = 0; i < homeLane.Count; i++)
            {
                route[CommonPathLength + i] = homeLane[i];
            }

            return route;
        }
    }
}

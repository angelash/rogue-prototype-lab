using System;
using System.Collections.Generic;

namespace MagnetShift
{
    public static class ShiftLevels
    {
        // Hand-routed benchmarks. The EditMode checks also solve every order with the live rules.
        static readonly int[] par = { 3, 5, 11, 6, 10 };
        public static int Par(int index) => par[index];

        // Uppercase pieces carry an order tag. Rows run from the rail down to the bin floor.
        public static IReadOnlyList<ShiftLevel> All { get; } = Array.AsReadOnly(new[]
        {
            new ShiftLevel("shaft", "深井取芯",
                "井底的重芯够不着。吸起长杆，再看磁线能伸到哪里。", 3, 2, 5, 8,
                ".......",
                ".#.r.#.",
                "..#.#..",
                "..#H#..",
                "...#..."),

            new ShiftLevel("overhang", "横梁底下",
                "横梁挡住了重芯。弯头装到磁头上，磁线就会转弯。", 1, 2, 5, 10,
                ".p..##.",
                "..e.H#.",
                ".#..##.",
                ".#.....",
                ".###..."),

            new ShiftLevel("gantry", "过检修梁",
                "左井的短片和右仓的重芯都要交。中间的隔板只容得下短钩。", 1, 2, 5, 13,
                ".......",
                ".r.#..H",
                "#.#.##.",
                "#P#....",
                ".#..##."),

            new ShiftLevel("scales", "地磅限载",
                "短片和重芯都要交。吊车限载 4，重芯一件就占 3。", 4, 2, 4, 11,
                ".......",
                "..r.P..",
                ".#..#.H",
                ".##..#.",
                "..#..##"),

            new ShiftLevel("blind-pocket", "盲孔封仓",
                "封仓前交出长杆和重芯。重芯压在盖板底下，开口朝下。", 1, 2, 7, 16,
                "....##..",
                ".R..H#..",
                ".#...e#.",
                "..#e..#.",
                "..#####.")
        });
    }
}

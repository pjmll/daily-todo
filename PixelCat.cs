using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DailyTodo
{
    internal sealed class PixelCat
    {
        private const int Width = 26;
        private const int Height = 20;
        private const int FrameMilliseconds = 160;

        private static readonly string[] Body =
        {
            "..........................",
            ".....###..........###.....",
            ".....#oo#........#oo#.....",
            ".....#opO#......#Opo#.....",
            ".....#oppO######Oppo#.....",
            "....#oOOOOCCCCCCOOOOo#....",
            "...#oOOCCCCCCCCCCCCOOo#...",
            "...#OOCCCCCCCCCCCCCCOO#...",
            "...#OCCCCCCCCCCCCCCCCO#...",
            "...#CCCCCCCCCCCCCCCCCC#...",
            "...#CCCCCCCCCCCCCCCCCC#...",
            "....#CCCCCCCCCCCCCCCC#....",
            "....#CCCCCCCCCCCCCCCC#....",
            ".....#CCCCCCCCCCCCCC#.....",
            ".....#CCCssssTssssCC#.....",
            ".....#CCCCsTTTTsCCCC#.....",
            "....#OOCCCCCCCCCCCCOO#....",
            "..######################..",
            "..#MMMMMMMMMMMMMMMMMMMM#..",
            "...####################..."
        };

        private static readonly string[] Paw = { ".###.", "#CCC#", "#Opp#", ".###." };
        private static readonly string[] PointPaw = { ".###.", "#OOO#", "#ooo#", ".###." };
        private static readonly Dictionary<char, Color> Palette = new Dictionary<char, Color>
        {
            { '.', Colors.Transparent },
            { '#', Color.FromRgb(65, 44, 35) },
            { 'o', Color.FromRgb(194, 115, 67) },
            { 'O', Color.FromRgb(245, 184, 112) },
            { 'C', Color.FromRgb(255, 240, 214) },
            { 'p', Color.FromRgb(235, 150, 141) },
            { 's', Color.FromRgb(44, 106, 88) },
            { 'T', Color.FromRgb(167, 221, 190) },
            { 'M', Color.FromRgb(211, 166, 117) },
            { 'g', Color.FromRgb(245, 199, 92) }
        };

        public static readonly IReadOnlyList<Skin> Skins = Array.AsReadOnly(new[]
        {
            new Skin("almond", "杏仁", 0xFFF0D6, 0xF5B870, 0xC27343, 0x2C6A58),
            new Skin("siamese", "暹罗猫", 0xFFF0D4, 0xA4745D, 0x6D493B, 0x4E7775,
                0x76C4F2, true, 7, 5,
                ".....OO.....",
                "....OOOO....",
                "..OOOOOOOO..",
                ".OOooooooOO.",
                "OOooooooooOO",
                "OOooooooooOO",
                ".OOooooooOO.",
                "..OOOOOOOO.."),
            new Skin("silver-shaded", "银渐层", 0xF4F6F2, 0xB1BBBD, 0x7B858B, 0x5A7E76,
                0x46AC79, false, 5, 5,
                "OOOOOO....OOOOOO",
                "OOO..........OOO",
                "OO............OO",
                "O..............O"),
            new Skin("orange", "橘猫", 0xFFD09B, 0xF18F4E, 0xB95F3D, 0x516691,
                0xD99443, false, 6, 5,
                "....o.o..o.o..",
                ".....o.oo.o...",
                "o.....o..o...o",
                "oo..........oo",
                "o............o"),
            new Skin("silver-point", "银点", 0xF6F7F2, 0xA9B1BB, 0x7E8998, 0x6886A8,
                0x76C5E7, true, 8, 6,
                "....OO....",
                "...OOOO...",
                "..OOOOOO..",
                ".OOOOOOOO.",
                ".OOooooOO.",
                "..OOOOOO..",
                "...OOOO..."),
            new Skin("lynx-siamese", "虎斑暹罗", 0xF5E7D4, 0xB39B87, 0x755D4F, 0x69895C,
                0x78C4F0, true, 7, 5,
                "....o..o....",
                "...OoOOoO...",
                "..OoOOOOoO..",
                ".OooOOOOooO.",
                "OoOOOOOOOOoO",
                ".ooOOooOOoo.",
                "..OOOooOOO..",
                "...OOOOOO..."),
            new Skin("mint", "薄荷", 0xE3F3E6, 0x86BCAB, 0x568F84, 0x417DB7,
                "..s..", ".sTs.", "..s.."),
            new Skin("sesame", "芝麻", 0xD8D4DC, 0x928D9D, 0x696375, 0xBE7651,
                ".ooo.", ".oOo."),
            new Skin("berry", "草莓", 0xFFE1DE, 0xEDA1A9, 0xC47887, 0x8D5F91,
                ".p.p.", ".ppp.", "..p.."),
            new Skin("mocha", "摩卡", 0xEBD0BA, 0xBF9379, 0x9B6D55, 0x5781A4,
                "OO...", "OOO..", ".O...")
        });

        internal sealed class Skin
        {
            public readonly string Id;
            public readonly string Name;
            public readonly BitmapSource Idle;
            public readonly BitmapSource Happy;
            public readonly BitmapSource Sleeping;
            public readonly BitmapSource[] TapFrames;
            public readonly BitmapSource[] ReverseTapFrames;
            public readonly BitmapSource[] CheerFrames;

            public Skin(string id, string name, int coat, int patch, int shade, int scarf, params string[] marking)
                : this(id, name, coat, patch, shade, scarf, 0x412C23, false, 10, 5, marking)
            {
            }

            public Skin(string id, string name, int coat, int patch, int shade, int scarf,
                int eye, bool pointed, int markingLeft, int markingTop, params string[] marking)
            {
                Id = id;
                Name = name;
                var palette = new Dictionary<char, Color>(Palette);
                palette['C'] = Rgb(coat);
                palette['O'] = Rgb(patch);
                palette['o'] = Rgb(shade);
                palette['s'] = Rgb(scarf);
                palette['e'] = Rgb(eye);
                var fur = new FurDetail(palette, markingLeft, markingTop, marking, pointed ? PointPaw : Paw);
                Idle = Draw(fur, 14, 14);
                var left = Draw(fur, 11, 15);
                var right = Draw(fur, 15, 11);
                Happy = Draw(fur, 11, 11, happy: true);
                Sleeping = Draw(fur, 15, 15, sleepy: true);
                TapFrames = new[] { left, right };
                ReverseTapFrames = new[] { right, left };
                CheerFrames = new[] { left, right, Happy, Happy };
            }
        }

        private readonly Image image;
        private readonly DispatcherTimer clock;
        private readonly Stopwatch playback = new Stopwatch();
        private BitmapSource[] frames;
        private Skin active = Skins[0];
        private bool reverseTap;

        public PixelCat(Image image)
        {
            this.image = image;
            image.Source = active.Idle;
            clock = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(FrameMilliseconds) };
            clock.Tick += delegate
            {
                int frameIndex = (int)(playback.ElapsedMilliseconds / FrameMilliseconds);
                if (frameIndex >= frames.Length) Stop();
                else image.Source = frames[frameIndex];
            };
        }

        public void Play(bool cheer)
        {
            if (!image.IsVisible || !SystemParameters.ClientAreaAnimation) return;
            clock.Stop();
            frames = cheer ? active.CheerFrames : reverseTap ? active.ReverseTapFrames : active.TapFrames;
            reverseTap = !reverseTap;
            image.Source = frames[0];
            playback.Restart();
            clock.Start();
        }

        public void ShowSkin(Skin skin)
        {
            if (active == skin) return;
            active = skin;
            Stop();
        }

        public void Stop()
        {
            clock.Stop();
            playback.Stop();
            image.Source = active.Idle;
        }

        private static Color Rgb(int color)
        {
            return Color.FromRgb((byte)(color >> 16), (byte)(color >> 8), (byte)color);
        }

        private sealed class FurDetail
        {
            public readonly Dictionary<char, Color> Palette;
            public readonly int Left;
            public readonly int Top;
            public readonly string[] Marking;
            public readonly string[] Paws;

            public FurDetail(Dictionary<char, Color> palette, int left, int top, string[] marking, string[] paws)
            {
                Palette = palette;
                Left = left;
                Top = top;
                Marking = marking;
                Paws = paws;
            }
        }

        private static BitmapSource Draw(FurDetail fur, int leftPaw, int rightPaw, bool happy = false, bool sleepy = false)
        {
            var pixels = Body.Select(row => row.ToCharArray()).ToArray();
            Stamp(pixels, fur.Left, fur.Top, fur.Marking);
            Stamp(pixels, 5, 10, "pp");
            Stamp(pixels, 19, 10, "pp");
            Stamp(pixels, 11, 10, ".oo.", "#..#", ".##.");
            if (happy)
            {
                Stamp(pixels, 7, 8, ".##.", "#..#");
                Stamp(pixels, 15, 8, ".##.", "#..#");
                Stamp(pixels, 1, 6, ".g.", "ggg", ".g.");
                Stamp(pixels, 22, 2, ".g.", "ggg", ".g.");
            }
            else if (sleepy)
            {
                Stamp(pixels, 8, 9, "##");
                Stamp(pixels, 16, 9, "##");
                Stamp(pixels, 22, 0, "sss", "..s", ".s.", "sss");
            }
            else
            {
                Stamp(pixels, 8, 8, "##", "#e");
                Stamp(pixels, 16, 8, "##", "e#");
            }

            Stamp(pixels, 6, leftPaw, fur.Paws);
            Stamp(pixels, 15, rightPaw, fur.Paws);
            var bytes = new byte[Width * Height * 4];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var color = fur.Palette[pixels[y][x]];
                    int offset = (y * Width + x) * 4;
                    bytes[offset] = color.B;
                    bytes[offset + 1] = color.G;
                    bytes[offset + 2] = color.R;
                    bytes[offset + 3] = color.A;
                }
            }

            var image = BitmapSource.Create(Width, Height, 96, 96, PixelFormats.Bgra32, null, bytes, Width * 4);
            image.Freeze();
            return image;
        }

        private static void Stamp(char[][] canvas, int left, int top, params string[] art)
        {
            for (int y = 0; y < art.Length; y++)
                for (int x = 0; x < art[y].Length; x++)
                    if (art[y][x] != '.') canvas[top + y][left + x] = art[y][x];
        }
    }
}

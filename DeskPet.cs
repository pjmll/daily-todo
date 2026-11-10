using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DailyTodo
{
    internal sealed class DeskPet
    {
        internal enum Animal { Cat, Rabbit, Deer }
        internal enum Marking { Plain, Point, Stripes, Silver }
        private enum Pose { Rest, Blink, Wave, Happy, Sleep }

        private static readonly Brush Outline = Paint(0x675344);
        private static readonly Brush Blush = Paint(0xEAA997);
        private static readonly Brush EarPink = Paint(0xEAB6A4);
        private static readonly Brush Shadow = Paint(0xD5C9A9);
        private static readonly Brush Star = Paint(0xE1A743);
        private static readonly Pen Edge = new Pen(Outline, 1.7) { LineJoin = PenLineJoin.Round };
        private static readonly Pen FaceLine = new Pen(Outline, 1.4)
        {
            StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round
        };

        public static readonly IReadOnlyList<Skin> Skins = Array.AsReadOnly(new[]
        {
            new Skin("almond", "杏仁", Animal.Cat, 0xEDC591, 0xFFF2D8, 0xC98F64, 0x614C3E, 0x96B879),
            new Skin("siamese", "暹罗猫", Animal.Cat, 0xF4DEB7, 0xFFF2D8, 0xAC7860, 0x477F9D, 0x90B6A2, Marking.Point),
            new Skin("silver-shaded", "银渐层", Animal.Cat, 0xDCE4DF, 0xF6F8EE, 0xA9BCB6, 0x4A8065, 0xC0A2CE, Marking.Silver),
            new Skin("orange", "橘猫", Animal.Cat, 0xF4B45F, 0xFFF0C9, 0xC87A44, 0x715440, 0x8EB486, Marking.Stripes),
            new Skin("silver-point", "银点", Animal.Cat, 0xF3F4E9, 0xFFF9EE, 0xB3BAC8, 0x5085A6, 0x9CBBC9, Marking.Point),
            new Skin("lynx-siamese", "虎斑暹罗", Animal.Cat, 0xE5CAA7, 0xF9E7CD, 0xA98068, 0x4C86A9, 0xC5AD6C, Marking.Stripes),
            new Skin("mint", "薄荷", Animal.Cat, 0xBBD9C1, 0xECF5DF, 0x82AB99, 0x507C6C, 0xE0AB72),
            new Skin("sesame", "芝麻", Animal.Cat, 0x9E929E, 0xDCCED4, 0x786A79, 0x554457, 0xE1AD6F, Marking.Silver),
            new Skin("berry", "草莓", Animal.Cat, 0xF2BEC5, 0xFFE6DE, 0xC8899A, 0x87536B, 0x91B28A),
            new Skin("mocha", "摩卡", Animal.Cat, 0xD8AE90, 0xF4D8BA, 0xA27860, 0x684E41, 0x93B7C4, Marking.Point),
            new Skin("bunny", "团子兔", Animal.Rabbit, 0xF1D9BB, 0xFFF4DC, 0xD6AA8B, 0x6D4E40, 0xD99078),
            new Skin("fawn", "栗栗鹿", Animal.Deer, 0xC7936D, 0xF9E1B5, 0x98674D, 0x634B3E, 0x87AFA7)
        });

        public static IReadOnlyList<Skin> LoadCatalog(string dataDirectory)
        {
            var nuko = NukoPack.Load(Path.Combine(dataDirectory, "local-nuko"));
            if (nuko != null) return nuko;

            var catalog = new List<Skin> { Skins[0] };
            string folder = Path.Combine(dataDirectory, "local-portraits");
            AddPortrait(catalog, folder, "cream-pup", "奶油小狗");
            AddPortrait(catalog, folder, "blue-cat", "蓝纹猫");
            AddPortrait(catalog, folder, "mint-rabbit", "薄荷兔");
            AddPortrait(catalog, folder, "cream-squirrel", "奶黄团子");
            for (int index = 1; index < Skins.Count; index++) catalog.Add(Skins[index]);
            return catalog.AsReadOnly();
        }

        private static void AddPortrait(List<Skin> catalog, string folder, string id, string title)
        {
            string path = Path.Combine(folder, id + ".jpg");
            if (!File.Exists(path)) return;
            try
            {
                using (var input = File.OpenRead(path))
                {
                    var decoder = new JpegBitmapDecoder(input, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    var portrait = decoder.Frames[0];
                    portrait.Freeze();
                    catalog.Add(new Skin("local-" + id, title, portrait));
                }
            }
            catch (Exception error)
            {
                if (!TaskStore.IsStorageError(error) && !(error is NotSupportedException)) throw;
            }
        }

        internal sealed class Skin
        {
            public readonly string Id;
            public readonly string Name;
            public readonly ImageSource Idle;
            public readonly ImageSource Happy;
            public readonly ImageSource Sleeping;
            public readonly Motion Wave;
            public readonly Motion Blink;
            public readonly Motion Cheer;
            public readonly bool PixelArt;

            public Skin(string id, string name, Animal animal, int coat, int face, int detail, int eyes, int scarf,
                Marking marking = Marking.Plain)
            {
                Id = id;
                Name = name;
                var colors = new Colors(coat, face, detail, eyes, scarf);
                Idle = Draw(animal, marking, colors, Pose.Rest);
                Happy = Draw(animal, marking, colors, Pose.Happy);
                Sleeping = Draw(animal, marking, colors, Pose.Sleep);
                var wave = Draw(animal, marking, colors, Pose.Wave);
                var blink = Draw(animal, marking, colors, Pose.Blink);
                Wave = Motion.Fixed(wave, Idle, wave);
                Blink = Motion.Fixed(blink, wave, Idle);
                Cheer = Motion.Fixed(wave, Happy, Happy, wave, Happy);
            }

            public Skin(string id, string name, BitmapSource portrait)
            {
                Id = id;
                Name = name;
                Idle = DrawPortrait(portrait, Pose.Rest);
                Happy = DrawPortrait(portrait, Pose.Happy);
                Sleeping = DrawPortrait(portrait, Pose.Sleep);
                var wave = DrawPortrait(portrait, Pose.Wave);
                var sway = DrawPortrait(portrait, Pose.Blink);
                Wave = Motion.Fixed(wave, Idle, wave);
                Blink = Motion.Fixed(sway, wave, Idle);
                Cheer = Motion.Fixed(wave, Happy, Happy, wave, Happy);
            }

            public Skin(string id, string name, Motion action, Motion cheer, ImageSource sleep)
            {
                Id = id;
                Name = name;
                Idle = action.First;
                Happy = cheer.First;
                Sleeping = sleep;
                Wave = action;
                Blink = action;
                Cheer = cheer;
                PixelArt = true;
            }
        }

        internal sealed class Motion
        {
            private readonly ImageSource[] frames;
            private readonly int[] ends;

            public ImageSource First { get { return frames[0]; } }
            public int Duration { get { return ends[ends.Length - 1]; } }

            public Motion(ImageSource[] frames, int[] durations)
            {
                this.frames = frames;
                ends = new int[frames.Length];
                int total = 0;
                for (int index = 0; index < ends.Length; index++)
                {
                    total += durations[index];
                    ends[index] = total;
                }
            }

            public static Motion Fixed(params ImageSource[] frames)
            {
                var delays = new int[frames.Length];
                for (int index = 0; index < delays.Length; index++) delays[index] = 155;
                return new Motion(frames, delays);
            }

            public ImageSource At(long milliseconds)
            {
                if (milliseconds >= Duration) return null;
                int index = Array.BinarySearch(ends, (int)milliseconds + 1);
                return frames[index < 0 ? ~index : index];
            }
        }

        private sealed class Colors
        {
            public readonly Brush Coat;
            public readonly Brush Face;
            public readonly Brush Detail;
            public readonly Brush Eyes;
            public readonly Brush Scarf;

            public Colors(int coat, int face, int detail, int eyes, int scarf)
            {
                Coat = Paint(coat);
                Face = Paint(face);
                Detail = Paint(detail);
                Eyes = Paint(eyes);
                Scarf = Paint(scarf);
            }
        }

        private readonly Image image;
        private readonly DispatcherTimer clock;
        private readonly Stopwatch playback = new Stopwatch();
        private Motion motion;
        private Skin active = Skins[0];
        private bool blinkNext;

        public DeskPet(Image image)
        {
            this.image = image;
            image.Source = active.Idle;
            clock = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
            clock.Tick += delegate
            {
                var frame = motion.At(playback.ElapsedMilliseconds);
                if (frame == null) Stop();
                else if (frame != image.Source) image.Source = frame;
            };
        }

        public void Play(bool cheer)
        {
            if (!image.IsVisible || !SystemParameters.ClientAreaAnimation) return;
            clock.Stop();
            motion = cheer ? active.Cheer : blinkNext ? active.Blink : active.Wave;
            if (!cheer) blinkNext = !blinkNext;
            image.Source = motion.First;
            playback.Restart();
            clock.Start();
        }

        public void ShowSkin(Skin skin)
        {
            if (active == skin) return;
            active = skin;
            blinkNext = false;
            SetScaling(image, skin);
            Stop();
        }

        public static void SetScaling(Image view, Skin skin)
        {
            RenderOptions.SetBitmapScalingMode(view, skin.PixelArt ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.Unspecified);
        }

        public void Stop()
        {
            clock.Stop();
            playback.Stop();
            image.Source = active.Idle;
        }

        private static Brush Paint(int color)
        {
            var brush = new SolidColorBrush(Color.FromRgb((byte)(color >> 16), (byte)(color >> 8), (byte)color));
            brush.Freeze();
            return brush;
        }

        private static ImageSource DrawPortrait(BitmapSource portrait, Pose pose)
        {
            var drawing = new DrawingGroup();
            using (var dc = drawing.Open())
            {
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 76, 61));
                Oval(dc, Shadow, null, 38, 57, 22, 2.4);

                double top = pose == Pose.Wave || pose == Pose.Happy ? 0 : pose == Pose.Sleep ? 5 : 2;
                double angle = pose == Pose.Wave ? -7 : pose == Pose.Blink || pose == Pose.Sleep ? 7 : 0;
                var frame = new Rect(11, top, 54, 54);
                dc.PushTransform(new RotateTransform(angle, 38, top + 27));
                dc.PushClip(new RectangleGeometry(frame, 13, 13));
                dc.DrawRectangle(Brushes.White, null, frame);
                double scale = Math.Min(frame.Width / portrait.PixelWidth, frame.Height / portrait.PixelHeight);
                double width = portrait.PixelWidth * scale;
                double height = portrait.PixelHeight * scale;
                dc.DrawImage(portrait, new Rect(frame.X + (frame.Width - width) / 2,
                    frame.Y + (frame.Height - height) / 2, width, height));
                dc.Pop();
                dc.Pop();

                if (pose == Pose.Happy)
                    Shape(dc, Star, null, "M65,9 L67,13 L71,15 L67,17 L65,21 L63,17 L59,15 L63,13 Z");
                if (pose == Pose.Sleep)
                    Shape(dc, null, FaceLine, "M61,12 L66,12 L61,17 L66,17 M67,5 L71,5 L67,9 L71,9");
            }
            var result = new DrawingImage(drawing);
            result.Freeze();
            return result;
        }

        private static ImageSource Draw(Animal animal, Marking marking, Colors colors, Pose pose)
        {
            var drawing = new DrawingGroup();
            using (var dc = drawing.Open())
            {
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 76, 61));
                Oval(dc, Shadow, null, 38, 55, 24, 3);

                if (animal == Animal.Cat)
                    Shape(dc, colors.Coat, Edge, "M49,45 C54,47 59,45 61,40 C64,32 71,34 70,40 C69,50 60,54 50,51 Z");
                else if (animal == Animal.Rabbit)
                    Oval(dc, colors.Face, Edge, 56, 44, 6, 5);

                Oval(dc, colors.Coat, Edge, 38, 43, 18, 12);
                Oval(dc, colors.Face, null, 38, 47, 10, 6);
                Oval(dc, colors.Coat, Edge, 28, 52, 6.3, 3.8);
                Oval(dc, colors.Coat, Edge, 48, 52, 6.3, 3.8);
                Shape(dc, colors.Scarf, Edge, "M26,39 Q38,45 50,39 L48,45 Q38,50 27,45 Z");

                if (pose == Pose.Sleep) dc.PushTransform(new TranslateTransform(0, 3));
                DrawEars(dc, animal, colors, marking);
                Shape(dc, colors.Coat, Edge, animal == Animal.Cat
                    ? "M14,29 C14,18 23,13 38,13 C53,13 62,18 62,29 L65,33 L61,35 L62,38 L58,39 C53,45 23,45 18,39 L14,38 L15,35 L11,33 Z"
                    : "M14,29 C14,18 24,13 38,13 C52,13 62,18 62,29 C62,39 53,44 38,44 C23,44 14,39 14,29 Z");
                Oval(dc, colors.Face, null, 38, 34, 19, 9);
                DrawMarkings(dc, animal, marking, colors);
                DrawFace(dc, animal, colors, pose);
                if (pose == Pose.Sleep) dc.Pop();

                if (pose == Pose.Wave || pose == Pose.Happy)
                {
                    Oval(dc, colors.Coat, Edge, 57, 41, 5.5, 6);
                    Oval(dc, Blush, null, 57, 40, 2.6, 2.2);
                    Shape(dc, Star, null, "M65,15 L67,19 L71,21 L67,23 L65,27 L63,23 L59,21 L63,19 Z");
                }
                if (pose == Pose.Sleep)
                    Shape(dc, colors.Scarf, FaceLine, "M59,12 L65,12 L59,18 L65,18 M66,6 L70,6 L66,10 L70,10");
            }
            var result = new DrawingImage(drawing);
            result.Freeze();
            return result;
        }

        private static void DrawEars(DrawingContext dc, Animal animal, Colors colors, Marking marking)
        {
            if (animal == Animal.Cat)
            {
                Brush tips = marking == Marking.Point ? colors.Detail : colors.Coat;
                Shape(dc, tips, Edge, "M18,22 Q13,11 18,5 Q28,6 33,16 Z M43,16 Q48,6 58,5 Q63,11 58,22 Z");
                Shape(dc, EarPink, null, "M21,17 Q17,11 20,9 Q25,10 28,15 Z M48,15 Q51,10 56,9 Q59,11 55,17 Z");
            }
            else if (animal == Animal.Rabbit)
            {
                Shape(dc, colors.Coat, Edge, "M25,20 C22,7 15,2 11,9 C7,15 10,30 15,32 C20,33 22,26 25,20 Z M51,20 C54,7 61,2 65,9 C69,15 66,30 61,32 C56,33 54,26 51,20 Z");
                Shape(dc, EarPink, null, "M15,12 C12,14 13,25 16,27 C19,27 20,20 18,15 Z M61,12 C64,14 63,25 60,27 C57,27 56,20 58,15 Z");
            }
            else
            {
                Shape(dc, colors.Detail, Edge, "M27,17 L24,8 L21,6 L24,5 L28,10 L30,7 L32,8 L30,16 Z M49,17 L52,8 L55,6 L52,5 L48,10 L46,7 L44,8 L46,16 Z");
                Shape(dc, colors.Coat, Edge, "M24,21 Q12,8 7,15 Q8,25 20,26 Z M52,21 Q64,8 69,15 Q68,25 56,26 Z");
                Shape(dc, EarPink, null, "M19,22 Q12,16 11,17 Q12,22 19,22 Z M57,22 Q64,16 65,17 Q64,22 57,22 Z");
            }
        }

        private static void DrawMarkings(DrawingContext dc, Animal animal, Marking marking, Colors colors)
        {
            if (marking == Marking.Point)
                Shape(dc, colors.Detail, null, "M25,26 Q25,22 30,22 Q35,22 38,27 Q41,22 46,22 Q51,22 51,26 L50,33 Q45,39 38,37 Q31,39 26,33 Z");
            else if (marking == Marking.Stripes)
                Shape(dc, colors.Detail, null, "M29,15 Q32,15 34,16 L35,22 Q33,22 29,15 Z M36,14 L40,14 L38,22 Z M42,16 Q44,15 47,15 Q43,22 41,22 Z M16,28 L22,30 L16,31 Z M60,28 L54,30 L60,31 Z");
            else if (marking == Marking.Silver)
                Shape(dc, colors.Detail, null, "M21,20 Q37,9 55,20 Q50,19 45,21 Q38,17 31,21 Q26,18 21,20 Z");
            if (animal == Animal.Deer)
            {
                Oval(dc, colors.Face, null, 33, 20, 2.2, 2.2);
                Oval(dc, colors.Face, null, 39, 18, 1.9, 1.9);
                Oval(dc, colors.Face, null, 45, 21, 2, 2);
            }
        }

        private static void DrawFace(DrawingContext dc, Animal animal, Colors colors, Pose pose)
        {
            Oval(dc, Blush, null, 23, 33, 4.3, 2.1);
            Oval(dc, Blush, null, 53, 33, 4.3, 2.1);
            if (pose == Pose.Blink || pose == Pose.Sleep)
                Shape(dc, null, FaceLine, "M27,28 Q30,32 33,28 M43,28 Q46,32 49,28");
            else if (pose == Pose.Happy)
                Shape(dc, null, FaceLine, "M27,30 Q30,25 33,30 M43,30 Q46,25 49,30");
            else
            {
                Oval(dc, colors.Eyes, null, 30, 28.5, 2.75, 3.8);
                Oval(dc, colors.Eyes, null, 46, 28.5, 2.75, 3.8);
                Oval(dc, Brushes.White, null, 29.3, 27.5, 0.8, 0.9);
                Oval(dc, Brushes.White, null, 45.3, 27.5, 0.8, 0.9);
            }
            Oval(dc, animal == Animal.Deer ? Outline : EarPink, FaceLine, 38, 33, 2.4, 1.3);
            if (pose == Pose.Happy)
                Shape(dc, Blush, FaceLine, "M34,36 Q38,38 42,36 Q41,41 38,41 Q35,41 34,36 Z");
            else
                Shape(dc, null, FaceLine, "M34,35.5 Q36,38 38,35.5 Q40,38 42,35.5");
        }

        private static void Oval(DrawingContext dc, Brush brush, Pen pen, double x, double y, double width, double height)
        {
            dc.DrawEllipse(brush, pen, new Point(x, y), width, height);
        }

        private static void Shape(DrawingContext dc, Brush brush, Pen pen, string path)
        {
            dc.DrawGeometry(brush, pen, Geometry.Parse(path));
        }
    }
}

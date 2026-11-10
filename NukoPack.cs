using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;
using DrawingImages = System.Drawing.Imaging;

namespace DailyTodo
{
    internal static class NukoPack
    {
        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            { "almond", "Nuko 经典" },
            { "siamese", "Nuko 咖啡" },
            { "silver-shaded", "Nuko 读书" },
            { "orange", "Nuko 发芽" },
            { "silver-point", "Nuko 爱心" },
            { "lynx-siamese", "Nuko 听歌" },
            { "mint", "Nuko 喝茶" },
            { "sesame", "Nuko 星星" },
            { "berry", "Nuko 苹果" },
            { "mocha", "Nuko 打鼓" },
            { "local-blue-cat", "Nuko 挥手" }
        };

        public static IReadOnlyList<DeskPet.Skin> Load(string folder)
        {
            if (!Directory.Exists(folder)) return null;
            var cheer = Read(Path.Combine(folder, "cheer.gif"));
            var starter = Read(Path.Combine(folder, "almond.gif")) ?? cheer;
            if (starter == null) return null;
            var sleep = Read(Path.Combine(folder, "sleep.gif"));
            var catalog = new List<DeskPet.Skin>
            {
                new DeskPet.Skin("almond", Names["almond"], starter, cheer ?? starter,
                    sleep == null ? starter.First : sleep.First)
            };
            foreach (var variant in Names)
            {
                if (variant.Key == "almond") continue;
                var animation = Read(Path.Combine(folder, variant.Key + ".gif"));
                if (animation != null)
                    catalog.Add(new DeskPet.Skin(variant.Key, variant.Value, animation, cheer ?? animation,
                        sleep == null ? animation.First : sleep.First));
            }
            return catalog.AsReadOnly();
        }

        private static DeskPet.Motion Read(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                using (var stream = File.OpenRead(path))
                using (var gif = Drawing.Image.FromStream(stream, false, true))
                {
                    if (gif.RawFormat.Guid != DrawingImages.ImageFormat.Gif.Guid || gif.Width > 160 || gif.Height > 160)
                        return null;
                    int count = gif.GetFrameCount(DrawingImages.FrameDimension.Time);
                    if (count < 2 || count > 120) return null;

                    var delays = new int[count];
                    const int frameDelay = 0x5100;
                    var properties = gif.PropertyIdList;
                    var timing = Array.IndexOf(properties, frameDelay) >= 0 ? gif.GetPropertyItem(frameDelay).Value : null;
                    var images = new ImageSource[count];
                    using (var frame = new Drawing.Bitmap(gif.Width, gif.Height, DrawingImages.PixelFormat.Format32bppArgb))
                    using (var graphics = Drawing.Graphics.FromImage(frame))
                    {
                        graphics.CompositingMode = Drawing.Drawing2D.CompositingMode.SourceCopy;
                        for (int index = 0; index < count; index++)
                        {
                            int offset = index * 4;
                            int units = timing != null && timing.Length >= offset + 4 ? BitConverter.ToInt32(timing, offset) : 0;
                            delays[index] = units > 0 ? Math.Min(units, 1000) * 10 : 100;
                            gif.SelectActiveFrame(DrawingImages.FrameDimension.Time, index);
                            graphics.DrawImageUnscaled(gif, 0, 0);
                            using (var png = new MemoryStream())
                            {
                                frame.Save(png, DrawingImages.ImageFormat.Png);
                                png.Position = 0;
                                var pixels = BitmapFrame.Create(png, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                                pixels.Freeze();
                                images[index] = pixels;
                            }
                        }
                    }
                    return new DeskPet.Motion(images, delays);
                }
            }
            catch (Exception error)
            {
                if (!TaskStore.IsStorageError(error) && !(error is ArgumentException) &&
                    !(error is System.Runtime.InteropServices.ExternalException)) throw;
                return null;
            }
        }
    }
}

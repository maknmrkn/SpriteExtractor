using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;

namespace SpriteExtractor.Services
{
    public static class ThumbnailService
    {
        public static Task<Image> GenerateThumbnailAsync(Bitmap sourceBitmap, Rectangle bounds)
        {
            return Task.Run<Image>(() =>
            {
                try
                {
                    const int size = 48;
                    var thumbnail = new Bitmap(size, size, PixelFormat.Format32bppArgb);

                    using (var g = Graphics.FromImage(thumbnail))
                    {
                        // simple checkerboard background
                        int cell = 6;
                        using var dark = new SolidBrush(Color.FromArgb(100, 100, 100));
                        using var light = new SolidBrush(Color.FromArgb(150, 150, 150));
                        for (int y = 0; y < size; y += cell)
                        {
                            for (int x = 0; x < size; x += cell)
                            {
                                var useDark = ((x / cell) + (y / cell)) % 2 == 0;
                                g.FillRectangle(useDark ? dark : light, x, y, cell, cell);
                            }
                        }

                        if (sourceBitmap != null && bounds.Width > 0 && bounds.Height > 0)
                        {
                            float scaleX = (size - 2f) / bounds.Width;
                            float scaleY = (size - 2f) / bounds.Height;
                            float scale = Math.Min(scaleX, scaleY);

                            int destWidth = Math.Max(1, (int)(bounds.Width * scale));
                            int destHeight = Math.Max(1, (int)(bounds.Height * scale));
                            int destX = (size - destWidth) / 2;
                            int destY = (size - destHeight) / 2;

                            g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

                            g.DrawImage(sourceBitmap,
                                new Rectangle(destX + 1, destY + 1, Math.Max(1, destWidth - 2), Math.Max(1, destHeight - 2)),
                                bounds,
                                GraphicsUnit.Pixel);
                        }

                        using var pen = new Pen(Color.White, 1);
                        g.DrawRectangle(pen, 0, 0, size - 1, size - 1);
                    }

                    return (Image)thumbnail;
                }
                catch
                {
                    return new Bitmap(48, 48);
                }
            });
        }
    }
}

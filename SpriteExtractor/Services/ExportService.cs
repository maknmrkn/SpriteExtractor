using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using SpriteExtractor.Models;

namespace SpriteExtractor.Services
{
    public static class ExportService
    {
        public static void ExportSprites(SpriteProject project, string outputDir)
        {
            try
            {
                if (!Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);
                
                if (string.IsNullOrEmpty(project.SourceImagePath) || !File.Exists(project.SourceImagePath))
                    throw new FileNotFoundException($"Source image not found: {project.SourceImagePath}");
                
                using var sourceImage = Image.FromFile(project.SourceImagePath);
                
                foreach (var sprite in project.Sprites)
                {
                    // بررسی محدوده معتبر
                    if (sprite.Bounds.Width <= 0 || sprite.Bounds.Height <= 0)
                        continue;
                    
                    if (sprite.Bounds.X < 0 || sprite.Bounds.Y < 0)
                        continue;
                    
                    if (sprite.Bounds.Right > sourceImage.Width || sprite.Bounds.Bottom > sourceImage.Height)
                        continue;
                    
                    var bitmap = new Bitmap(sprite.Bounds.Width, sprite.Bounds.Height);
                    
                    using (var g = Graphics.FromImage(bitmap))
                    {
                        g.DrawImage(sourceImage,
                            new Rectangle(0, 0, sprite.Bounds.Width, sprite.Bounds.Height),
                            sprite.Bounds,
                            GraphicsUnit.Pixel);
                    }
                    
                    var outputPath = Path.Combine(outputDir, $"{sprite.Name}.png");
                    bitmap.Save(outputPath, ImageFormat.Png);
                    bitmap.Dispose();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to export sprites: {ex.Message}", ex);
            }
        }
    }
}
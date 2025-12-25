using System;
using System.Drawing;

namespace SpriteExtractor.Models
{
    public class ProjectSettings
    {
        public string OutputFormat { get; set; } = "PNG";
        public string OutputDirectory { get; set; } = "./Output/";
        public bool AutoDetectEnabled { get; set; } = false;
        
        // 🔧 این Property جدید برای رنگ هایلایت - حتماً باید وجود داشته باشد
        public Color HighlightColor { get; set; } = Color.Orange;
    }
}
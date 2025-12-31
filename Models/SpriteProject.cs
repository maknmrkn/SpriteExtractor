using System.Collections.Generic;
using System.Drawing;
using Newtonsoft.Json;

namespace SpriteExtractor.Models
{
    public class SpriteProject
    {
        public int SchemaVersion { get; set; } = 1;
        public string Name { get; set; } = "New Project";

        // Backward compatibility: some code may expect ProjectName
        [JsonIgnore]
        public string ProjectName
        {
            get => Name;
            set => Name = value;
        }

        public string SourceImagePath { get; set; }

        // Lightweight inline settings so we don't depend on an external ProjectSettings file
        public SimpleProjectSettings Settings { get; set; } = new SimpleProjectSettings();

        public List<SpriteDefinition> Sprites { get; set; } = new List<SpriteDefinition>();
    }

    // Minimal settings DTO embedded here to avoid external dependency
    public class SimpleProjectSettings
    {
        public string OutputDirectory { get; set; } = "./Output/";
        public string OutputFormat { get; set; } = "png";
        public bool AutoDetectEnabled { get; set; } = true;

        // ذخیره رنگ به صورت ARGB int برای سریالایز شدن
        public int HighlightColorArgb { get; set; } = Color.Yellow.ToArgb();

        // این property برای کدهایی که انتظار HighlightColor از نوع Color را دارند
        [JsonIgnore]
        public Color HighlightColor
        {
            get => Color.FromArgb(HighlightColorArgb);
            set => HighlightColorArgb = value.ToArgb();
        }
    }
}
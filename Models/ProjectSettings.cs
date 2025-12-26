using System;
using System.Drawing;
using System.IO;
using Newtonsoft.Json;

namespace SpriteExtractor.Models
{
    public class ProjectSettings
    {
        public string OutputFormat { get; set; } = "PNG";

        private string _outputDirectory = "./Output/";
        public string OutputDirectory
        {
            get => _outputDirectory;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    _outputDirectory = Path.GetFullPath("./Output/");
                }
                else
                {
                    _outputDirectory = Path.GetFullPath(value);
                }

                try
                {
                    Directory.CreateDirectory(_outputDirectory);
                }
                catch
                {
                    // ایجاد دایرکتوری شکست خورد — در این لایه خطا را نادیده می‌گیریم.
                }
            }
        }

        public bool AutoDetectEnabled { get; set; } = false;

        [JsonIgnore]
        public Color HighlightColor { get; set; } = Color.Orange;

        [JsonProperty("HighlightColor")]
        public int HighlightColorArgb
        {
            get => HighlightColor.ToArgb();
            set => HighlightColor = Color.FromArgb(value);
        }
    }
}
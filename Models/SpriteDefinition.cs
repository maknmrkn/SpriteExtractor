using System.Drawing;
using Newtonsoft.Json;

namespace SpriteExtractor.Models
{
    public class SpriteDefinition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Sprite";
        public Rectangle Bounds { get; set; }
        public Point Pivot { get; set; }
        public bool IsVisible { get; set; } = true;

        [JsonIgnore]
        public Image Thumbnail { get; set; }

        public string ThumbnailKey { get; set; }
    }
}
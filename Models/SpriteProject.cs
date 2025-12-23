using System;
using System.Collections.Generic;
using System.Drawing;

namespace SpriteExtractor.Models
{
    public class SpriteProject
    {
        public string ProjectName { get; set; } = "New Project";
        public string SourceImagePath { get; set; } = "";
        public List<SpriteDefinition> Sprites { get; set; } = new();
        public ProjectSettings Settings { get; set; } = new();
    }

    public class SpriteDefinition
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "Sprite";
        public Rectangle Bounds { get; set; }
        public Point Pivot { get; set; } = new Point(0, 0);
        public bool IsVisible { get; set; } = true;
        
        public override string ToString() => $"{Name} ({Bounds.X}, {Bounds.Y}, {Bounds.Width}, {Bounds.Height})";
    }


}
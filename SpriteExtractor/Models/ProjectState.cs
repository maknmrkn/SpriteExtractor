using System;
using System.Collections.Generic;

namespace SpriteExtractor.Models
{
    public class ProjectState
    {
        public List<SpriteDefinition> Sprites { get; set; } = new();
        public string ImagePath { get; set; } = string.Empty;
        
        public ProjectState Clone()
        {
            return new ProjectState
            {
                ImagePath = this.ImagePath,
                Sprites = this.Sprites.ConvertAll(s => new SpriteDefinition
                {
                    Name = s.Name,
                    Bounds = s.Bounds,
                    Pivot = s.Pivot,
                    IsVisible = s.IsVisible
                })
            };
        }
    }
}
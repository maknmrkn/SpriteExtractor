using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using SpriteExtractor.Models;

namespace SpriteExtractor.Views
{
    public interface IMainView
    {
        Panel ImagePanel { get; }
        ListView SpriteListView { get; }
        PropertyGrid PropertyGrid { get; }
        StatusStrip StatusBar { get; }
        SpriteImageList SpriteThumbnails { get; }
        SpriteImageList SpriteImageList { get; }

        void UpdateSpriteList(List<SpriteDefinition> sprites);
        void UpdateStatus(string message);
        void ScrollToSprite(Rectangle spriteBounds);
        // UI helper methods to decouple presenters from concrete view implementation
        void InvalidateImagePanel();
        void BeginInvokeAction(Action action);

        // Thumbnail helpers
        void UpdateSpriteThumbnail(string key, Image thumbnail);
        void RemoveSpriteThumbnail(string key);
        void ClearSpriteThumbnails();

        // ListView batch helpers
        void BeginUpdateSpriteList();
        void EndUpdateSpriteList();
        void EnsureSpriteImageListAssigned();
    }
}

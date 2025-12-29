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

        /// <summary>
/// Updates the view's sprite list to reflect the provided sprite definitions.
/// </summary>
/// <param name="sprites">The sprite definitions to display; the view will replace its current list with these items.</param>
void UpdateSpriteList(List<SpriteDefinition> sprites);
        /// <summary>
/// Updates the status bar text to display the provided message.
/// </summary>
/// <param name="message">The message to show in the status area.</param>
void UpdateStatus(string message);
        /// <summary>
/// Scrolls the image panel so the area defined by spriteBounds is visible.
/// </summary>
/// <param name="spriteBounds">The sprite's bounding rectangle in image panel coordinates to bring into view.</param>
void ScrollToSprite(Rectangle spriteBounds);
        /// <summary>
/// Requests a repaint of the ImagePanel control.
/// </summary>
        void InvalidateImagePanel();
        /// <summary>
/// Schedules the provided action to run on the UI thread (or defers execution until it can be run on the UI thread).
/// </summary>
/// <param name="action">The delegate to execute on the UI thread.</param>
void BeginInvokeAction(Action action);

        /// <summary>
/// Sets or replaces the thumbnail image for the sprite identified by <paramref name="key"/>.
/// </summary>
/// <param name="key">Unique identifier of the sprite whose thumbnail will be updated.</param>
/// <param name="thumbnail">Thumbnail image to assign for the sprite.</param>
        void UpdateSpriteThumbnail(string key, Image thumbnail);
        /// <summary>
/// Removes the thumbnail image associated with the specified sprite key.
/// </summary>
/// <param name="key">Identifier of the sprite whose thumbnail should be removed.</param>
void RemoveSpriteThumbnail(string key);
        /// <summary>
/// Removes all sprite thumbnail images from the view's thumbnail collections.
/// </summary>
void ClearSpriteThumbnails();

        /// <summary>
/// Marks the start of a batch update to the sprite list.
/// </summary>
/// <remarks>
/// Call before performing multiple modifications to the sprite list; pair with <see cref="EndUpdateSpriteList"/> when the batch is complete.
/// </remarks>
        void BeginUpdateSpriteList();
        /// <summary>
/// Marks the end of a batch update to the sprite list and causes the view to apply any accumulated changes.
/// </summary>
void EndUpdateSpriteList();
        /// <summary>
/// Ensures the view's <c>SpriteImageList</c> property is assigned, creating and assigning a new <c>SpriteImageList</c> if it is not already set.
/// </summary>
void EnsureSpriteImageListAssigned();
    }
}
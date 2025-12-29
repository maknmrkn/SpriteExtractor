using System;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using SpriteExtractor.Models;
using SpriteExtractor.Views;

namespace SpriteExtractor.Presenters
{
    // Scaffold for sprite-related operations. Start small and call into
    // MainPresenter internals as they are made accessible.
    public static class SpritePresenter
    {
        // Higher-level sprite operations. Uses MainPresenter internal APIs
        // exposed for incremental refactor.

        public static void DeleteSelectedSprite(MainPresenter main)
        {
            if (main == null) return;

            var view = main.View;
            var project = main.Project;
            var cmdManager = main.CommandManager;

            SpriteDefinition sprite = null;
            if (view?.SpriteListView?.SelectedItems.Count > 0)
                sprite = view.SpriteListView.SelectedItems[0].Tag as SpriteDefinition;

            if (sprite == null) return;

            int index = -1;
            if (project?.Sprites != null)
            {
                index = project.Sprites.IndexOf(sprite);
                if (index < 0) index = -1;
            }

            var result = MessageBox.Show($"Delete sprite '{sprite.Name}'?", "Confirm delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result != DialogResult.Yes) return;

            var cmd = new Services.DelegateCommand(
                execute: () => main.RemoveSpriteInternal(sprite),
                undo: () => main.InsertSpriteInternal(sprite, index),
                description: $"Delete '{sprite.Name}'"
            );

            cmdManager.ExecuteCommand(cmd);

            view?.UpdateStatus($"Sprite '{sprite.Name}' deleted");
        }

        public static void InsertSprite(MainPresenter main, SpriteDefinition sprite, int index)
        {
            if (main == null || sprite == null) return;
            main.InsertSpriteInternal(sprite, index);
        }

        public static void InsertNewSprite(MainPresenter main, SpriteDefinition sprite)
        {
            if (main == null || sprite == null) return;
            var project = main.Project;
            int index = project?.Sprites?.Count ?? 0;
            // Insert the sprite into the project first
            main.InsertSpriteInternal(sprite, index);
            
            // Ensure thumbnail is created before UI list rebuilds
            var key = main.GetSpriteKey(sprite);
            _ = CreateOrUpdateThumbnailAsync(main, sprite, key);
        }

        public static void RemoveSprite(MainPresenter main, SpriteDefinition sprite)
        {
            if (main == null || sprite == null) return;
            main.RemoveSpriteInternal(sprite);
        }

        public static async System.Threading.Tasks.Task CreateOrUpdateThumbnailAsync(MainPresenter main, SpriteDefinition sprite, string key)
        {
            if (main == null || sprite == null || string.IsNullOrEmpty(key)) return;

            try
            {
                // Try cache first
                if (main.ThumbnailCache.TryGetValue(sprite, out var cached) && cached != null)
                {
                    main.View?.UpdateSpriteThumbnail(key, cached);
                    if (!string.IsNullOrEmpty(sprite.Id))
                        main.View?.UpdateSpriteThumbnail(sprite.Id, cached);
                    return;
                }

                // Generate thumbnail off the UI thread
                var thumb = await Services.ThumbnailService.GenerateThumbnailAsync(main.LoadedBitmap, sprite.Bounds).ConfigureAwait(false);

                // cache (non-fatal)
                try { main.ThumbnailCache[sprite] = thumb; } catch { }

                // Post update to UI
                main.View?.BeginInvokeAction(() =>
                {
                    try
                    {
                        main.View?.UpdateSpriteThumbnail(key, thumb);
                        if (!string.IsNullOrEmpty(sprite.Id))
                            main.View?.UpdateSpriteThumbnail(sprite.Id, thumb);
                    }
                    catch { }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating thumbnail: {ex.Message}");
            }
        }

        public static async System.Threading.Tasks.Task UpdateAllThumbnailsAsync(MainPresenter main)
        {
            if (main == null) return;
            var view = main.View;
            var project = main.Project;
            if (view == null || project == null) return;

            try
            {
                view.ClearSpriteThumbnails();

                foreach (var sprite in project.Sprites)
                {
                    var key = main.GetSpriteKey(sprite); // Use the consistent key from main presenter
                    // run sequentially to avoid creating too many concurrent bitmaps
                    await CreateOrUpdateThumbnailAsync(main, sprite, key).ConfigureAwait(false);
                }

                view.UpdateSpriteList(project.Sprites);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating all thumbnails: {ex.Message}");
            }
        }
    }
}
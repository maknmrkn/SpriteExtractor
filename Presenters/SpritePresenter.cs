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
        /// <summary>
        /// Deletes the currently selected sprite after user confirmation and records the deletion as an undoable command.
        /// </summary>
        /// <remarks>
        /// If no sprite is selected or the user cancels the confirmation dialog, the method does nothing.
        /// </remarks>
        /// <param name="main">The main presenter providing access to the view, project, and command manager; if null the method does nothing.</param>

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

        /// <summary>
        /// Insert a sprite into the project's sprite collection at the specified index.
        /// </summary>
        /// <param name="sprite">The sprite definition to insert.</param>
        /// <param name="index">Zero-based position at which to insert the sprite in the project's sprite list.</param>
        public static void InsertSprite(MainPresenter main, SpriteDefinition sprite, int index)
        {
            if (main == null || sprite == null) return;
            main.InsertSpriteInternal(sprite, index);
        }

        /// <summary>
        /// Inserts the given sprite into the project's sprite collection and starts creating or updating its thumbnail.
        /// </summary>
        /// <param name="main">The main presenter that owns the project and view.</param>
        /// <param name="sprite">The sprite definition to insert.</param>
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

        /// <summary>
        /// Removes the given sprite from the application's project via the provided presenter.
        /// </summary>
        /// <param name="main">The main presenter that performs the removal.</param>
        /// <param name="sprite">The sprite definition to remove.</param>
        public static void RemoveSprite(MainPresenter main, SpriteDefinition sprite)
        {
            if (main == null || sprite == null) return;
            main.RemoveSpriteInternal(sprite);
        }

        /// <summary>
        /// Generates (or retrieves from cache) a thumbnail for the given sprite and updates the view with it.
        /// </summary>
        /// <param name="sprite">Sprite definition whose thumbnail should be generated or retrieved from cache.</param>
        /// <param name="key">Key used to identify and update the sprite's thumbnail in the view.</param>
        /// <returns>Completion of the thumbnail generation and view update operation.</returns>
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

        /// <summary>
        /// Clears existing thumbnails and regenerates thumbnails for every sprite sequentially, then refreshes the view's sprite list.
        /// </summary>
        /// <remarks>
        /// No action is taken if <paramref name="main"/> is null or if the presenter's view or project is unavailable. Thumbnails are generated sequentially to limit concurrent bitmap creation.
        /// </remarks>
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
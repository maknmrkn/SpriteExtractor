using System;
using System.IO;
using System.Windows.Forms;
using SpriteExtractor.Models;
using SpriteExtractor.Services;
using SpriteExtractor.Views;

namespace SpriteExtractor.Presenters
{
    public static class ProjectPresenter
    {
        /// <summary>
        /// Prompt the user with a Save dialog to persist the given sprite project to disk and update the UI on success.
        /// </summary>
        /// <param name="project">The sprite project to save. If <see cref="SpriteProject.SourceImagePath"/> is null or empty, a warning is shown and the save is aborted.</param>
        /// <param name="view">The main view used to update status messages; required for status updates.</param>
        public static void SaveProject(SpriteProject project, Views.IMainView view)
        {
            if (project == null || view == null) return;

            if (string.IsNullOrEmpty(project.SourceImagePath))
            {
                MessageBox.Show("Please load an image first", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var dialog = new SaveFileDialog
            {
                Filter = "Sprite Project|*.spriteproj|JSON|*.json",
                DefaultExt = ".spriteproj",
                FileName = project.ProjectName
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    ProjectService.SaveProject(project, dialog.FileName);
                    view.UpdateStatus($"Project saved: {Path.GetFileName(dialog.FileName)}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطا در ذخیره پروژه: {ex.Message}", "خطا",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// Opens a file dialog to load a sprite project, updates the view with the loaded sprites and status, and refreshes the image panel.
        /// </summary>
        /// <param name="view">The main view to update with the project's sprites and status; if null, no action is taken.</param>
        /// <returns>The loaded <see cref="SpriteProject"/> on success, or <c>null</c> if the dialog is cancelled, an error occurs, or <paramref name="view"/> is null.</returns>
        public static SpriteProject LoadProject(Views.IMainView view)
        {
            if (view == null) return null;

            using var dialog = new OpenFileDialog
            {
                Filter = "Sprite Project|*.spriteproj|JSON|*.json|All Files|*.*"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var proj = ProjectService.LoadProject(dialog.FileName);
                    view.UpdateSpriteList(proj.Sprites);
                    view.UpdateStatus($"Project loaded: {Path.GetFileName(dialog.FileName)}");
                    view.InvalidateImagePanel();
                    return proj;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading project: {ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            return null;
        }
    }
}
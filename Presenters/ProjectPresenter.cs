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

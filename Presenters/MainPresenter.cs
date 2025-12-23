using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;  // این خط را اضافه کنید
using System.Windows.Forms;
using SpriteExtractor.Models;
using SpriteExtractor.Services;
using SpriteExtractor.Views;

namespace SpriteExtractor.Presenters
{
    public class MainPresenter
    {
        private MainForm _view;
        private SpriteProject _project;
        private string _currentTool = "select";
        
        // متغیرهای مربوط به رسم مستطیل
        private Point _dragStart;
        private Rectangle _currentRect;
        private bool _isDragging = false;
        public enum SelectionMode { None, Drawing, Moving, Resizing }
        public enum ResizeHandle { None, TopLeft, Top, TopRight, Right, BottomRight, Bottom, BottomLeft, Left }

        // متغیرهای مدیریت حالت
        private SelectionMode _currentSelectionMode = SelectionMode.None;
        private SpriteDefinition _selectedSprite = null;
        private ResizeHandle _activeResizeHandle = ResizeHandle.None;
        private Point _lastMousePosition;
        
        public MainPresenter(MainForm view)
        {
            _view = view;
            _project = new SpriteProject();
            
            SetupEventHandlers();
            
        }
        
        private void SetupEventHandlers()
        {
            _view.ImagePanel.MouseDown += OnImagePanelMouseDown;
            _view.ImagePanel.MouseMove += OnImagePanelMouseMove;
            _view.ImagePanel.MouseUp += OnImagePanelMouseUp;
            _view.ImagePanel.Paint += OnImagePanelPaint;
              // 🔧 این خط برای Two-Way Binding ضروری است:
             //_view.PropertyGrid.PropertyValueChanged += OnPropertyGridValueChanged;
        }

            private void OnPropertyGridValueChanged(object s, PropertyValueChangedEventArgs e)
            {
                if (_selectedSprite == null) return;
                
                var propertyName = e.ChangedItem.PropertyDescriptor?.Name;
                
                // بررسی تغییرات موقعیت (X, Y)
                if (propertyName == "X" || propertyName == "Y")
                {
                    // موقعیت اسپرایت در صحنه تغییر کند
                    _view.ImagePanel.Invalidate();
                    UpdateListViewForSprite(_selectedSprite);
                }
                // 🔧 اضافه کردن بررسی تغییرات اندازه (Width, Height)
                else if (propertyName == "Width" || propertyName == "Height")
                {
                    // اندازه اسپرایت در صحنه تغییر کند
                    _view.ImagePanel.Invalidate();
                    UpdateListViewForSprite(_selectedSprite);
                    _view.UpdateStatus($"Size changed to {_selectedSprite.Bounds.Width}x{_selectedSprite.Bounds.Height}");
                }
            }

        // عملیات فایل - نسخه اصلاح شده بدون فریز
        public async void OpenImage()
        {
            // ابتدا دیالوگ را نشان بده (این در UI Thread اجرا می‌شود)
            using var dialog = new OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.bmp;*.gif|All Files|*.*",
                Title = "Select Sprite Sheet Image"
            };
            
            var dialogResult = dialog.ShowDialog();
            if (dialogResult != DialogResult.OK) return;
            
            try
            {
                _view.UpdateStatus("در حال بارگذاری تصویر...");
                
                // بارگذاری تصویر در Background برای جلوگیری از فریز
                await Task.Run(() =>
                {
                    _project.SourceImagePath = dialog.FileName;
                    _project.Sprites.Clear();
                });
                
                // آپدیت UI در Main Thread
                _view.UpdateSpriteList(_project.Sprites);
                _view.ImagePanel.Invalidate();
                
                _view.UpdateStatus($"بارگذاری شد: {Path.GetFileName(dialog.FileName)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطا در بارگذاری تصویر: {ex.Message}", "خطا", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                _view.UpdateStatus("خطا در بارگذاری تصویر");
            }
        }
        
        public void SaveProject()
        {
            if (string.IsNullOrEmpty(_project.SourceImagePath))
            {
                MessageBox.Show("Please load an image first", "Warning", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            using var dialog = new SaveFileDialog
            {
                Filter = "Sprite Project|*.spriteproj|JSON|*.json",
                DefaultExt = ".spriteproj",
                FileName = _project.ProjectName
            };
            
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    ProjectService.SaveProject(_project, dialog.FileName);
                    _view.UpdateStatus($"Project saved: {Path.GetFileName(dialog.FileName)}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطا در ذخیره پروژه: {ex.Message}", "خطا", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        
        public void LoadProject()
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Sprite Project|*.spriteproj|JSON|*.json|All Files|*.*"
            };
            
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    _project = ProjectService.LoadProject(dialog.FileName);
                    _view.UpdateSpriteList(_project.Sprites);
                    _view.UpdateStatus($"Project loaded: {Path.GetFileName(dialog.FileName)}");
                    _view.ImagePanel.Invalidate();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading project: {ex.Message}", "Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        
        // عملیات ویرایش
        public void SetToolMode(string tool) 
        { 
            _currentTool = tool; 
            _view.UpdateStatus($"Tool: {tool}");
        }
        
        public void DeleteSelectedSprite()
        {
            if (_view.SpriteListView.SelectedItems.Count > 0)
            {
                var sprite = _view.SpriteListView.SelectedItems[0].Tag as SpriteDefinition;
                if (sprite != null)
                {
                    _project.Sprites.Remove(sprite);
                    _view.UpdateSpriteList(_project.Sprites);
                    _view.ImagePanel.Invalidate();
                }
            }
        }
        
        public void OnSpriteSelected()
        {
            if (_view.SpriteListView.SelectedItems.Count > 0)
            {
                var sprite = _view.SpriteListView.SelectedItems[0].Tag as SpriteDefinition;
                _view.PropertyGrid.SelectedObject = sprite;
            }
        }
        
        // توابع Undo/Redo موقت
        public void Undo() 
        { 
            _view.UpdateStatus("Undo - Feature coming soon");
        }
        
        public void Redo() 
        { 
            _view.UpdateStatus("Redo - Feature coming soon");
        }
        
        // تشخیص خودکار موقت
        public void AutoDetect()
        {
            if (string.IsNullOrEmpty(_project.SourceImagePath))
            {
                MessageBox.Show("Please load an image first", "Warning", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            _view.UpdateStatus("Auto-detection - Feature coming soon");
        }
        
        public void ExportSprites()
        {
            if (string.IsNullOrEmpty(_project.SourceImagePath))
            {
                MessageBox.Show("Please load an image first", "Warning", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            if (_project.Sprites.Count == 0)
            {
                MessageBox.Show("No sprites to export", "Warning", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select output folder for sprites"
            };
            
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    ExportService.ExportSprites(_project, dialog.SelectedPath);
                    _view.UpdateStatus($"Exported {_project.Sprites.Count} sprites to {dialog.SelectedPath}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error exporting sprites: {ex.Message}", "Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        
        // کنترل‌های نمایش
        public void ZoomIn() 
        { 
            _view.UpdateStatus("Zoom In - Feature coming soon");
        }
        
        public void ZoomOut() 
        { 
            _view.UpdateStatus("Zoom Out - Feature coming soon");
        }
        
        public void ZoomFit() 
        { 
            _view.UpdateStatus("Fit to Screen - Feature coming soon");
        }
                
            private void OnImagePanelMouseDown(object sender, MouseEventArgs e)
        {
            _lastMousePosition = e.Location;
            
            if (_currentTool == "rectangle")
            {
                // حالت رسم مستطیل جدید
                _dragStart = e.Location;
                _currentRect = new Rectangle(e.X, e.Y, 0, 0);
                _isDragging = true;
                _currentSelectionMode = SelectionMode.Drawing;
                _selectedSprite = null;
            }
            else if (_currentTool == "select")
            {
                // ابتدا بررسی کن آیا روی دسته‌های Resize کلیک شده
                if (_selectedSprite != null)
                {
                    _activeResizeHandle = HitTestResizeHandles(_selectedSprite.Bounds, e.Location);
                    
                    if (_activeResizeHandle != ResizeHandle.None)
                    {
                        _currentSelectionMode = SelectionMode.Resizing;
                        _view.ImagePanel.Invalidate();
                        return;
                    }
                }
                
                // اگر روی دسته نبود، بررسی کن آیا روی خود اسپرایت کلیک شده
                var clickedSprite = HitTestSprites(e.Location);
                
                if (clickedSprite != null)
                {
                    _selectedSprite = clickedSprite;
                    _currentSelectionMode = SelectionMode.Moving;
                    _view.PropertyGrid.SelectedObject = _selectedSprite;
                    UpdateListViewSelection();
                }
                else
                {
                    _selectedSprite = null;
                    _currentSelectionMode = SelectionMode.None;
                    _view.PropertyGrid.SelectedObject = null;
                }
                
                _view.ImagePanel.Invalidate();
            }
        }

        // متد کمکی برای آپدیت انتخاب در ListView
        private void UpdateListViewSelection()
        {
            foreach (ListViewItem item in _view.SpriteListView.Items)
            {
                if (item.Tag == _selectedSprite)
                {
                    item.Selected = true;
                    item.EnsureVisible();
                    break;
                }
            }
        }
        
                private void OnImagePanelMouseMove(object sender, MouseEventArgs e)
        {
            // 1. حالت رسم مستطیل
            if (_isDragging && _currentTool == "rectangle")
            {
                _currentRect = new Rectangle(
                    Math.Min(_dragStart.X, e.X),
                    Math.Min(_dragStart.Y, e.Y),
                    Math.Abs(e.X - _dragStart.X),
                    Math.Abs(e.Y - _dragStart.Y)
                );
                _view.ImagePanel.Invalidate();
                return;
            }
            
            // 2. تغییر Cursor هنگام Hover روی دسته‌ها
            if (_currentTool == "select" && _selectedSprite != null && _currentSelectionMode == SelectionMode.None)
            {
                var handle = HitTestResizeHandles(_selectedSprite.Bounds, e.Location);
                _view.ImagePanel.Cursor = GetCursorForHandle(handle);
            }
            
            // 3. حالت Move (جابجایی اسپرایت)
            if (_currentSelectionMode == SelectionMode.Moving && _selectedSprite != null && e.Button == MouseButtons.Left)
            {
                int deltaX = e.X - _lastMousePosition.X;
                int deltaY = e.Y - _lastMousePosition.Y;
                
                var bounds = _selectedSprite.Bounds;
                bounds.X += deltaX;
                bounds.Y += deltaY;
                _selectedSprite.Bounds = bounds;
                
                _view.ImagePanel.Invalidate();
                RefreshPropertyGrid();
                _lastMousePosition = e.Location;
            }
            
            // 4. حالت Resize (تغییر اندازه)
            if (_currentSelectionMode == SelectionMode.Resizing && _selectedSprite != null && e.Button == MouseButtons.Left)
            {
                var bounds = _selectedSprite.Bounds;
                int deltaX = e.X - _lastMousePosition.X;
                int deltaY = e.Y - _lastMousePosition.Y;
                
                // اعمال تغییر اندازه بر اساس دسته فعال
                switch (_activeResizeHandle)
                {
                    case ResizeHandle.TopLeft:
                        bounds.X += deltaX;
                        bounds.Y += deltaY;
                        bounds.Width -= deltaX;
                        bounds.Height -= deltaY;
                        break;
                    case ResizeHandle.Top:
                        bounds.Y += deltaY;
                        bounds.Height -= deltaY;
                        break;
                    case ResizeHandle.TopRight:
                        bounds.Y += deltaY;
                        bounds.Width += deltaX;
                        bounds.Height -= deltaY;
                        break;
                    case ResizeHandle.Right:
                        bounds.Width += deltaX;
                        break;
                    case ResizeHandle.BottomRight:
                        bounds.Width += deltaX;
                        bounds.Height += deltaY;
                        break;
                    case ResizeHandle.Bottom:
                        bounds.Height += deltaY;
                        break;
                    case ResizeHandle.BottomLeft:
                        bounds.X += deltaX;
                        bounds.Width -= deltaX;
                        bounds.Height += deltaY;
                        break;
                    case ResizeHandle.Left:
                        bounds.X += deltaX;
                        bounds.Width -= deltaX;
                        break;
                }
                
                // جلوگیری از اندازه منفی (حداقل 5x5)
                if (bounds.Width < 5) bounds.Width = 5;
                if (bounds.Height < 5) bounds.Height = 5;
                
                _selectedSprite.Bounds = bounds;
                _view.ImagePanel.Invalidate();
                RefreshPropertyGrid();
                _lastMousePosition = e.Location;
            }
        }
        
        private void OnImagePanelMouseUp(object sender, MouseEventArgs e)
        {
            // پایان رسم مستطیل
            if (_isDragging && _currentTool == "rectangle")
            {
                _isDragging = false;
                
                if (_currentRect.Width > 5 && _currentRect.Height > 5)
                {
                    var sprite = new SpriteDefinition
                    {
                        Name = $"Sprite_{_project.Sprites.Count + 1}",
                        Bounds = _currentRect
                    };
                    
                    _project.Sprites.Add(sprite);
                    _view.UpdateSpriteList(_project.Sprites);
                }
                
                _currentRect = Rectangle.Empty;
                _view.ImagePanel.Invalidate();
            }
            
            // پایان حالت‌های Move و Resize
            if (_currentSelectionMode == SelectionMode.Moving || _currentSelectionMode == SelectionMode.Resizing)
            {
                _currentSelectionMode = SelectionMode.None;
                _activeResizeHandle = ResizeHandle.None;
                _view.ImagePanel.Cursor = Cursors.Default;
                _view.UpdateStatus($"Sprite updated. Position: ({_selectedSprite.Bounds.X}, {_selectedSprite.Bounds.Y}), Size: {_selectedSprite.Bounds.Width}x{_selectedSprite.Bounds.Height}");
            }
        }
        private void OnImagePanelPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            
            // اگر تصویری بارگذاری شده، آن را رسم کن
            if (!string.IsNullOrEmpty(_project.SourceImagePath) && File.Exists(_project.SourceImagePath))
            {
                try
                {
                    using var image = Image.FromFile(_project.SourceImagePath);
                    g.DrawImage(image, 0, 0);
                }
                catch
                {
                    // خطا در بارگذاری تصویر
                }
            }
            
            // رسم مستطیل موقت
            if (_isDragging && _currentTool == "rectangle")
            {
                using var pen = new Pen(Color.Red, 2);
                g.DrawRectangle(pen, _currentRect);
            }
                        // رسم دسته‌های Resize برای اسپرایت انتخاب‌شده
            if (_selectedSprite != null && _currentTool == "select")
            {
                var bounds = _selectedSprite.Bounds;
                
                // نقاط مرکزی دسته‌ها
                var handlePoints = new[]
                {
                    new Point(bounds.Left, bounds.Top),                     // TopLeft
                    new Point(bounds.Left + bounds.Width / 2, bounds.Top), // Top
                    new Point(bounds.Right, bounds.Top),                   // TopRight
                    new Point(bounds.Right, bounds.Top + bounds.Height / 2), // Right
                    new Point(bounds.Right, bounds.Bottom),                // BottomRight
                    new Point(bounds.Left + bounds.Width / 2, bounds.Bottom), // Bottom
                    new Point(bounds.Left, bounds.Bottom),                 // BottomLeft
                    new Point(bounds.Left, bounds.Top + bounds.Height / 2)   // Left
                };
                
                using var handleBrush = new SolidBrush(Color.White);
                using var handleBorderPen = new Pen(Color.Black, 1);
                
                foreach (var point in handlePoints)
                {
                    var handleRect = new Rectangle(
                        point.X - ResizeHandleSize / 2,
                        point.Y - ResizeHandleSize / 2,
                        ResizeHandleSize,
                        ResizeHandleSize
                    );
                    
                    // رسم دسته
                    g.FillRectangle(handleBrush, handleRect);
                    g.DrawRectangle(handleBorderPen, handleRect);
                }
            }
            // رسم مستطیل‌های ذخیره شده
            // رسم مستطیل‌های ذخیره شده
            var visibleSprites = _project.Sprites.Where(s => s.IsVisible).ToList();
            foreach (var sprite in visibleSprites)
            {
                // انتخاب رنگ بر اساس اینکه آیا این اسپرایت انتخاب شده یا نه
                Color borderColor = (sprite == _selectedSprite) ? Color.Blue : Color.Lime;
                float borderWidth = (sprite == _selectedSprite) ? 2.5f : 1.5f;
                
                using var pen = new Pen(borderColor, borderWidth);
                g.DrawRectangle(pen, sprite.Bounds);
                
                // نمایش نام
                using var brush = new SolidBrush(Color.White);
                g.DrawString(sprite.Name, 
                    new Font("Arial", 10, FontStyle.Bold), 
                    brush, 
                    sprite.Bounds.X, 
                    sprite.Bounds.Y - 20);
            }
        }
        private SpriteDefinition HitTestSprites(Point location)
        {
            // از آخر به اول می‌رویم تا اسپرایت‌های روی هم به درستی انتخاب شوند
            foreach (var sprite in _project.Sprites.AsEnumerable().Reverse())
            {
                if (sprite.Bounds.Contains(location))
                    return sprite;
            }
            return null;
        }
                private void RefreshPropertyGrid()
        {
            if (_selectedSprite != null)
            {
                // ترفند برای فورس کردن رفرش PropertyGrid
                var temp = _view.PropertyGrid.SelectedObject;
                _view.PropertyGrid.SelectedObject = null;
                _view.PropertyGrid.SelectedObject = temp;
                
                // همچنین لیست را هم آپدیت کن
                UpdateListViewForSprite(_selectedSprite);
            }
        }

        private void UpdateListViewForSprite(SpriteDefinition sprite)
        {
            foreach (ListViewItem item in _view.SpriteListView.Items)
            {
                if (item.Tag == sprite)
                {
                    item.SubItems[1].Text = $"{sprite.Bounds.X}, {sprite.Bounds.Y}";
                    item.SubItems[2].Text = $"{sprite.Bounds.Width}×{sprite.Bounds.Height}";
                    break;
                }
            }
        }
                // ثابت برای اندازه دسته‌ها
        private const int ResizeHandleSize = 8;

        // متد HitTest برای تشخیص کلیک روی دسته‌های تغییر اندازه
        private ResizeHandle HitTestResizeHandles(Rectangle bounds, Point location)
        {
            // لیست نقاط مرکزی دسته‌ها
            var handles = new Dictionary<ResizeHandle, Point>
            {
                { ResizeHandle.TopLeft, new Point(bounds.Left, bounds.Top) },
                { ResizeHandle.Top, new Point(bounds.Left + bounds.Width / 2, bounds.Top) },
                { ResizeHandle.TopRight, new Point(bounds.Right, bounds.Top) },
                { ResizeHandle.Right, new Point(bounds.Right, bounds.Top + bounds.Height / 2) },
                { ResizeHandle.BottomRight, new Point(bounds.Right, bounds.Bottom) },
                { ResizeHandle.Bottom, new Point(bounds.Left + bounds.Width / 2, bounds.Bottom) },
                { ResizeHandle.BottomLeft, new Point(bounds.Left, bounds.Bottom) },
                { ResizeHandle.Left, new Point(bounds.Left, bounds.Top + bounds.Height / 2) }
            };

            // بررسی برخورد با هر دسته
            foreach (var handle in handles)
            {
                var handleRect = new Rectangle(
                    handle.Value.X - ResizeHandleSize / 2,
                    handle.Value.Y - ResizeHandleSize / 2,
                    ResizeHandleSize,
                    ResizeHandleSize
                );

                if (handleRect.Contains(location))
                    return handle.Key;
            }

            return ResizeHandle.None;
        }

        // متد برای گرفتن Cursor مناسب برای هر دسته
        private Cursor GetCursorForHandle(ResizeHandle handle)
        {
            return handle switch
            {
                ResizeHandle.TopLeft or ResizeHandle.BottomRight => Cursors.SizeNWSE,
                ResizeHandle.TopRight or ResizeHandle.BottomLeft => Cursors.SizeNESW,
                ResizeHandle.Top or ResizeHandle.Bottom => Cursors.SizeNS,
                ResizeHandle.Left or ResizeHandle.Right => Cursors.SizeWE,
                _ => Cursors.Default
            };
        }

    }
}
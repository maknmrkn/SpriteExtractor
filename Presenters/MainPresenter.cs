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
using System.Drawing.Imaging; // این خط حیاتی است
//using System.Threading.Tasks;


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

        private SpriteDefinition _focusedSprite = null; // برای مدیریت focus
        public enum SelectionMode { None, Drawing, Moving, Resizing }
        public enum ResizeHandle { None, TopLeft, Top, TopRight, Right, BottomRight, Bottom, BottomLeft, Left }

        // متغیرهای مدیریت حالت
        private SelectionMode _currentSelectionMode = SelectionMode.None;
        private SpriteDefinition _selectedSprite = null;
        private ResizeHandle _activeResizeHandle = ResizeHandle.None;
        private Point _lastMousePosition;
        // در بالای کلاس MainPresenter، بعد از متغیرهای دیگر:
        private System.Windows.Forms.Timer _propertyChangeTimer;
        private Rectangle _lastKnownBounds = Rectangle.Empty;
        private bool _isPropertyGridMonitoring = false;
        private Bitmap _loadedBitmap;
                
        public MainPresenter(MainForm view)
        {
            _view = view;
            _project = new SpriteProject();
            
            SetupEventHandlers();
            SetupPropertyGridTimer(); // این خط را اضافه کنید
            // بعد از آن این خط را اضافه کنید:
SetupDoubleClickHandler();
        }

        private void SetupPropertyGridTimer()
        {
                _propertyChangeTimer = new System.Windows.Forms.Timer();
                 _propertyChangeTimer.Interval = 50; // 50 میلی‌ثانیه
                 _propertyChangeTimer.Tick += OnPropertyGridTimerTick;
        }

            private void OnPropertyGridTimerTick(object sender, EventArgs e)
            {
                if (_selectedSprite == null || !_isPropertyGridMonitoring) return;
                
                // مقایسه Bounds فعلی با آخرین وضعیت ذخیره‌شده
                if (_selectedSprite.Bounds != _lastKnownBounds)
                {
                    _lastKnownBounds = _selectedSprite.Bounds;
                    _view.ImagePanel.Invalidate();
                    UpdateListViewForSprite(_selectedSprite);
                }
            }

        private void SetupEventHandlers()
        {
            _view.ImagePanel.MouseDown += OnImagePanelMouseDown;
            _view.ImagePanel.MouseMove += OnImagePanelMouseMove;
            _view.ImagePanel.MouseUp += OnImagePanelMouseUp;
            _view.ImagePanel.Paint += OnImagePanelPaint;
              // 🔧 این خط برای Two-Way Binding ضروری است:
            // _view.PropertyGrid.PropertyValueChanged += OnPropertyGridValueChanged;
             _view.PropertyGrid.SelectedGridItemChanged += OnPropertyGridItemChanged;
             
        }

        // بعد از متد SetupEventHandlers، این متد را اضافه کنید:
private void SetupDoubleClickHandler()
{
    // دابل‌کلیک روی لیست اسپرایت‌ها
    _view.SpriteListView.MouseDoubleClick += (sender, e) =>
    {
        if (_view.SpriteListView.SelectedItems.Count > 0)
        {
            var sprite = _view.SpriteListView.SelectedItems[0].Tag as SpriteDefinition;
            if (sprite != null)
            {
                // اسکرول به موقعیت اسپرایت
                _view.ScrollToSprite(sprite.Bounds);
                
                // هایلایت متمایز (اختیاری - برای گام بعدی)
                _focusedSprite = sprite;
                _view.ImagePanel.Invalidate();
                
                _view.UpdateStatus($"Focused: {sprite.Name}");
            }
        }
    };
}

        private void OnPropertyGridItemChanged(object sender, SelectedGridItemChangedEventArgs e)
        {
            if (_selectedSprite == null) return;
            
            // این متد با هر تغییر انتخاب (حتی تغییر بین X, Y, Width, Height) فراخوانی می‌شود
            // می‌توانیم هر بار پنل را رفرش کنیم تا تغییرات نمایش داده شوند
            _view.ImagePanel.Invalidate();
            UpdateListViewForSprite(_selectedSprite);
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
            // این متد را به MainPresenter اضافه کنید
            private void UpdateSelectedSprite(SpriteDefinition sprite)
            {
                // ۱. تایمر قبلی را متوقف کن
                _propertyChangeTimer.Stop();
                _isPropertyGridMonitoring = false;
                
                // ۲. تمام انتخاب‌های قبلی در ListView را پاک کن
                foreach (ListViewItem item in _view.SpriteListView.Items)
                {
                    item.Selected = false;
                }
                
                // ۳. اسپرایت جدید را تنظیم کن
                _selectedSprite = sprite;
                
                if (_selectedSprite != null)
                {
                    // ۴. ذخیره وضعیت اولیه
                    _lastKnownBounds = _selectedSprite.Bounds;
                    _isPropertyGridMonitoring = true;
                    
                    // ۵. شروع مانیتورینگ
                    _propertyChangeTimer.Start();
                    
                    // ۶. آیتم مربوطه در ListView را انتخاب کن
                    foreach (ListViewItem item in _view.SpriteListView.Items)
                    {
                        if (item.Tag == _selectedSprite)
                        {
                            item.Selected = true;
                            item.EnsureVisible(); // اسکرول خودکار به آیتم
                            break;
                        }
                    }
                }
                
                // ۷. PropertyGrid را آپدیت کن
                _view.PropertyGrid.SelectedObject = _selectedSprite;
            }

        // عملیات فایل - نسخه اصلاح شده بدون فریز
            public async void OpenImage()
            {
                using var dialog = new OpenFileDialog
                {
                    Filter = "PNG Images|*.png|JPEG Images|*.jpg;*.jpeg|All Files|*.*",
                    Title = "Select Sprite Sheet Image"
                };
                
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // 🔧 تصویر قبلی را پاک کن
                        _loadedBitmap?.Dispose();
                        
                        // 🔧 بارگذاری تصویر با حفظ Alpha (شفافیت)
                        _loadedBitmap = LoadImageWithTransparency(dialog.FileName);

                        DebugImageTransparency(dialog.FileName);
                        
                        _project.SourceImagePath = dialog.FileName;
                        _project.Sprites.Clear();
                        
                        _view.UpdateStatus($"Loaded: {Path.GetFileName(dialog.FileName)}");
                        _view.ImagePanel.Invalidate();
                        
                        UpdateAllThumbnails();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error loading image: {ex.Message}", "Error", 
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }

            // 🔧 متد جدید برای بارگذاری تصویر با حفظ شفافیت
            private Bitmap LoadImageWithTransparency(string filePath)
            {
                // بارگذاری مستقیم - نیازی به تغییر فرمت نیست
                var bitmap = new Bitmap(filePath);
                
                // اگر تصویر شفافیت ندارد، همان را برگردان
                if (!bitmap.PixelFormat.HasFlag(PixelFormat.Alpha))
                {
                    Console.WriteLine("⚠️ Image has no alpha channel");
                    return bitmap;
                }
                
                Console.WriteLine("✅ Image has alpha channel");
                return bitmap;
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
                    // 🔥 فقط این یک خط را اضافه کنید:
                      UpdateSelectedSprite(null); // این خط جدید است
                }
            }
        }
        
       public void OnSpriteSelected()
{
    if (_view.SpriteListView.SelectedItems.Count > 0)
    {
        var sprite = _view.SpriteListView.SelectedItems[0].Tag as SpriteDefinition;
        _view.PropertyGrid.SelectedObject = sprite;
        
        // اسکرول خودکار هنگام انتخاب از لیست
        if (sprite != null)
        {
            _view.ScrollToSprite(sprite.Bounds);
            _focusedSprite = sprite; // تنظیم focus
            _view.ImagePanel.Invalidate(); // رندر مجدد برای هایلایت
        }
    }
    else
    {
        _focusedSprite = null; // اگر چیزی انتخاب نشده
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
               // 🔧 اگر ابزار rectangle است اما تصویری بارگذاری نشده، برگرد
            if (_currentTool == "rectangle" && string.IsNullOrEmpty(_project.SourceImagePath))
            {
                MessageBox.Show("Please load an image first.", "Info", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
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
                            // ✅ این خط تغییر کرد:
                            UpdateSelectedSprite(clickedSprite);
                            _currentSelectionMode = SelectionMode.Moving;
                        }
                else
                        {
                            // ✅ این خط تغییر کرد:
                            UpdateSelectedSprite(null);
                            _currentSelectionMode = SelectionMode.None;
                        }
                
                _view.ImagePanel.Invalidate();
            }
        }

        // متد کمکی برای آپدیت انتخاب در ListView
        
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
                    UpdateAllThumbnails(); // 📌 ساخت Thumbnail + آپدیت لیست
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
                 if (_selectedSprite != null)
                    UpdateThumbnailForSprite(_selectedSprite); // 📌 فقط Thumbnail این اسپرایت را آپدیت کن
                   _view.UpdateStatus($"Sprite updated. Position: ({_selectedSprite.Bounds.X}, {_selectedSprite.Bounds.Y}), Size: {_selectedSprite.Bounds.Width}x{_selectedSprite.Bounds.Height}");
                
                
            }
        }

        public void SetHighlightColor(Color color)
            {
                _project.Settings.HighlightColor = color;
                _view.ImagePanel.Invalidate(); // رندر مجدد برای اعمال رنگ جدید
                
                // ذخیره در تنظیمات کاربر (اختیاری)
                //Properties.Settings.Default.HighlightColor = color;
                //Properties.Settings.Default.Save();
            }

        public Color GetHighlightColor()
            {
                return _project.Settings.HighlightColor;
            }

        private void OnImagePanelPaint(object sender, PaintEventArgs e)
        {
            
                var g = e.Graphics;
                
                // ۱. ابتدا پس‌زمینه شطرنجی بکش (برای نمایش شفافیت)
                if (_checkerboardBrush == null)
                {
                    var pattern = CreateCheckerboardPattern();
                    _checkerboardBrush = new TextureBrush(pattern);
                }
                
                g.FillRectangle(_checkerboardBrush, _view.ImagePanel.ClientRectangle);
                
                // ۲. اگر تصویر بارگذاری شده، آن را با تنظیمات مناسب رسم کن
                if (_loadedBitmap != null)
                {
                    // 🔧 تنظیمات کیفیت برای حفظ شفافیت
                    g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    
                    // 🔧 پارامترهای DrawImage که شفافیت را حفظ می‌کنند
                    var imageAttr = new System.Drawing.Imaging.ImageAttributes();
                    
                    // مهم: ماتریس رنگ را تنظیم کن (بدون تغییر Alpha)
                    imageAttr.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix
                    {
                        Matrix00 = 1, Matrix01 = 0, Matrix02 = 0, Matrix03 = 0, Matrix04 = 0,
                        Matrix10 = 0, Matrix11 = 1, Matrix12 = 0, Matrix13 = 0, Matrix14 = 0,
                        Matrix20 = 0, Matrix21 = 0, Matrix22 = 1, Matrix23 = 0, Matrix24 = 0,
                        Matrix30 = 0, Matrix31 = 0, Matrix32 = 0, Matrix33 = 1, Matrix34 = 0,
                        Matrix40 = 0, Matrix41 = 0, Matrix42 = 0, Matrix43 = 0, Matrix44 = 1
                    });
                    
                    // رسم تصویر با حفظ شفافیت
                    g.DrawImage(
                        _loadedBitmap,
                        new Rectangle(0, 0, _loadedBitmap.Width, _loadedBitmap.Height),
                        0, 0, _loadedBitmap.Width, _loadedBitmap.Height,
                        GraphicsUnit.Pixel,
                        imageAttr
                    );
                    
                    imageAttr.Dispose();
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
    // تشخیص اسپرایت focus شده
    bool isFocused = (sprite == _focusedSprite);
    
    // رنگ و thickness متفاوت برای focus
    var penColor = isFocused ? Color.Cyan : Color.Lime;
    var penThickness = isFocused ? 2.5f : 1f;
    
    using var pen = new Pen(penColor, penThickness);
    g.DrawRectangle(pen, sprite.Bounds);
    
    // نمایش نام با رنگ متفاوت برای focus
    var textColor = isFocused ? Color.Yellow : Color.White;
    using var brush = new SolidBrush(textColor);
    g.DrawString(sprite.Name, 
        new Font("Arial", isFocused ? 11 : 10, 
                isFocused ? FontStyle.Bold : FontStyle.Regular), 
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
        // این متد را به MainPresenter اضافه کنید
        
             private void UpdateThumbnailForSprite(SpriteDefinition sprite)
            {
                if (sprite == null || _loadedBitmap == null) return;
                
                try
                {
                    var thumbnail = new Bitmap(48, 48, PixelFormat.Format32bppArgb);
                    
                    using (var g = Graphics.FromImage(thumbnail))
                    {
                        // ۱. پس‌زمینه شطرنجی برای Thumbnail
                        if (_checkerboardBrush != null)
                        {
                            g.FillRectangle(_checkerboardBrush, 0, 0, 48, 48);
                        }
                        else
                        {
                            g.Clear(Color.DarkGray);
                        }
                        
                        // ۲. محاسبه scale
                        float scaleX = 46f / sprite.Bounds.Width;
                        float scaleY = 46f / sprite.Bounds.Height;
                        float scale = Math.Min(scaleX, scaleY);
                        
                        int destWidth = (int)(sprite.Bounds.Width * scale);
                        int destHeight = (int)(sprite.Bounds.Height * scale);
                        int destX = (48 - destWidth) / 2;
                        int destY = (48 - destHeight) / 2;
                        
                        // ۳. تنظیمات برای حفظ شفافیت
                        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                        
                        // ۴. رسم ناحیه اسپرایت
                        if (sprite.Bounds.Width > 0 && sprite.Bounds.Height > 0)
                        {
                            g.DrawImage(_loadedBitmap,
                                new Rectangle(destX + 1, destY + 1, destWidth - 2, destHeight - 2),
                                sprite.Bounds,
                                GraphicsUnit.Pixel);
                        }
                        
                        // ۵. حاشیه سفید دور Thumbnail
                        using var pen = new Pen(Color.White, 1);
                        g.DrawRectangle(pen, destX, destY, destWidth, destHeight);
                    }
                    
                    // ذخیره Thumbnail
                    _view.SpriteThumbnails.AddOrUpdateThumbnail(sprite.Id, thumbnail);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating thumbnail: {ex.Message}");
                }
            }

            private void UpdateAllThumbnails()
            {
                if (string.IsNullOrEmpty(_project.SourceImagePath)) return;
                
                try
                {
                    _view.SpriteThumbnails.Clear();
                    
                    foreach (var sprite in _project.Sprites)
                    {
                        UpdateThumbnailForSprite(sprite);
                    }
                    
                    _view.UpdateSpriteList(_project.Sprites);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating thumbnails: {ex.Message}");
                }
            } 
              
             public void OnListViewItemSelected(SpriteDefinition sprite)
            {
                if (sprite != null && sprite != _selectedSprite)
                {
                    UpdateSelectedSprite(sprite);
                    _view.ImagePanel.Invalidate(); // رندر مجدد برای هایلایت
                }
            }

            public void FocusOnSprite(SpriteDefinition sprite)
            {
                if (sprite != null && sprite != _selectedSprite)
                {
                    // از متد موجود UpdateSelectedSprite استفاده می‌کنیم
                    UpdateSelectedSprite(sprite);
                    
                    // رندر مجدد برای اعمال هایلایت
                    _view.ImagePanel.Invalidate();
                    
                    // نمایش پیام وضعیت
                    _view.UpdateStatus($"Focus on: {sprite.Name} (Double-click)");
                    
                    // TODO: در آینده می‌توانیم اسکرول خودکار به موقعیت اسپرایت اضافه کنیم
                }
            }
            // در MainPresenter، متدی برای پاکسازی اضافه کنید
            public void Cleanup()
            {
                _loadedBitmap?.Dispose();
                _loadedBitmap = null;
                
                _checkerboardBrush?.Dispose();
                _checkerboardBrush = null;
                
                _view.SpriteThumbnails?.Clear();
                _propertyChangeTimer?.Stop();
                _propertyChangeTimer?.Dispose();
            }

            private void DebugImageTransparency(string filePath)
            {
                try
                {
                    using var bmp = new Bitmap(filePath);
                    Console.WriteLine($"📊 Image Debug: {Path.GetFileName(filePath)}");
                    Console.WriteLine($"   Size: {bmp.Width}x{bmp.Height}");
                    Console.WriteLine($"   PixelFormat: {bmp.PixelFormat}");
                    Console.WriteLine($"   HasAlpha: {bmp.PixelFormat.HasFlag(PixelFormat.Alpha)}");
                    
                    // تست پیکسل‌های گوشه‌ها
                    var corners = new[] { new Point(0, 0), new Point(bmp.Width-1, 0), 
                                        new Point(0, bmp.Height-1), new Point(bmp.Width-1, bmp.Height-1) };
                    
                    foreach (var point in corners)
                    {
                        if (point.X < bmp.Width && point.Y < bmp.Height)
                        {
                            var color = bmp.GetPixel(point.X, point.Y);
                            Console.WriteLine($"   Pixel({point.X},{point.Y}): A={color.A}, R={color.R}, G={color.G}, B={color.B}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Debug error: {ex.Message}");
                }
            }

            private Bitmap CreateCheckerboardPattern(int cellSize = 10)
            {
                var pattern = new Bitmap(cellSize * 2, cellSize * 2);
                
                using (var g = Graphics.FromImage(pattern))
                {
                    // سلول خاکستری تیره
                    using (var darkBrush = new SolidBrush(Color.FromArgb(100, 100, 100)))
                    {
                        g.FillRectangle(darkBrush, 0, 0, cellSize, cellSize);
                        g.FillRectangle(darkBrush, cellSize, cellSize, cellSize, cellSize);
                    }
                    
                    // سلول خاکستری روشن
                    using (var lightBrush = new SolidBrush(Color.FromArgb(150, 150, 150)))
                    {
                        g.FillRectangle(lightBrush, cellSize, 0, cellSize, cellSize);
                        g.FillRectangle(lightBrush, 0, cellSize, cellSize, cellSize);
                    }
                }
                
                return pattern;
            }

            private TextureBrush _checkerboardBrush = null;

    }
}
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection; // این خط حیاتی است
// using System.Threading.Tasks removed during refactor
using System.Windows.Forms;
using SpriteExtractor.Models;
using SpriteExtractor.Services;
using SpriteExtractor.Views;

namespace SpriteExtractor.Presenters
{
    public class MainPresenter
    {
        private Views.IMainView _view;
        private SpriteProject _project;
        private string _currentTool = "select";
        // بالای کلاس MainPresenter، کنار سایر فیلدها
        private readonly Dictionary<SpriteDefinition, Image> _thumbnailCache = new();
        // نگهداری کلیدهای پایدار برای اسپرایت‌هایی که Id ندارند
        private readonly Dictionary<SpriteDefinition, string> _spriteKeys = new();

        // متغیرهای مربوط به رسم مستطیل
        private Point _dragStart;
        private Rectangle _currentRect;
        private bool _isDragging = false;

        private bool _suppressListSelectionChanged = false;
        public bool IsSuppressingListSelection => _suppressListSelectionChanged;
        // Internal accessors for incremental refactor
        internal Views.IMainView View => _view;
        internal SpriteProject Project => _project;
        internal Services.CommandManager CommandManager => _commandManager;
        internal Bitmap LoadedBitmap => _loadedBitmap;
        internal TextureBrush CheckerboardBrush => _checkerboardBrush;
        internal Dictionary<SpriteDefinition, Image> ThumbnailCache => _thumbnailCache;
        // Command manager
        private readonly Services.CommandManager _commandManager = new Services.CommandManager();
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
        private int _spriteCounter = 1; /// <summary>
        /// Creates a MainPresenter that manages application state and coordinates the provided main view.
        /// </summary>
        /// <param name="view">The main view implementation used for UI interactions (implements IMainView).</param>
        public MainPresenter(Views.IMainView view)
        {
            _view = view;
            _project = new SpriteProject();
            _commandManager = new CommandManager();
            _commandManager.OperationPerformed += OnCommandOperationPerformed;

            SetupEventHandlers();
            SetupPropertyGridTimer(); // این خط را اضافه کنید
            // بعد از آن این خط را اضافه کنید:
            SetupDoubleClickHandler();

        }
        /// <summary>
        /// Initialize and configure the timer used to poll for property-grid changes.
        /// </summary>
        /// <remarks>
        /// The timer is created, set to a 50 millisecond interval, and wired to the <see cref="OnPropertyGridTimerTick"/> handler.
        /// </remarks>
        private void SetupPropertyGridTimer()
        {
            _propertyChangeTimer = new System.Windows.Forms.Timer();
            _propertyChangeTimer.Interval = 50; // 50 میلی‌ثانیه
            _propertyChangeTimer.Tick += OnPropertyGridTimerTick;
        }

        /// <summary>
        /// Monitors the selected sprite's bounds while property-grid monitoring is active and refreshes the image panel and list view when the bounds change.
        /// </summary>
        private void OnPropertyGridTimerTick(object sender, EventArgs e)
        {
            if (_selectedSprite == null || !_isPropertyGridMonitoring) return;

            // مقایسه Bounds فعلی با آخرین وضعیت ذخیره‌شده
            if (_selectedSprite.Bounds != _lastKnownBounds)
            {
                _lastKnownBounds = _selectedSprite.Bounds;
                _view.InvalidateImagePanel();
                UpdateListViewForSprite(_selectedSprite);
            }
        }

        /// <summary>
        /// Subscribes the presenter's handlers to the view's UI events so the presenter receives mouse, paint, and property-grid change notifications.
        /// </summary>
        /// <remarks>
        /// Hooks ImagePanel mouse and paint events and subscribes to PropertyGrid selection/value change notifications required for keeping the model and UI in sync.
        /// </remarks>
        private void SetupEventHandlers()
        {
            // Event wiring still requires access to the panel control
            _view.ImagePanel.MouseDown += OnImagePanelMouseDown;
            _view.ImagePanel.MouseMove += OnImagePanelMouseMove;
            _view.ImagePanel.MouseUp += OnImagePanelMouseUp;
            _view.ImagePanel.Paint += OnImagePanelPaint;
            // 🔧 این خط برای Two-Way Binding ضروری است:
            // _view.PropertyGrid.PropertyValueChanged += OnPropertyGridValueChanged;
            _view.PropertyGrid.SelectedGridItemChanged += OnPropertyGridItemChanged;


        }

        /// <summary>
        /// Attaches a handler to the sprite list's double-click event that focuses the double-clicked sprite.
        /// </summary>
        /// <remarks>
        /// When a sprite list item is double-clicked, the view scrolls to the sprite's bounds, sets that sprite as focused,
        /// invalidates the image panel to refresh the display, and updates the status message with the sprite's name.
        /// </remarks>
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
                        _view.InvalidateImagePanel();

                        _view.UpdateStatus($"Focused: {sprite.Name}");
                    }
                }
            };
        }

        /// <summary>
        /// Refreshes the image panel and synchronizes the sprite list row when the PropertyGrid's selected item changes for the currently selected sprite.
        /// </summary>
        /// <param name="sender">The PropertyGrid that raised the event.</param>
        /// <param name="e">Event data describing the newly selected grid item.</param>
        private void OnPropertyGridItemChanged(object sender, SelectedGridItemChangedEventArgs e)
        {
            if (_selectedSprite == null) return;

            // این متد با هر تغییر انتخاب (حتی تغییر بین X, Y, Width, Height) فراخوانی می‌شود
            // می‌توانیم هر بار پنل را رفرش کنیم تا تغییرات نمایش داده شوند
            _view.InvalidateImagePanel();
            UpdateListViewForSprite(_selectedSprite);
        }

        /// <summary>
        /// Responds to changes made in the property grid for the currently selected sprite and updates the view and list state accordingly.
        /// </summary>
        /// <param name="s">The sender of the property value changed event (property grid).</param>
        /// <param name="e">Event arguments containing the changed property item and its new value.</param>
        private void OnPropertyGridValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            if (_selectedSprite == null) return;

            var propertyName = e.ChangedItem.PropertyDescriptor?.Name;

            // بررسی تغییرات موقعیت (X, Y)
            if (propertyName == "X" || propertyName == "Y")
            {
                // موقعیت اسپرایت در صحنه تغییر کند
                _view.InvalidateImagePanel();
                UpdateListViewForSprite(_selectedSprite);
            }
            // 🔧 اضافه کردن بررسی تغییرات اندازه (Width, Height)
            else if (propertyName == "Width" || propertyName == "Height")
            {
                // اندازه اسپرایت در صحنه تغییر کند
                _view.InvalidateImagePanel();
                UpdateListViewForSprite(_selectedSprite);
                _view.UpdateStatus($"Size changed to {_selectedSprite.Bounds.Width}x{_selectedSprite.Bounds.Height}");
            }
        }
        /// <summary>
        /// Make the specified sprite the active selection and synchronize UI state to match.
        /// </summary>
        /// <param name="sprite">The sprite to select, or null to clear the current selection.</param>
        /// <remarks>
        /// Side effects: stops and (for a non-null sprite) starts the property-change monitoring timer, clears and updates the list view selection to the specified sprite (suppressing selection-changed events during the update), and sets the PropertyGrid's SelectedObject to the sprite.
        /// </remarks>
        private void UpdateSelectedSprite(SpriteDefinition sprite)
        {
            // ۱. تایمر قبلی را متوقف کن
            _propertyChangeTimer?.Stop();
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
                _suppressListSelectionChanged = true;
                try
                {
                    if (_view?.SpriteListView != null)
                    {
                        foreach (ListViewItem item in _view.SpriteListView.Items)
                        {
                            bool shouldSelect = (item.Tag == _selectedSprite);
                            if (item.Selected != shouldSelect)
                                item.Selected = shouldSelect;
                            if (shouldSelect)
                                item.EnsureVisible();
                        }
                    }
                }
                finally
                {
                    _suppressListSelectionChanged = false;
                }

            }

            // ۷. PropertyGrid را آپدیت کن
            _view.PropertyGrid.SelectedObject = _selectedSprite;
        }

        /// <summary>
        /// Prompts the user to select an image file, loads it as the project's sprite sheet, and reinitializes project state.
        /// </summary>
        /// <remarks>
        /// On successful selection and load this method:
        /// - sets the project's SourceImagePath,
        /// - clears the project's sprite list,
        /// - resets the internal sprite counter to 1,
        /// - replaces the presenter's loaded bitmap,
        /// - updates the UI status and invalidates the image panel,
        /// - and requests an asynchronous rebuild of all thumbnails via SpritePresenter.
        /// If an error occurs while loading the image, a message box is shown describing the error.
        /// </remarks>
        public void OpenImage()
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
                    _loadedBitmap?.Dispose();
                    _loadedBitmap = LoadImageWithTransparency(dialog.FileName);
                    DebugImageTransparency(dialog.FileName);

                    _project.SourceImagePath = dialog.FileName;
                    _project.Sprites.Clear();

                    // ✅ Reset کردن counter برای پروژه جدید
                    _spriteCounter = 1;

                    _view.UpdateStatus($"Loaded: {Path.GetFileName(dialog.FileName)}");
                    _view.InvalidateImagePanel();

                    // Delegate full thumbnail rebuild to SpritePresenter (async)
                    _ = Presenters.SpritePresenter.UpdateAllThumbnailsAsync(this);
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

        /// <summary>
        /// Persists the current sprite project to storage and updates the view accordingly.
        /// </summary>
        public void SaveProject()
        {
            Presenters.ProjectPresenter.SaveProject(_project, _view);
        }

        /// <summary>
        /// Loads a SpriteProject via the ProjectPresenter and, if successful, replaces the presenter's current project with the loaded project.
        /// </summary>
        public void LoadProject()
        {
            var proj = Presenters.ProjectPresenter.LoadProject(_view);
            if (proj != null)
            {
                _project = proj;
            }
        }

        // عملیات ویرایش
        public void SetToolMode(string tool)
        {
            _currentTool = tool;
            _view.UpdateStatus($"Tool: {tool}");
        }

        /// <summary>
        /// Deletes the currently selected sprite from the project, updates UI state, and records the action for undo.
        /// </summary>
        public void DeleteSelectedSprite()
        {
            Presenters.SpritePresenter.DeleteSelectedSprite(this);
        }
        /// <summary>
/// Reverses the last executed command in the command manager.
/// </summary>
/// <remarks>
/// Does nothing if there is no operation available to undo.
/// </remarks>
        public void Undo() => _commandManager.Undo();
        public void Redo() => _commandManager.Redo();
        public bool CanUndo() => _commandManager.CanUndo;
        public bool CanRedo() => _commandManager.CanRedo;




        // حذف واقعی بدون مدیریت undo stack (private helper)
        // حذف واقعی بدون مدیریت undo stack (private helper)
        /// <summary>
        /// Remove a sprite from the project and UI, cleaning up its thumbnails and cache and updating selection and the image panel.
        /// </summary>
        /// <param name="sprite">The sprite definition to remove; if null the method does nothing.</param>
        internal void RemoveSpriteInternal(SpriteDefinition sprite)
        {
            if (sprite == null) return;

            // 🔑 استخراج کلید پایدار برای thumbnail قبل از هر کار
            string spriteKey = !string.IsNullOrEmpty(sprite.Id) ? sprite.Id : (_spriteKeys.TryGetValue(sprite, out var k) ? k : null);

            System.Diagnostics.Debug.WriteLine($"🗑️ Removing sprite: {sprite.Name} with key: {spriteKey ?? "(none)"}");

            // پیدا کردن index فعلی در مدل قبل از حذف
            int modelIndex = -1;
            if (_project?.Sprites != null)
                modelIndex = _project.Sprites.IndexOf(sprite);

            // حذف از مدل فقط اگر موجود باشد
            if (_project?.Sprites != null && modelIndex >= 0)
            {
                _project.Sprites.RemoveAt(modelIndex);
            }

            // 🎯 حذف thumbnail با استفاده از Id منحصربه‌فرد
            try
            {
                if (!string.IsNullOrEmpty(spriteKey))
                {
                    _view.RemoveSpriteThumbnail(spriteKey);
                }

                // همچنین از cache محلی هم حذف کن
                if (_thumbnailCache.ContainsKey(sprite))
                {
                    _thumbnailCache[sprite]?.Dispose();
                    _thumbnailCache.Remove(sprite);
                }

                // و اگر کلید موقت در map داشتیم، آن را پاک کن
                if (_spriteKeys.ContainsKey(sprite))
                    _spriteKeys.Remove(sprite);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error removing thumbnail: {ex.Message}");
            }

            // حذف آیتم از ListView
            if (_view?.SpriteListView != null)
            {
                _view.BeginUpdateSpriteList();
                try
                {
                    ListViewItem toRemove = null;

                    // 🔍 پیدا کردن آیتم بر اساس Tag (reference equality)
                    foreach (ListViewItem item in _view.SpriteListView.Items)
                    {
                        if (ReferenceEquals(item.Tag, sprite))
                        {
                            toRemove = item;
                            System.Diagnostics.Debug.WriteLine($"   Found ListView item to remove: {item.Text}");
                            break;
                        }
                    }

                    if (toRemove != null)
                    {
                        _suppressListSelectionChanged = true;
                        try
                        {
                            int removedIndex = _view.SpriteListView.Items.IndexOf(toRemove);
                            _view.SpriteListView.Items.Remove(toRemove);

                            // فقط انتخاب آیتم مجاور را انجام بده، بدون ریلود کل لیست
                            if (_view.SpriteListView.Items.Count > 0)
                            {
                                int selectIndex = Math.Min(removedIndex, _view.SpriteListView.Items.Count - 1);
                                var newItem = _view.SpriteListView.Items[selectIndex];
                                newItem.Selected = true;

                                if (newItem.Tag is SpriteDefinition newSprite)
                                    UpdateSelectedSprite(newSprite);
                                else
                                    UpdateSelectedSprite(null);
                            }
                            else
                            {
                                UpdateSelectedSprite(null);
                            }
                        }
                        finally
                        {
                            _suppressListSelectionChanged = false;
                        }
                    }
                }
                finally
                {
                    _view.EndUpdateSpriteList();
                }
            }

            // رفرش صریح UI و پنل تصویر
            _view?.SpriteListView?.Refresh();
            _view?.InvalidateImagePanel();
        }




        // درج واقعی بدون مدیریت undo stack (private helper)
        /// <summary>
        /// Inserts a sprite into the project and the sprite list UI, updates selection and list-row data, and triggers thumbnail creation.
        /// </summary>
        /// <param name="sprite">The sprite definition to insert; if null, the method is a no-op.</param>
        /// <param name="index">Desired insertion index; values less than zero or greater than the current count are clamped to the end.</param>
        internal void InsertSpriteInternal(SpriteDefinition sprite, int index)
        {
            if (sprite == null) return;

            // درج در مدل با clamp ایندکس
            if (_project?.Sprites != null)
            {
                if (index < 0 || index > _project.Sprites.Count)
                    index = _project.Sprites.Count;
                _project.Sprites.Insert(index, sprite);
            }

            var spriteKey = GetSpriteKey(sprite);

            // Delegate thumbnail creation/registration to SpritePresenter (async)
            _ = Presenters.SpritePresenter.CreateOrUpdateThumbnailAsync(this, sprite, spriteKey);

            // درج در ListView
            if (_view?.SpriteListView != null)
            {
                // اطمینان از اینکه ImageList به ListView اختصاص داده شده است
                _view.EnsureSpriteImageListAssigned();
                _view.BeginUpdateSpriteList();
                try
                {
                    var item = new ListViewItem(sprite.Name ?? "Sprite")
                    {
                        Tag = sprite,
                        ImageKey = spriteKey  // 🔑 استفاده از کلید منحصربه‌فرد
                    };

                    // اطمینان از وجود حداقل 3 SubItem
                    while (item.SubItems.Count < 3)
                        item.SubItems.Add(string.Empty);

                    item.SubItems[1].Text = $"{sprite.Bounds.X}, {sprite.Bounds.Y}";
                    item.SubItems[2].Text = $"{sprite.Bounds.Width}×{sprite.Bounds.Height}";

                    _suppressListSelectionChanged = true;
                    try
                    {
                        if (index < 0 || index > _view.SpriteListView.Items.Count)
                            index = _view.SpriteListView.Items.Count;

                        _view.SpriteListView.Items.Insert(index, item);

                        // انتخاب آیتم درج‌شده تا UI همگام شود
                        item.Selected = true;
                        item.EnsureVisible();
                    }
                    finally
                    {
                        _suppressListSelectionChanged = false;
                    }
                }
                finally
                {
                    _view.EndUpdateSpriteList();
                }
            }

            // همگام‌سازی انتخاب در presenter و به‌روزرسانی ردیف
            UpdateSelectedSprite(sprite);
            UpdateListViewForSprite(sprite);

            // رفرش صریح UI و پنل تصویر
            _view?.SpriteListView?.Refresh();
            _view?.InvalidateImagePanel();
            _view?.BeginInvokeAction(() => { /* no-op update wrapper if needed */ });
        }

        // متد کمکی برای تولید thumbnail از bitmap اصلی
        /// <summary>
        /// Synchronizes UI state when the user selects a sprite in the list view.
        /// </summary>
        /// <remarks>
        /// Sets the PropertyGrid to the selected sprite, scrolls the image panel to the sprite's bounds,
        /// sets the presenter's focused sprite, and requests an image-panel redraw. If no item is selected,
        /// clears the focused sprite.
        /// </remarks>





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
                    _view.InvalidateImagePanel(); // رندر مجدد برای هایلایت
                }
            }
            else
            {
                _focusedSprite = null; // اگر چیزی انتخاب نشده
            }
        }

        // توابع Undo/Redo موقت


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

        /// <summary>
        /// Handle mouse-down events on the image panel to begin drawing, select, move, or resize sprites.
        /// </summary>
        /// <param name="sender">The control that raised the event (image panel).</param>
        /// <param name="e">Mouse event data containing the click location and button information.</param>
        /// <remarks>
        /// If no image is loaded, shows an informational message and aborts. If the click hits an existing sprite, the presenter
        /// switches to select mode and enters either moving or resizing depending on which resize handle (if any) was hit.
        /// If the click is on empty space, begins a new rectangle draw operation and clears the current selection.
        /// The method requests a panel redraw after updating state.
        /// </remarks>
        private void OnImagePanelMouseDown(object sender, MouseEventArgs e)
        {
            // If no image is loaded, don't proceed
            if (string.IsNullOrEmpty(_project.SourceImagePath))
            {
                MessageBox.Show("Please load an image first.", "Info",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _lastMousePosition = e.Location;

            // Check if clicked on an existing sprite first
            var clickedSprite = HitTestSprites(e.Location);

            if (clickedSprite != null)
            {
                // If clicked on an existing sprite, switch to select mode and allow moving
                _currentTool = "select"; // Automatically switch to select mode
                UpdateSelectedSprite(clickedSprite);
                
                // Check if clicked on resize handles
                _activeResizeHandle = HitTestResizeHandles(clickedSprite.Bounds, e.Location);

                if (_activeResizeHandle != ResizeHandle.None)
                {
                    _currentSelectionMode = SelectionMode.Resizing;
                }
                else
                {
                    // Otherwise, start moving the sprite
                    _currentSelectionMode = SelectionMode.Moving;
                }
            }
            else
            {
                // If clicked on empty space, start drawing a new rectangle regardless of current tool
                // حالت رسم مستطیل جدید
                _dragStart = e.Location;
                _currentRect = new Rectangle(e.X, e.Y, 0, 0);
                _isDragging = true;
                _currentSelectionMode = SelectionMode.Drawing;
                _selectedSprite = null;
                
                // Deselect any currently selected sprite
                UpdateSelectedSprite(null);
            }

            _view.InvalidateImagePanel();
        }

        /// <summary>
        /// Handle mouse-move events on the image panel to update drawing rectangles, move or resize the selected sprite, adjust the cursor over resize handles, and synchronize thumbnails, the property grid, and the list view in real time.
        /// </summary>
        /// <param name="sender">The event source (image panel).</param>
        /// <param name="e">Mouse event data; the location and button state determine drawing, moving, or resizing actions.</param>

        private void OnImagePanelMouseMove(object sender, MouseEventArgs e)
        {
            // 1. حالت رسم مستطیل
            if (_isDragging && _currentSelectionMode == SelectionMode.Drawing)
            {
                _currentRect = new Rectangle(
                    Math.Min(_dragStart.X, e.X),
                    Math.Min(_dragStart.Y, e.Y),
                    Math.Abs(e.X - _dragStart.X),
                    Math.Abs(e.Y - _dragStart.Y)
                );
                _view.InvalidateImagePanel();
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

                // Only update if there's actual movement
                if (deltaX != 0 || deltaY != 0)
                {
                    var bounds = _selectedSprite.Bounds;
                    bounds.X += deltaX;
                    bounds.Y += deltaY;
                    _selectedSprite.Bounds = bounds;

                    _view.InvalidateImagePanel();
                    RefreshPropertyGrid();
                    _lastMousePosition = e.Location;
                    
                    // Update thumbnail in real-time as the sprite is being moved
                    var key = GetSpriteKey(_selectedSprite);
                    _ = Presenters.SpritePresenter.CreateOrUpdateThumbnailAsync(this, _selectedSprite, key);
                    
                    // Update list view item in real-time
                    UpdateListViewForSprite(_selectedSprite);
                }
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
                if (bounds.Width >= 5 && bounds.Height >= 5)
                {
                    _selectedSprite.Bounds = bounds;
                    _view.InvalidateImagePanel();
                    RefreshPropertyGrid();
                    _lastMousePosition = e.Location;
                    
                    // Update thumbnail in real-time as the sprite is being resized
                    var key = GetSpriteKey(_selectedSprite);
                    _ = Presenters.SpritePresenter.CreateOrUpdateThumbnailAsync(this, _selectedSprite, key);
                    
                    // Update list view item in real-time
                    UpdateListViewForSprite(_selectedSprite);
                }
            }
        }

        /// <summary>
        /// Handle mouse button release on the image panel; completes a drawing operation (creating a new sprite and executing an undoable add command) or finalizes a move/resize (updating thumbnail, list view, and status).</summary>
        /// <param name="sender">Event source (the image panel).</param>
        /// <param name="e">Mouse event data containing the release location and button state.</param>
        private void OnImagePanelMouseUp(object sender, MouseEventArgs e)
        {
            // پایان رسم مستطیل
            if (_isDragging && _currentSelectionMode == SelectionMode.Drawing)
            {
                _isDragging = false;

                if (_currentRect.Width > 5 && _currentRect.Height > 5)
                {
                    // ✅ استفاده از counter برای نام منحصربه‌فرد
                    var sprite = new SpriteDefinition
                    {
                        Name = $"Sprite_{_spriteCounter}",
                        Bounds = _currentRect
                    };

                    _spriteCounter++; // افزایش counter برای اسپرایت بعدی

                    // Create an undoable command for adding the sprite
                    var cmd = new Services.DelegateCommand(
                        execute: () => {
                            // Use the SpritePresenter helper so insertion logic is centralized
                            Presenters.SpritePresenter.InsertNewSprite(this, sprite);
                        },
                        undo: () => {
                            // Remove the sprite to undo the operation
                            Presenters.SpritePresenter.RemoveSprite(this, sprite);
                        },
                        description: $"Add '{sprite.Name}' sprite"
                    );

                    // Execute the command through the command manager to make it undoable
                    _commandManager.ExecuteCommand(cmd);
                    
                    // Switch to select mode and select the newly created sprite
                    _currentTool = "select";
                    UpdateSelectedSprite(sprite);
                }

                _currentRect = Rectangle.Empty;
                _view.InvalidateImagePanel();
            }

            // پایان حالت‌های Move و Resize
            if (_currentSelectionMode == SelectionMode.Moving || _currentSelectionMode == SelectionMode.Resizing)
            {
                _currentSelectionMode = SelectionMode.None;
                _activeResizeHandle = ResizeHandle.None;
                _view.ImagePanel.Cursor = Cursors.Default;
                if (_selectedSprite != null)
                // Update thumbnail for selected sprite via SpritePresenter (async)
                {
                    var key = GetSpriteKey(_selectedSprite);
                    _ = Presenters.SpritePresenter.CreateOrUpdateThumbnailAsync(this, _selectedSprite, key);
                    
                    // Also update the list view item with new position/size
                    UpdateListViewForSprite(_selectedSprite);
                }
                _view.UpdateStatus($"Sprite updated. Position: ({_selectedSprite.Bounds.X}, {_selectedSprite.Bounds.Y}), Size: {_selectedSprite.Bounds.Width}x{_selectedSprite.Bounds.Height}");
            }
        }

        /// <summary>
        /// Set the project's highlight color and refresh the image panel to apply the change.
        /// </summary>
        /// <param name="color">The new highlight color to use for sprite highlighting.</param>
        public void SetHighlightColor(Color color)
        {
            _project.Settings.HighlightColor = color;
            _view.InvalidateImagePanel(); // رندر مجدد برای اعمال رنگ جدید

            // ذخیره در تنظیمات کاربر (اختیاری)
            //Properties.Settings.Default.HighlightColor = color;
            //Properties.Settings.Default.Save();
        }

        public Color GetHighlightColor()
        {
            return _project.Settings.HighlightColor;
        }

        /// <summary>
        /// Renders the image panel contents: checkerboard background, loaded bitmap (preserving alpha), temporary drawing rectangle, resize handles for the selected sprite, and outlines/labels for visible sprites.
        /// </summary>
        /// <param name="sender">The control that raised the Paint event.</param>
        /// <param name="e">The PaintEventArgs containing the Graphics surface and clipping region used to draw the image panel.</param>
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
                    Matrix00 = 1,
                    Matrix01 = 0,
                    Matrix02 = 0,
                    Matrix03 = 0,
                    Matrix04 = 0,
                    Matrix10 = 0,
                    Matrix11 = 1,
                    Matrix12 = 0,
                    Matrix13 = 0,
                    Matrix14 = 0,
                    Matrix20 = 0,
                    Matrix21 = 0,
                    Matrix22 = 1,
                    Matrix23 = 0,
                    Matrix24 = 0,
                    Matrix30 = 0,
                    Matrix31 = 0,
                    Matrix32 = 0,
                    Matrix33 = 1,
                    Matrix34 = 0,
                    Matrix40 = 0,
                    Matrix41 = 0,
                    Matrix42 = 0,
                    Matrix43 = 0,
                    Matrix44 = 1
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
            if (_isDragging && _currentSelectionMode == SelectionMode.Drawing)
            {
                using var pen = new Pen(Color.Red, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
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

        /// <summary>
        /// Updates the sprite's row in the view's list to reflect the sprite's bounds and associated image key.
        /// </summary>
        /// <param name="sprite">The sprite whose list entry should be updated; if null or the view's list is unavailable, no action is taken.</param>
        private void UpdateListViewForSprite(SpriteDefinition sprite)
        {
            if (sprite == null || _view?.SpriteListView == null) return;

            foreach (ListViewItem item in _view.SpriteListView.Items)
            {
                if (item.Tag == sprite)
                {
                    // اطمینان از وجود حداقل 3 SubItem (index 0,1,2)
                    while (item.SubItems.Count < 3)
                        item.SubItems.Add(string.Empty);

                    item.SubItems[1].Text = $"{sprite.Bounds.X}, {sprite.Bounds.Y}";
                    item.SubItems[2].Text = $"{sprite.Bounds.Width}×{sprite.Bounds.Height}";
                    
                    // Update the image key to ensure thumbnail is properly linked
                    var spriteKey = GetSpriteKey(sprite);
                    item.ImageKey = spriteKey;
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

        /// <summary>
        /// Selects the mouse cursor appropriate for the given resize handle.
        /// </summary>
        /// <param name="handle">The resize handle whose corresponding cursor is required.</param>
        /// <returns>
        /// The cursor that represents the resize direction for the handle:
        /// `SizeNWSE` for top-left/bottom-right, `SizeNESW` for top-right/bottom-left,
        /// `SizeNS` for top/bottom, `SizeWE` for left/right, or the default cursor otherwise.
        /// </returns>
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

        // Thumbnail generation and management are delegated to Presenters.SpritePresenter
        /// <summary>
        /// Make the provided sprite the currently selected sprite (if selection is not suppressed and different from the current selection) and refresh the image panel.
        /// </summary>
        /// <param name="sprite">The sprite to select; may be null to clear the selection.</param>

        public void OnListViewItemSelected(SpriteDefinition sprite)
        {
            if (_suppressListSelectionChanged) return;
            if (sprite == _selectedSprite) return;
            UpdateSelectedSprite(sprite); // UpdateSelectedSprite باید null-safe باشد
            _view?.InvalidateImagePanel();
        }

        /// <summary>
        /// Cancels any in-progress sprite editing operation and resets the presenter's interaction state.
        /// </summary>
        /// <remarks>
        /// Resets the active tool to "select", clears the current sprite selection, cancels dragging and resize modes, stops property-grid monitoring, invalidates the image panel, and updates the status message to indicate cancellation.
        /// </remarks>
        public void CancelCurrentOperation()
        {
            // بازگشت به حالت ابزار پیش‌فرض
            SetToolMode("select");

            // پاک کردن انتخاب فعلی
            UpdateSelectedSprite(null);

            // ریست حالت‌های داخلی مرتبط با انتخاب/درگ
            _currentSelectionMode = SelectionMode.None;
            _activeResizeHandle = ResizeHandle.None;
            _isDragging = false;

            // توقف مانیتورینگ PropertyGrid اگر فعال است
            _propertyChangeTimer?.Stop();
            _isPropertyGridMonitoring = false;

            // رفرش نمای تصویر و وضعیت
            _view?.InvalidateImagePanel();
            _view?.UpdateStatus("Operation cancelled");
        }



        /// <summary>
        /// Sets the UI focus to the given sprite and updates the image panel and status to reflect the change.
        /// </summary>
        /// <param name="sprite">The sprite to focus; if null or already focused, the method does nothing.</param>
        public void FocusOnSprite(SpriteDefinition sprite)
        {
            if (sprite != null && sprite != _selectedSprite)
            {
                // از متد موجود UpdateSelectedSprite استفاده می‌کنیم
                UpdateSelectedSprite(sprite);

                // رندر مجدد برای اعمال هایلایت
                _view.InvalidateImagePanel();

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

        /// <summary>
        /// Creates a small two-by-two checkerboard bitmap used for painting a tiled transparency background.
        /// </summary>
        /// <param name="cellSize">The size, in pixels, of each square cell in the pattern (default is 10).</param>
        /// <returns>A Bitmap of size <c>cellSize*2</c> by <c>cellSize*2</c> containing alternating light and dark gray squares.</returns>
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
        /// <summary>
        /// Provides a stable identifier for a sprite, preferring the sprite's own Id when present.
        /// </summary>
        /// <param name="s">The sprite to obtain a key for; may be null.</param>
        /// <returns>The sprite's Id if present; otherwise a stable generated key associated with the sprite. Returns null if <paramref name="s"/> is null.</returns>
        public string GetSpriteKey(SpriteDefinition s)
        {
            if (s == null) return null;

            // ✅ اولویت اول: استفاده از Id که GUID منحصربه‌فرد است
            if (!string.IsNullOrEmpty(s.Id))
                return s.Id;

            if (_spriteKeys.TryGetValue(s, out var existing))
                return existing;

            var newId = Guid.NewGuid().ToString();
            _spriteKeys[s] = newId;
            return newId;
        }

        /// <summary>
        /// Refreshes thumbnails, updates the sprite list, and invalidates the image panel when undo/redo/clear operations occur.
        /// </summary>
        /// <remarks>
        /// When the operation is Undo, Redo, or Clear, requests a full thumbnail update via SpritePresenter, then updates the view's sprite list and invalidates the image panel to reflect the restored state.
        /// </remarks>

        private void OnCommandOperationPerformed(CommandManager.OperationType op)
        {
            if (op == CommandManager.OperationType.Undo ||
                op == CommandManager.OperationType.Redo ||
                op == CommandManager.OperationType.Clear)
            {
                // 1️⃣ بازسازی thumbnailها قبل از آپدیت لیست
                if (_project?.Sprites != null)
                {
                    _ = Presenters.SpritePresenter.UpdateAllThumbnailsAsync(this);
                }

                // 2️⃣ حالا لیست را با thumbnailهای آماده آپدیت کن
                _view?.UpdateSpriteList(_project?.Sprites ?? new List<SpriteDefinition>());

                // 3️⃣ پنل تصویر را Invalidate کن
                _view?.InvalidateImagePanel();
            }
        }

        private TextureBrush _checkerboardBrush = null;
    }
}
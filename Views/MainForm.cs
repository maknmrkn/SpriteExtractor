using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SpriteExtractor.Models;
using SpriteExtractor.Presenters;
using SpriteExtractor.Views;

namespace SpriteExtractor.Views
{
    public partial class MainForm : Form, IMainView
    {
        private MainPresenter _presenter;

        // کنترل‌های اصلی - با مقدار اولیه null! برای رفع CS8618
        public MenuStrip MainMenu { get; private set; } = null!;
        public ToolStrip Toolbar { get; private set; } = null!;
        public TabControl MainTabs { get; private set; } = null!;
        public Panel ImagePanel { get; private set; } = null!;
        public ListView SpriteListView { get; private set; } = null!;
        public PropertyGrid PropertyGrid { get; private set; } = null!;
        public StatusStrip StatusBar { get; private set; } = null!;
        public SpriteImageList SpriteThumbnails { get; private set; } // این خط را اضافه کنید
        private SpriteImageList _spriteImageList;
        public SpriteImageList SpriteImageList => _spriteImageList;


        public MainForm()
        {
            InitializeComponent();
            _presenter = new MainPresenter(this);

            // single/shared SpriteImageList instance for everything
            _spriteImageList = new SpriteImageList();
            SpriteThumbnails = _spriteImageList;               // make both references point to same object
            SpriteListView.SmallImageList = _spriteImageList.ImageList;

            SpriteListView.SelectedIndexChanged += OnListViewSelectionChanged;
            SpriteListView.MouseDoubleClick += OnListViewDoubleClick;
            this.KeyPreview = true;
            this.KeyDown += MainForm_KeyDown;


            // اطمینان از جزئیات نمایش و ستون‌ها
            SpriteListView.View = View.Details;
            if (SpriteListView.Columns.Count < 3)
            {
                SpriteListView.Columns.Clear();
                SpriteListView.Columns.Add("Name", 150);
                SpriteListView.Columns.Add("Position", 120);
                SpriteListView.Columns.Add("Size", 100);
            }






        }

        // IMainView implementations
        public void InvalidateImagePanel()
        {
            ImagePanel?.Invalidate();
        }

        public void BeginInvokeAction(Action action)
        {
            if (action == null) return;
            try
            {
                if (this.IsHandleCreated)
                    this.BeginInvoke((MethodInvoker)delegate { action(); });
                else
                    action();
            }
            catch
            {
                // ignore invocation failures
            }
        }

        public void UpdateSpriteThumbnail(string key, Image thumbnail)
        {
            if (string.IsNullOrEmpty(key) || _spriteImageList == null) return;
            _spriteImageList.AddOrUpdateThumbnail(key, thumbnail);
        }

        public void RemoveSpriteThumbnail(string key)
        {
            if (string.IsNullOrEmpty(key) || _spriteImageList == null) return;
            _spriteImageList.RemoveThumbnail(key);
        }

        public void ClearSpriteThumbnails()
        {
            _spriteImageList?.Clear();
            SpriteThumbnails?.Clear();
        }

        public void BeginUpdateSpriteList()
        {
            SpriteListView?.BeginUpdate();
        }

        public void EndUpdateSpriteList()
        {
            SpriteListView?.EndUpdate();
        }

        public void EnsureSpriteImageListAssigned()
        {
            if (SpriteListView != null && _spriteImageList != null)
                SpriteListView.SmallImageList = _spriteImageList.ImageList;
        }

        private void InitializeComponent()
        {

            // تنظیمات اصلی فرم
            this.Text = "Sprite Extractor - MVP";
            this.WindowState = FormWindowState.Maximized;

            // ایجاد منوی اصلی
            MainMenu = new MenuStrip();
            CreateMenuItems();
            this.Controls.Add(MainMenu);


            // ایجاد نوار ابزار
            Toolbar = new ToolStrip();
            CreateToolbarItems();
            this.Controls.Add(Toolbar);

            // ایجاد TabControl
            MainTabs = new TabControl { Dock = DockStyle.Fill, Top = 60 };
            CreateTabs();
            this.Controls.Add(MainTabs);

            // ایجاد StatusBar
            StatusBar = new StatusStrip { Dock = DockStyle.Bottom };
            StatusBar.Items.Add("Ready");
            this.Controls.Add(StatusBar);

        }

        private void CreateMenuItems()
        {
            var fileMenu = new ToolStripMenuItem("File");
            fileMenu.DropDownItems.Add("Open Image...", null, (s, e) => _presenter.OpenImage());
            fileMenu.DropDownItems.Add("Save Project...", null, (s, e) => _presenter.SaveProject());
            fileMenu.DropDownItems.Add("Load Project...", null, (s, e) => _presenter.LoadProject());
            fileMenu.DropDownItems.Add("-");
            fileMenu.DropDownItems.Add("Export Sprites...", null, (s, e) => _presenter.ExportSprites());
            fileMenu.DropDownItems.Add("-");
            fileMenu.DropDownItems.Add("Exit", null, (s, e) => this.Close());

            var editMenu = new ToolStripMenuItem("Edit");
            editMenu.DropDownItems.Add("Undo", null, (s, e) => _presenter.Undo());
            editMenu.DropDownItems.Add("Redo", null, (s, e) => _presenter.Redo());
            editMenu.DropDownItems.Add("-");
            editMenu.DropDownItems.Add("Delete Sprite", null, (s, e) => _presenter.DeleteSelectedSprite());


            var viewMenu = new ToolStripMenuItem("View");

            // زیرمنوی انتخاب رنگ هایلایت
            var highlightColorMenu = new ToolStripMenuItem("Highlight Color");

            // رنگ‌های پیش‌فرض
            var colors = new Dictionary<string, Color>
            {
                {"Orange", Color.Orange},
                {"Blue", Color.Blue},
                {"Red", Color.Red},
                {"Green", Color.Green},
                {"Purple", Color.Purple},
                {"Yellow", Color.Yellow}
            };

            foreach (var color in colors)
            {
                var item = new ToolStripMenuItem(color.Key, null, (s, e) =>
                {
                    _presenter?.SetHighlightColor(color.Value);
                    UpdateHighlightColorMenu(highlightColorMenu, color.Key);
                });

                highlightColorMenu.DropDownItems.Add(item);
            }

            // جداکننده
            highlightColorMenu.DropDownItems.Add(new ToolStripSeparator());

            // گزینه انتخاب رنگ دلخواه
            // گزینه انتخاب رنگ دلخواه (نسخه ساده‌شده)
            var customColorItem = new ToolStripMenuItem("Custom Color...", null, (s, e) =>
            {
                using var colorDialog = new ColorDialog
                {
                    Color = _presenter?.GetHighlightColor() ?? Color.Orange,
                    FullOpen = true
                };

                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    _presenter?.SetHighlightColor(colorDialog.Color);

                    // 🔧 فقط نام را به "Custom" تغییر دهید
                    UpdateHighlightColorMenu(highlightColorMenu, "Custom");
                }
            });

            highlightColorMenu.DropDownItems.Add(customColorItem);

            viewMenu.DropDownItems.Add(highlightColorMenu);
            MainMenu.Items.Add(viewMenu);
            var toolsMenu = new ToolStripMenuItem("Tools");
            toolsMenu.DropDownItems.Add("Auto-Detect Sprites", null, (s, e) => _presenter.AutoDetect());

            MainMenu.Items.AddRange(new[] { fileMenu, editMenu, viewMenu, toolsMenu });
            UpdateHighlightColorMenu(highlightColorMenu, "Orange");
        }

        private void CreateToolbarItems()
        {
            Toolbar.Items.Add(new ToolStripButton("Open", null, (s, e) => _presenter.OpenImage()));
            Toolbar.Items.Add(new ToolStripSeparator());
            Toolbar.Items.Add(new ToolStripButton("Select", null, (s, e) => _presenter.SetToolMode("select")));
            // Removed rectangle tool - now rectangle creation happens via drag on image panel
            Toolbar.Items.Add(new ToolStripSeparator());
            Toolbar.Items.Add(new ToolStripButton("Zoom In", null, (s, e) => _presenter.ZoomIn()));
            Toolbar.Items.Add(new ToolStripButton("Zoom Out", null, (s, e) => _presenter.ZoomOut()));
            Toolbar.Items.Add(new ToolStripButton("Fit to Screen", null, (s, e) => _presenter.ZoomFit()));
        }

        private void CreateTabs()
        {
            // تب ویرایش دستی
            var manualTab = new TabPage("Manual Editing");

            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 700
            };

            // پنل سمت چپ برای نمایش تصویر
            ImagePanel = new DoubleBufferedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.DarkGray,
                BorderStyle = BorderStyle.FixedSingle,
                AutoScroll = true
            };
            splitContainer.Panel1.Controls.Add(ImagePanel);

            // پنل سمت راست برای لیست و خصوصیات
            var rightPanel = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical
            };

            SpriteListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true
            };
            SpriteListView.Columns.Add("Name", 150);
            SpriteListView.Columns.Add("Position", 100);
            SpriteListView.Columns.Add("Size", 100);
            SpriteListView.SelectedIndexChanged += (s, e) => _presenter.OnSpriteSelected();
            rightPanel.Panel1.Controls.Add(SpriteListView);

            PropertyGrid = new PropertyGrid
            {
                Dock = DockStyle.Fill,
                ToolbarVisible = false
            };
            rightPanel.Panel2.Controls.Add(PropertyGrid);

            splitContainer.Panel2.Controls.Add(rightPanel);
            manualTab.Controls.Add(splitContainer);

            // تب ویرایش خودکار
            var autoTab = new TabPage("Auto Detection");
            // بعداً تکمیل می‌شود

            MainTabs.TabPages.AddRange(new[] { manualTab, autoTab });
        }

        private string GetSpriteKeyForView(SpriteDefinition s)
        {
            if (s == null) return null;

            // Use the presenter's GetSpriteKey method
            return _presenter?.GetSpriteKey(s);
        }

        public void UpdateSpriteList(List<SpriteDefinition> sprites)
        {
            if (SpriteListView == null) return;

            SpriteListView.BeginUpdate();
            SpriteListView.Items.Clear();

            // 🔴 خیلی مهم: مطمئن شو ImageList ست شده
            EnsureSpriteImageListAssigned();

            foreach (var sprite in sprites.Where(s => s.IsVisible))
            {
                var item = new ListViewItem(sprite.Name)
                {
                    Tag = sprite
                };

                // Use the sprite key for thumbnail assignment
                var spriteKey = GetSpriteKeyForView(sprite);
                if (!string.IsNullOrEmpty(spriteKey))
                {
                    item.ImageKey = spriteKey;
                    
                    // If the thumbnail doesn't exist in the image list yet, trigger its creation
                    if (_spriteImageList.GetImageIndex(spriteKey) == -1)
                    {
                        // Trigger thumbnail creation for this sprite if not already in the image list
                        // The thumbnail will be added asynchronously to the image list
                    }
                }

                item.SubItems.Add($"{sprite.Bounds.X}, {sprite.Bounds.Y}");
                item.SubItems.Add($"{sprite.Bounds.Width}×{sprite.Bounds.Height}");

                SpriteListView.Items.Add(item);
            }

            SpriteListView.EndUpdate();
            SpriteListView.Refresh();
        }


        public void UpdateStatus(string message)
        {
            if (StatusBar.Items.Count > 0)
                StatusBar.Items[0].Text = message;
        }

        // این متد جدید را اضافه کنید:
        public void ScrollToSprite(Rectangle spriteBounds)
        {
            if (ImagePanel == null || !ImagePanel.AutoScroll) return;

            try
            {
                // محاسبه موقعیت مرکز اسپرایت
                int centerX = spriteBounds.X + (spriteBounds.Width / 2);
                int centerY = spriteBounds.Y + (spriteBounds.Height / 2);

                // محاسبه offset برای اسکرول (مرکز کردن در viewport)
                int scrollX = centerX - (ImagePanel.ClientSize.Width / 2);
                int scrollY = centerY - (ImagePanel.ClientSize.Height / 2);

                // محدود کردن به محدوده‌های معتبر
                scrollX = Math.Max(0, scrollX);
                scrollY = Math.Max(0, scrollY);

                // اعمال اسکرول
                ImagePanel.AutoScrollPosition = new Point(scrollX, scrollY);
                ImagePanel.Invalidate(); // رندر مجدد
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Scroll error: {ex.Message}");
            }
        }

        private void OnListViewSelectionChanged(object sender, EventArgs e)
        {
            if (_presenter != null && _presenter.IsSuppressingListSelection) return;

            if (_presenter != null)
            {
                if (SpriteListView.SelectedItems.Count > 0)
                {
                    var sprite = SpriteListView.SelectedItems[0].Tag as SpriteDefinition;
                    _presenter.OnListViewItemSelected(sprite);
                }
                else
                {
                    _presenter.OnListViewItemSelected(null);
                }
            }
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                _presenter?.DeleteSelectedSprite();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                _presenter?.CancelCurrentOperation();
                e.Handled = true;
            }
            // داخل MainForm_KeyDown
            if (e.Control && e.KeyCode == Keys.Z)
            {
                _presenter?.Undo();
                e.Handled = true;
                return;
            }
            else if (e.Control && e.KeyCode == Keys.Y)
            {
                _presenter?.Redo();
                e.Handled = true;
                return;
            }

        }



        // متد کمکی برای آپدیت تیک کنار رنگ انتخاب‌شده
        private void UpdateHighlightColorMenu(ToolStripMenuItem menu, string selectedColorName)
        {
            foreach (var item in menu.DropDownItems)
            {
                // 🔧 فقط آیتم‌هایی که ToolStripMenuItem هستند را بررسی کن
                if (item is ToolStripMenuItem menuItem)
                {
                    // آیتم‌هایی که متن آنها "Custom Color..." نیست را چک کن
                    if (menuItem.Text != "Custom Color...")
                    {
                        menuItem.Checked = (menuItem.Text == selectedColorName);
                    }
                }
                // ToolStripSeparator را نادیده بگیر
            }
        }

        private void OnListViewDoubleClick(object sender, MouseEventArgs e)
        {
            var item = SpriteListView.GetItemAt(e.X, e.Y);
            if (item != null)
            {
                var sprite = item.Tag as SpriteDefinition;
                _presenter?.FocusOnSprite(sprite);

                // اسکرول خودکار به موقعیت اسپرایت در پنل
                // (نیاز به محاسبات Viewport دارد)
            }
        }
        // در MainForm، رویداد FormClosing را هندل کنید
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _presenter?.Cleanup();
            base.OnFormClosing(e);
        }

        // UI automation helper for smoke-testing: performs open-image (via direct bitmap set), draw/insert, move, delete, undo
        public async System.Threading.Tasks.Task RunUiAutomationAsync()
        {
            try
            {
                Console.WriteLine("UI smoke: start");

                // 1) load image from TestAssets if available, otherwise create dummy
                var testPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "TestAssets", "test.png");
                System.Drawing.Bitmap bmp;
                if (System.IO.File.Exists(testPath))
                {
                    bmp = new Bitmap(testPath);
                    Console.WriteLine($"UI smoke: loaded image {testPath}");
                }
                else
                {
                    bmp = new Bitmap(200, 200);
                    using (var g = Graphics.FromImage(bmp)) g.Clear(Color.Magenta);
                    Console.WriteLine("UI smoke: created dummy bitmap");
                }

                // set presenter's private _loadedBitmap via reflection
                var fld = typeof(SpriteExtractor.Presenters.MainPresenter).GetField("_loadedBitmap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                fld?.SetValue(_presenter, bmp);
                // set project source path
                var projFld = typeof(SpriteExtractor.Presenters.MainPresenter).GetField("_project", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var proj = projFld?.GetValue(_presenter) as SpriteExtractor.Models.SpriteProject;
                if (proj != null) proj.SourceImagePath = testPath ?? "(in-memory)";

                // 2) draw/insert sprite (simulate rectangle creation)
                var sprite = new SpriteExtractor.Models.SpriteDefinition { Name = "UI_Smoke", Bounds = new Rectangle(10, 10, 48, 48) };

                // Create an undoable insert using CommandManager
                var cmd = new SpriteExtractor.Services.DelegateCommand(
                    execute: () => _presenter.InsertSpriteInternal(sprite, _presenter.Project?.Sprites?.Count ?? 0),
                    undo: () => _presenter.RemoveSpriteInternal(sprite),
                    description: "UI smoke insert"
                );

                // execute on UI thread
                _presenter.CommandManager.ExecuteCommand(cmd);
                Console.WriteLine("UI smoke: inserted sprite via command");

                // allow async thumbnail generation to run
                await System.Threading.Tasks.Task.Delay(300);

                // 3) move sprite
                sprite.Bounds = new Rectangle(20, 20, 48, 48);
                var key = (string)typeof(SpriteExtractor.Presenters.MainPresenter).GetMethod("GetSpriteKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(_presenter, new object[] { sprite });
                await SpriteExtractor.Presenters.SpritePresenter.CreateOrUpdateThumbnailAsync(_presenter, sprite, key).ConfigureAwait(false);
                Console.WriteLine("UI smoke: moved sprite and updated thumbnail");

                // 4) delete sprite via command
                var delCmd = new SpriteExtractor.Services.DelegateCommand(
                    execute: () => _presenter.RemoveSpriteInternal(sprite),
                    undo: () => _presenter.InsertSpriteInternal(sprite, _presenter.Project?.Sprites?.Count ?? 0),
                    description: "UI smoke delete"
                );
                _presenter.CommandManager.ExecuteCommand(delCmd);
                Console.WriteLine("UI smoke: deleted sprite");

                await System.Threading.Tasks.Task.Delay(200);

                // 5) Undo delete (should restore)
                _presenter.CommandManager.Undo();
                Console.WriteLine("UI smoke: undo performed");

                await System.Threading.Tasks.Task.Delay(200);

                Console.WriteLine("UI smoke: finished");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"UI smoke error: {ex}");
            }
        }



    }
}
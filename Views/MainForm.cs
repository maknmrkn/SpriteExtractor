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
    public partial class MainForm : Form
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
        
        public MainForm()
        {
            InitializeComponent();
            _presenter = new MainPresenter(this);
        SpriteThumbnails = new SpriteImageList();
        SpriteListView.SmallImageList = SpriteThumbnails.ImageList;
        SpriteListView.SelectedIndexChanged += OnListViewSelectionChanged;
        SpriteListView.MouseDoubleClick += OnListViewDoubleClick;

        
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
            Toolbar.Items.Add(new ToolStripButton("Rectangle", null, (s, e) => _presenter.SetToolMode("rectangle")));
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
                BorderStyle = BorderStyle.FixedSingle
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
        
        public void UpdateSpriteList(List<SpriteDefinition> sprites)
        {
            SpriteListView.Items.Clear();
            
            foreach (var sprite in sprites.Where(s => s.IsVisible))
            {
                var item = new ListViewItem(sprite.Name)
                {
                    Tag = sprite,
                    // 🔧 این خط حیاتی را اضافه کنید:
                    ImageIndex = SpriteThumbnails?.GetImageIndex(sprite.Id) ?? -1
                };
                
                item.SubItems.Add($"{sprite.Bounds.X}, {sprite.Bounds.Y}");
                item.SubItems.Add($"{sprite.Bounds.Width}×{sprite.Bounds.Height}");
                SpriteListView.Items.Add(item);
            }
        }
        
        public void UpdateStatus(string message)
        {
            if (StatusBar.Items.Count > 0)
                StatusBar.Items[0].Text = message;
        }

                private void OnListViewSelectionChanged(object sender, EventArgs e)
        {
            // به Presenter اطلاع بده که انتخاب در لیست تغییر کرده
            if (_presenter != null && SpriteListView.SelectedItems.Count > 0)
            {
                var sprite = SpriteListView.SelectedItems[0].Tag as SpriteDefinition;
                _presenter.OnListViewItemSelected(sprite);
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

      
    }
}
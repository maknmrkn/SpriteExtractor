using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SpriteExtractor.Models;
using SpriteExtractor.Presenters;

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
        public SpriteImageList SpriteThumbnails { get; private set; }
        
        public MainForm()
        {
            InitializeComponent();
            _presenter = new MainPresenter(this);
                    // این خط را اضافه کنید:
        SpriteThumbnails = new SpriteImageList();
        SpriteListView.SmallImageList = SpriteThumbnails.ImageList;
        
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
            var toolsMenu = new ToolStripMenuItem("Tools");
            toolsMenu.DropDownItems.Add("Auto-Detect Sprites", null, (s, e) => _presenter.AutoDetect());
            
            MainMenu.Items.AddRange(new[] { fileMenu, editMenu, viewMenu, toolsMenu });
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
        
        // متدهای کمکی برای Presenter
public void UpdateSpriteList(List<SpriteDefinition> sprites)
{
    SpriteListView.Items.Clear();
    
    foreach (var sprite in sprites.Where(s => s.IsVisible))
    {
        var item = new ListViewItem(sprite.Name)
        {
            Tag = sprite,
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
    }
}
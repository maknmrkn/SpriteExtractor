using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using SpriteExtractor.Presenters;
using SpriteExtractor.Views;
using SpriteExtractor.Models;

public static class SmokeTest
{
    public static void Run()
    {
        Console.WriteLine("Starting smoke test...");
        // Create a headless MainForm replacement that implements IMainView minimally
        var fakeView = new HeadlessView();
        var presenter = new SpriteExtractor.Presenters.MainPresenter(fakeView);

        // Load a known image from project folder if exists
        var imgPath = Path.Combine(Directory.GetCurrentDirectory(), "TestAssets", "test.png");
        if (File.Exists(imgPath))
        {
            Console.WriteLine($"Loading image: {imgPath}");
            // use reflection to set loaded bitmap (since LoadImageWithTransparency is private)
            var bmp = new Bitmap(imgPath);
            var fld = typeof(SpriteExtractor.Presenters.MainPresenter).GetField("_loadedBitmap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            fld?.SetValue(presenter, bmp);
            var projFld = typeof(SpriteExtractor.Presenters.MainPresenter).GetField("_project", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var proj = projFld?.GetValue(presenter) as SpriteProject;
            if (proj != null) proj.SourceImagePath = imgPath;
        }
        else
        {
            Console.WriteLine("No TestAssets/test.png found — creating dummy bitmap.");
            var bmp = new Bitmap(200, 200);
            using (var g = Graphics.FromImage(bmp)) g.Clear(Color.Magenta);
            var fld = typeof(SpriteExtractor.Presenters.MainPresenter).GetField("_loadedBitmap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            fld?.SetValue(presenter, bmp);
            var projFld = typeof(SpriteExtractor.Presenters.MainPresenter).GetField("_project", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var proj = projFld?.GetValue(presenter) as SpriteProject;
            if (proj != null) proj.SourceImagePath = "(in-memory)";
        }

        // Add a sprite and create thumbnail
        var sprite = new SpriteDefinition { Name = "SmokeSprite", Bounds = new Rectangle(10, 10, 64, 64) };
        SpritePresenter.InsertNewSprite(presenter, sprite);
        var key = typeof(SpriteExtractor.Presenters.MainPresenter).GetMethod("GetSpriteKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(presenter, new object[] { sprite }) as string;
        Console.WriteLine($"Created sprite with key: {key}");

        var t = SpritePresenter.CreateOrUpdateThumbnailAsync(presenter, sprite, key);
        t.Wait();
        Console.WriteLine("Thumbnail generation finished.");

        // Move sprite
        sprite.Bounds = new Rectangle(20, 20, 64, 64);
        var moveKey = typeof(SpriteExtractor.Presenters.MainPresenter).GetMethod("GetSpriteKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(presenter, new object[] { sprite }) as string;
        var tu = SpritePresenter.CreateOrUpdateThumbnailAsync(presenter, sprite, moveKey);
        tu.Wait();
        Console.WriteLine("Thumbnail updated after move.");

        // Delete sprite
        presenter.RemoveSpriteInternal(sprite);
        Console.WriteLine("Sprite removed.");

        // Undo/Redo not exercised (requires command stack wiring in this headless mode)
        Console.WriteLine("Smoke test complete.");
    }
}

class HeadlessView : SpriteExtractor.Views.IMainView
{
    private readonly System.Windows.Forms.Panel _imagePanel = new System.Windows.Forms.Panel();
    private readonly System.Windows.Forms.ListView _listView = new System.Windows.Forms.ListView();
    private readonly System.Windows.Forms.PropertyGrid _propGrid = new System.Windows.Forms.PropertyGrid();
    public System.Windows.Forms.Panel ImagePanel => _imagePanel;
    public System.Windows.Forms.ListView SpriteListView => _listView;
    public System.Windows.Forms.PropertyGrid PropertyGrid => _propGrid;
    public SpriteExtractor.Views.SpriteImageList SpriteThumbnails { get; } = new SpriteExtractor.Views.SpriteImageList();
    public SpriteExtractor.Views.SpriteImageList SpriteImageList => SpriteThumbnails;
    public object BeginInvoke(Delegate method) => null;
    public System.Windows.Forms.MenuStrip MainMenu => null;
    public System.Windows.Forms.StatusStrip StatusBar => null;
    public System.Windows.Forms.TabControl MainTabs => null;
    public System.Windows.Forms.ToolStrip Toolbar => null;
    public void BeginInvokeAction(Action action) { action?.Invoke(); }
    public void ClearSpriteThumbnails() { SpriteThumbnails.Clear(); }
    public void EndUpdateSpriteList() { }
    public void EnsureSpriteImageListAssigned() { }
    public void InvalidateImagePanel() { }
    public void UpdateSpriteList(System.Collections.Generic.List<SpriteDefinition> sprites) { }
    public void UpdateSpriteThumbnail(string key, Image thumbnail) { SpriteThumbnails.AddOrUpdateThumbnail(key, thumbnail); }
    public void RemoveSpriteThumbnail(string key) { SpriteThumbnails.RemoveThumbnail(key); }
    public void BeginUpdateSpriteList() { }
    public void UpdateStatus(string message) => Console.WriteLine($"Status: {message}");
    public void ScrollToSprite(Rectangle spriteBounds) { }
}

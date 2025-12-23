using System.Windows.Forms;

namespace SpriteExtractor.Views
{
    public class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            // فعال‌سازی Double Buffering برای حذف لرزش
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.ResizeRedraw |
                         ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();
        }
    }
}
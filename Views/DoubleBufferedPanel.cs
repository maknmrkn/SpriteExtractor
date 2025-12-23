using System.Collections.Generic;
using System.Drawing;
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
    // کلاس برای مدیریت Thumbnail‌ها
    public class SpriteImageList
    {
        private ImageList _imageList;
        private Dictionary<string, int> _spriteToIndexMap;
        private int _currentIndex = 0;
        
        public SpriteImageList()
        {
            _imageList = new ImageList
            {
                ColorDepth = ColorDepth.Depth32Bit,
                ImageSize = new Size(48, 48) // اندازه ثابت برای همه Thumbnailها
            };
            
            _spriteToIndexMap = new Dictionary<string, int>();
        }
        
        public ImageList ImageList => _imageList;
        
        public void AddOrUpdateThumbnail(string spriteId, Image thumbnail)
        {
            // اگر Thumbnail قبلاً وجود دارد، آپدیت کن
            if (_spriteToIndexMap.ContainsKey(spriteId))
            {
                int index = _spriteToIndexMap[spriteId];
                _imageList.Images[index] = thumbnail;
            }
            // اگر جدید است، اضافه کن
            else
            {
                _imageList.Images.Add(thumbnail);
                _spriteToIndexMap[spriteId] = _currentIndex;
                _currentIndex++;
            }
        }
        
        public void RemoveThumbnail(string spriteId)
        {
            if (_spriteToIndexMap.ContainsKey(spriteId))
            {
                _spriteToIndexMap.Remove(spriteId);
                // در ImageList واقعاً حذف نمی‌کنیم (Indexها جابجا می‌شوند)
                // فقط از دیکشنری حذف می‌کنیم
            }
        }
        
        public int GetImageIndex(string spriteId)
        {
            return _spriteToIndexMap.ContainsKey(spriteId) ? 
                _spriteToIndexMap[spriteId] : -1;
        }
        
        public void Clear()
        {
            _imageList.Images.Clear();
            _spriteToIndexMap.Clear();
            _currentIndex = 0;
        }
    }
    
}
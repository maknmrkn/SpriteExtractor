using System.Drawing;
using System.Windows.Forms;

namespace SpriteExtractor.Views
{
    public class SpriteImageList
    {
        private readonly ImageList _imageList;

        public SpriteImageList()
        {
            _imageList = new ImageList
            {
                ColorDepth = ColorDepth.Depth32Bit,
                ImageSize = new Size(48, 48)
            };
        }

        public ImageList ImageList => _imageList;

        public void AddOrUpdateThumbnail(string spriteId, Image thumbnail)
        {
            if (string.IsNullOrEmpty(spriteId) || thumbnail == null) return;

            // مقیاس کردن تصویر به اندازهٔ ImageList برای جلوگیری از مشکلات نمایش
            Image toAdd = thumbnail;
            if (thumbnail.Size != _imageList.ImageSize)
            {
                toAdd = new Bitmap(thumbnail, _imageList.ImageSize);
            }

            int existingIndex = _imageList.Images.IndexOfKey(spriteId);
            if (existingIndex >= 0)
                _imageList.Images.RemoveByKey(spriteId);

            _imageList.Images.Add(spriteId, toAdd);
        }

        public void RemoveThumbnail(string spriteId)
        {
            if (string.IsNullOrEmpty(spriteId)) return;
            int idx = _imageList.Images.IndexOfKey(spriteId);
            if (idx >= 0) _imageList.Images.RemoveByKey(spriteId);
        }

        public int GetImageIndex(string spriteId)
        {
            if (string.IsNullOrEmpty(spriteId)) return -1;
            return _imageList.Images.IndexOfKey(spriteId);
        }

        public void Clear()
        {
            _imageList.Images.Clear();
        }
    }
}

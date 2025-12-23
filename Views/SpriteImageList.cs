using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SpriteExtractor.Views
{
    public class SpriteImageList
    {
        private ImageList _imageList;
        private Dictionary<string, int> _spriteToIndexMap;
        
        public SpriteImageList()
        {
            _imageList = new ImageList
            {
                ColorDepth = ColorDepth.Depth32Bit,
                ImageSize = new Size(48, 48)
            };
            _spriteToIndexMap = new Dictionary<string, int>();
        }
        
        public ImageList ImageList => _imageList;
        
        public void AddOrUpdateThumbnail(string spriteId, Image thumbnail)
        {
            if (_spriteToIndexMap.ContainsKey(spriteId))
            {
                _imageList.Images[_spriteToIndexMap[spriteId]] = thumbnail;
            }
            else
            {
                _spriteToIndexMap[spriteId] = _imageList.Images.Count;
                _imageList.Images.Add(thumbnail);
            }
        }
        
        public void RemoveThumbnail(string spriteId)
        {
            if (_spriteToIndexMap.ContainsKey(spriteId))
            {
                _spriteToIndexMap.Remove(spriteId);
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
        }
    }
}
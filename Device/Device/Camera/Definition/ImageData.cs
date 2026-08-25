using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Device.Camera.Definition
{
    /// <summary>
    /// 图像数据
    /// </summary>
    public struct ImageData
    {
        /// <summary>
        /// 图像宽度
        /// </summary>
        public int Width { get; set; }
        /// <summary>
        /// 图像高度
        /// </summary>
        public int Height { get; set; }
        /// <summary>
        /// 图像原始数据（托管内存，GC自动回收，无需手动释放）
        /// </summary>
        public byte[] RawData { get; set; }
        /// <summary>
        /// 图像输出类型
        /// </summary>
        public EImageFormat ImageType { get; set; }
    }

    /// <summary>
    /// 图像格式
    /// </summary>
    public enum EImageFormat
    {
        Gray8,
        Gray16,
        Gray24,
        Gray32,
        Color24,
    }
}
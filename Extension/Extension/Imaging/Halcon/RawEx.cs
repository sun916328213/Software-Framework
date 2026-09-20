using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Imaging.Halcon
{
    public static  class RawEx
    {
        /// <summary>
        /// 将原始字节数据转换为Bitmap图像。
        /// </summary>
        /// <param name="pRawData">原始字节数据。</param>
        /// <param name="pWidth">图像宽度。</param>
        /// <param name="pHeight">图像高度。</param>
        /// <returns>包含Bitmap对象的SResult，包括操作结果和可能的错误信息。</returns>
        public static Bitmap ToBitmap(this byte[] pRawData, int pWidth, int pHeight)
        {
            // 创建一个具有指定宽度、高度和像素格式的Bitmap对象
            Bitmap bitmap = new Bitmap(pWidth, pHeight, PixelFormat.Format8bppIndexed);   

            // 设置灰度调色板
            ColorPalette palette = bitmap.Palette;
            for (int i = 0; i < 256; i++)
                palette.Entries[i] = Color.FromArgb(i, i, i);
            bitmap.Palette = palette;

            // 锁定内存区域直接操作
            BitmapData data = bitmap.LockBits(
                new Rectangle(0, 0, pWidth, pHeight),
                ImageLockMode.WriteOnly,
                bitmap.PixelFormat);  // ‌:ml-citation{ref="8" data="citationList"}

            // 复制RAW数据到内存指针
            Marshal.Copy(pRawData, 0, data.Scan0, pWidth * pHeight);   
            bitmap.UnlockBits(data);
            return bitmap;
        }

        /// <summary>
        /// 将原始字节数据转换为HImage图像，自动根据数据长度识别位深。
        /// </summary>
        /// <param name="pRawData">原始字节数据。</param>
        /// <param name="pWidth">图像宽度。</param>
        /// <param name="pHeight">图像高度。</param>
        /// <returns>HObject图像</returns>
        public static HObject ToHImage(this byte[] pRawData, int pWidth, int pHeight)
        {
            HObject image;

            // 根据字节数自动识别Halcon图像类型
            int bytesPerPixel = pRawData.Length / (pWidth * pHeight);
            string imageType = bytesPerPixel switch
            {
                1 => "byte",      // 8位灰度
                2 => "uint2",     // 16位灰度
                4 => "real",      // 32位浮点
                _ => "byte"
            };

            // 分配非托管内存
            IntPtr ptr = Marshal.AllocHGlobal(pRawData.Length);
            Marshal.Copy(pRawData, 0, ptr, pRawData.Length);

            // 生成HObject图像（GenImage1内部会拷贝数据，可安全释放ptr）
            HOperatorSet.GenImage1(out image, imageType, pWidth, pHeight, ptr);
            Marshal.FreeHGlobal(ptr);
            return image;
        }
    }
}

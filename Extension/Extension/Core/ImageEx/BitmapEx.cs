using HalconDotNet;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Extension.Core.ImageEx
{
    public static class BitmapEx
    {
        /// <summary>
        /// Bitmap图像转Halcon HObject
        /// </summary>
        /// <param name="bitmap">Bitmap</param>
        /// <returns></returns>
        public static HObject ToHImage(this Bitmap bitmap)
        {
            HObject HImage;
            try
            {
                System.Drawing.Rectangle rect = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
                BitmapData srcBmpData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, bitmap.PixelFormat);
                if (bitmap.PixelFormat == System.Drawing.Imaging.PixelFormat.Format8bppIndexed)
                {
                    HImage = srcBmpData.Scan0.ToHImageByte(  bitmap.Width, bitmap.Height);
                }
                else
                {
                    HImage= srcBmpData.Scan0.ToHImageRGB(bitmap.Width, bitmap.Height);
                }
                bitmap.UnlockBits(srcBmpData);
                return HImage;
            }
            catch { return null; }
        }

        public static ImageSource ToImageSource(this Bitmap bitmap)
        {
            try
            {
                ImageSource ImageSource = Imaging.CreateBitmapSourceFromHBitmap(bitmap.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                return ImageSource;
            }
            catch { return null; }
        }
    }
}

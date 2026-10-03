// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：BitmapEx。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：HalconDotNet、System、System.ComponentModel、System.Diagnostics、System.Drawing、System.Drawing.Imaging。导入不代表所有类型都会在运行时加载。
using HalconDotNet;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Extension.Imaging.Halcon
{
    /// <summary>
    /// BitmapEx 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
    /// </summary>
    public static class BitmapEx
    {
        /// <summary>
        /// Bitmap图像转Halcon HObject
        /// </summary>
        /// <param name="bitmap">待转换的位图对象</param>
        /// <returns></returns>
        /// <summary>
        /// Bitmap 转 Halcon HObject。转换失败返回 <c>null</c>，调用方需判空。
        /// </summary>
        public static HObject? ToHImage(this Bitmap bitmap)
        {
            BitmapData? srcBmpData = null;
            try
            {
                System.Drawing.Rectangle rect = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
                srcBmpData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, bitmap.PixelFormat);

                return bitmap.PixelFormat == System.Drawing.Imaging.PixelFormat.Format8bppIndexed
                    ? srcBmpData.Scan0.ToHImageByte(bitmap.Width, bitmap.Height)
                    : srcBmpData.Scan0.ToHImageRGB(bitmap.Width, bitmap.Height);
            }
            catch (Exception ex) when (ex is ArgumentException
                                          or InvalidOperationException
                                          or ExternalException)
            {
                // 转换失败时返回 null 由调用方判空，但这里必须留痕：
                // 否则"图转不出来"会被误当成"算法结果为空"，排查方向完全跑偏。
                Debug.WriteLine($"[BitmapEx] Bitmap 转 HObject 失败：{ex.Message}");
                return null;
            }
            finally
            {
                // LockBits 后必须 UnlockBits。原实现把 UnlockBits 放在 try 末尾，
                // 一旦中途抛异常就永远解锁不了，该 Bitmap 后续任何绘制/保存都会失败。
                if (srcBmpData != null)
                    bitmap.UnlockBits(srcBmpData);
            }
        }

        /// <summary>
        /// Bitmap 转 WPF ImageSource。转换失败返回 <c>null</c>，调用方需判空。
        /// </summary>
        public static ImageSource? ToImageSource(this Bitmap bitmap)
        {
            IntPtr hBitmap = IntPtr.Zero;
            try
            {
                hBitmap = bitmap.GetHbitmap();
                return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            catch (Exception ex) when (ex is Win32Exception
                                          or ArgumentException
                                          or InvalidOperationException
                                          or OutOfMemoryException)
            {
                Debug.WriteLine($"[BitmapEx] Bitmap 转 ImageSource 失败：{ex.Message}");
                return null;
            }
            finally
            {
                // GetHbitmap() 返回的 HBITMAP 必须由调用方调用 DeleteObject 释放。
                // 原实现从不释放，每转换一次就泄漏一个 GDI 句柄（单进程上限约 10000），
                // 长时间运行的视觉程序会因此出现界面绘制全面失效。
                if (hBitmap != IntPtr.Zero)
                    DeleteObject(hBitmap);
            }
        }

        /// <summary>
        /// 执行删除操作；对应入口为 DeleteObject。
        /// </summary>
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}

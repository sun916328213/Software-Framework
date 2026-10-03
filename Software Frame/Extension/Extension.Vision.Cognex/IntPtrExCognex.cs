// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：IntPtrExCognex。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Extension.Imaging.Halcon、Cognex.VisionPro、System。导入不代表所有类型都会在运行时加载。
using Extension.Imaging.Halcon;
using Cognex.VisionPro;
using System;

namespace Extension.Vision.Cognex.Imaging.Halcon.Cognex
{
    /// <summary>
    /// IntPtrExCognex 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
    /// </summary>
    public static class IntPtrExCognex
    {
        /// <summary>
        /// 指针转换成CogImage8Grey
        /// </summary>
        /// <param name="intPtr">指针数据</param>
        /// <param name="width">图像宽度</param>
        /// <param name="height">图像高度</param>
        /// <returns></returns>
        public static ICogImage ToCogImage8Grey(this IntPtr intPtr, int width, int height)
        {
            ICogImage image = null;

            CogImage8Root cogImage8Root = new CogImage8Root();
            cogImage8Root.Initialize(width, height, intPtr, width, null);

            CogImage8Grey cogImage8Grey = new CogImage8Grey();
            cogImage8Grey.SetRoot(cogImage8Root);
            image = cogImage8Grey.ScaleImage(width, height);

            return image;
        }

        /// <summary>
        /// 指针转换成CogImage8Grey
        /// </summary>
        /// <param name="intPtr">指针数据</param>
        /// <param name="width">图像宽度</param>
        /// <param name="height">图像高度</param>
        /// <param name="imageFormat">图像格式 EImageFormat</param>
        /// <returns></returns>
        public static ICogImage ToCogImage24PlanarColor(this IntPtr intPtr, int width, int height)
        {
            ICogImage image = null;

            int m_nRowStep = width * height;
            CogImage8Root image0 = new CogImage8Root();
            IntPtr ptr0 = new IntPtr(intPtr.ToInt64());
            image0.Initialize((int)width, (int)height, ptr0, (int)width, null);

            CogImage8Root image1 = new CogImage8Root();
            IntPtr ptr1 = new IntPtr(intPtr.ToInt64() + m_nRowStep);
            image1.Initialize((int)width, (int)height, ptr1, (int)width, null);

            CogImage8Root image2 = new CogImage8Root();
            IntPtr ptr2 = new IntPtr(intPtr.ToInt64() + m_nRowStep * 2);
            image2.Initialize((int)width, (int)height, ptr2, (int)width, null);

            CogImage24PlanarColor colorImage = new CogImage24PlanarColor();
            colorImage.SetRoots(image0, image1, image2);

            image = colorImage.ScaleImage(width, height); ;

            return image;
        }
    }
}

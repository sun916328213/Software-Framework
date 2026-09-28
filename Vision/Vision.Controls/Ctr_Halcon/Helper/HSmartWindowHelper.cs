using HalconDotNet;
using System.Collections.Generic;
using System.Drawing;

namespace Vision.Controls.Ctr_Halcon.Helper
{
    /// <summary>
    /// HSmartWindowControlWPF 显示扩展方法
    /// </summary>
    public static class HSmartWindowHelper
    {
        /// <summary>
        /// 显示控件初始化
        /// </summary>
        /// <param name="hSmartWindow">显示图像控件</param>
        public static void Init(this HSmartWindowControlWPF hSmartWindow)
        {
            hSmartWindow.SetFullImagePart();
        }

        /// <summary>
        /// 显示图片
        /// </summary>
        /// <param name="hSmartWindow">显示图像控件</param>
        /// <param name="pImage">图片</param>
        public static void ShowImage(this HSmartWindowControlWPF hSmartWindow, HObject pImage)
        {
            hSmartWindow.HalconWindow.ClearWindow();
            if (pImage != null)
            {
                hSmartWindow.HalconWindow.DispObj(pImage);
            }
        }

        /// <summary>
        /// 清除控件上所有信息
        /// </summary>
        /// <param name="hSmartWindow">显示图像控件</param>
        public static void Clear(this HSmartWindowControlWPF hSmartWindow)
        {
            hSmartWindow.HalconWindow.ClearWindow();
        }

        /// <summary>
        /// 适应窗口
        /// </summary>
        /// <param name="hSmartWindow">显示图像控件</param>
        public static void Fit(this HSmartWindowControlWPF hSmartWindow)
        {
            HOperatorSet.SetPart(hSmartWindow.HalconWindow, 0, 0, -2, -2);
        }

        /// <summary>
        /// 显示矩形
        /// </summary>
        /// <param name="hSmartWindow">显示图像控件</param>
        /// <param name="pRectangle">矩形信息</param>
        /// <param name="pColor">矩形颜色</param>
        public static void ShowRectangle(this HSmartWindowControlWPF hSmartWindow, List<Rectangle> pRectangle, string pColor = "green")
        {
            hSmartWindow.HalconWindow.SetLineWidth(2);
            hSmartWindow.HalconWindow.SetColor(pColor);

            for (int i = 0; i < pRectangle.Count; i++)
            {
                HObject region, roi;
                Rectangle rectangle = pRectangle[i];
                HOperatorSet.GenRectangle1(out region, rectangle.Y, rectangle.X, rectangle.Y + rectangle.Height, rectangle.X + rectangle.Width);

                HOperatorSet.GenContourRegionXld(region, out roi, "border");
                hSmartWindow.HalconWindow.DispObj(roi);
            }
        }

        /// <summary>
        /// 显示文字
        /// </summary>
        /// <param name="hSmartWindow">显示图像控件</param>
        /// <param name="text">文字信息</param>
        /// <param name="col">x坐标</param>
        /// <param name="row">y坐标</param>
        /// <param name="pColor">颜色</param>
        /// <param name="size">字体大小</param>
        public static void ShowText(this HSmartWindowControlWPF hSmartWindow, string text, int col, int row, string pColor = "blue", int size = 20)
        {
            hSmartWindow.HalconWindow.SetFont("Arial-Normal-" + size.ToString());
            hSmartWindow.HalconWindow.SetColor(pColor);
            hSmartWindow.HalconWindow.DispText(text, "image", row, col, pColor, new HTuple(), new HTuple());
        }

        /// <summary>
        /// 显示十字线
        /// </summary>
        /// <param name="hSmartWindow">显示图像控件</param>
        /// <param name="image">图像</param>
        /// <param name="pColor">颜色</param>
        public static void ShowCross(this HSmartWindowControlWPF hSmartWindow, HObject image, string pColor = "red")
        {
            HTuple w, h;
            HOperatorSet.GetImageSize(image, out w, out h);
            HOperatorSet.SetColor(hSmartWindow.HalconWindow, pColor);
            HOperatorSet.DispLine(hSmartWindow.HalconWindow, 0, w / 2, h, w / 2);
            HOperatorSet.DispLine(hSmartWindow.HalconWindow, h / 2, 0, h / 2, w);
        }
    }
}

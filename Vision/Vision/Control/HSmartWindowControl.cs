
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Controls;
using System.Windows.Forms;

namespace LabelInspection
{
    /// <summary>
    /// 
    /// </summary>
   
    public partial class HSmartWindowControl : System.Windows.Forms.UserControl
    {
        /// <summary>
        /// 颜色枚举
        /// </summary>
        public enum EHColor
        {
            red,
            green,
            blue,
            orange,
            yellow,
            navy,
        }

        HObject InputImage;

        public HSmartWindowControl()
        {
            InitializeComponent();
            hSmartWindowControl1.MouseWheel += new System.Windows.Forms.MouseEventHandler(hSmartWindowControl1_MouseWheel);
        }

        /// <summary>
        /// 鼠标滚轮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void hSmartWindowControl1_MouseWheel(object sender, MouseEventArgs e)
        {
            MouseEventArgs newe = new MouseEventArgs(e.Button, e.Clicks, e.X - Location.X, e.Y - Location.Y, e.Delta);
            hSmartWindowControl1.HSmartWindowControl_MouseWheel(sender, newe);
        }

        /// <summary>
        /// 放大缺陷 以矩形进行局部放大
        /// </summary>
        /// <param name="pImage">图像</param>
        /// <param name="pPoint">矩形中心</param>
        /// <param name="width">矩形宽度</param>
        /// <param name="height">矩形高度</param>
        public void Zoom(HObject pImage, PointF pPoint, double width, double height)
        {
            HOperatorSet.GetImageSize(pImage, out HTuple pImagewidth, out HTuple pImageHeight);

            double k = pImagewidth / pImageHeight;

            double Max = width > height ? width : height;
            width = Max * 2; //默认放大保留宽度为最大宽度的2被
            height = width / k;

            k = Convert.ToDouble(hSmartWindowControl1.Width) / hSmartWindowControl1.Height;
            if (k > 1)
                height = height / k;
            else
                width = width / k;

            HOperatorSet.SetPart(hSmartWindowControl1.HalconWindow, pPoint.Y - height, pPoint.X - width, pPoint.Y + height, pPoint.X + width);
        }


        /// <summary>
        /// 显示图片
        /// </summary>
        /// <param name="pImage">图片</param>
        public void ShowImgae(HObject pImage)
        {
            hSmartWindowControl1.SetFullImagePart( );
            hSmartWindowControl1.HalconWindow.ClearWindow();
            if (pImage != null)
            {
                InputImage = pImage;
                hSmartWindowControl1.HalconWindow.DispObj(pImage);
            }
        }

        /// <summary>
        /// HObject 转 HImage
        /// </summary>
        /// <param name="hobject"></param>
        /// <param name="image"></param>
        private void HobjectToHimage(HObject hobject, out HImage image)
        {
            image = new HImage();
            HTuple pointer, type, width, height;
            HOperatorSet.GetImagePointer1(hobject, out pointer, out type, out width, out height);
            image.GenImage1(type, width, height, pointer);
        }

        /// <summary>
        /// 清除窗体所有的内容
        /// </summary>
        public void Clear()
        {
            hSmartWindowControl1.HalconWindow.ClearWindow();
        }

        /// <summary>
        /// 适应窗口
        /// </summary>
        public void Fit()
        {
            HOperatorSet.SetPart(hSmartWindowControl1.HalconWindow, 0, 0, -2, -2);
        }

        /// <summary>
        /// 显示矩形
        /// </summary>
        /// <param name="pRectangle">矩形信息</param>
        /// <param name="pColor">颜色</param>
        public void ShowRectangle(List< Rectangle> pRectangle, EHColor pColor = EHColor.green)
        {
            try
            {
                hSmartWindowControl1.HalconWindow.SetLineWidth(2);
                hSmartWindowControl1.HalconWindow.SetColor(pColor.ToString());

                for (int i = 0; i < pRectangle.Count; i++)
                {
                    HObject region, roi;
                    Rectangle rectangle = pRectangle[i];
                    HOperatorSet.GenRectangle1(out region, rectangle.Y, rectangle.X, rectangle.Y + rectangle.Height, rectangle.X + rectangle.Width);

                    HOperatorSet.GenContourRegionXld(region, out roi, "border");
                    hSmartWindowControl1.HalconWindow.DispObj(roi);
                }
            }
            catch (Exception ex)
            {
                throw (ex);
            }
        }

    
        /// <summary>
        /// 显示文字
        /// </summary>
        /// <param name="text">信息</param>
        /// <param name="col">x坐标</param>
        /// <param name="row">y坐标</param>
        /// <param name="windowControl">显示控件</param>
        /// <param name="pColor">颜色</param>
        /// <param name="size">字体大小</param>
        public void ShowText(string text, int col, int row, HSmartWindowControl windowControl, EHColor pColor = EHColor.blue, int size = 20)
        {
            hSmartWindowControl1.HalconWindow.SetFont("Arial-Normal-" + size.ToString());
            hSmartWindowControl1.HalconWindow.DispText(text, "image", row, col, pColor.ToString(), new HTuple(), new HTuple());
        }

        /// <summary>
        /// 保存图片
        /// </summary>
        /// <param name="pPath"></param>
        /// <param name="pFilename"></param>
        public void SaveImage(string pPath, string pFilename)
        {
            try
            {
                if (!Directory.Exists(pPath))
                    Directory.CreateDirectory(pPath);
                string path = System.IO.Path.Combine(pPath, pFilename);
                path = System.IO.Path.GetFullPath(path);
                HOperatorSet.WriteImage(InputImage, "bmp", 0, path);
            }
            catch (Exception ex)
            {
                throw (ex);
            }
        }

        /// <summary>
        /// 加载图片并显示
        /// </summary>
        /// <param name="pImage"></param>
        /// <param name="pPath"></param>
        /// <param name="pFilename"></param>
        public void LoadImage(out HObject pImage, string pPath, string pFilename)
        {
            pImage = null;
            string path = System.IO.Path.Combine(pPath, pFilename);
            path = System.IO.Path.GetFullPath(path);
            if (File.Exists(path))
            {
                HOperatorSet.ReadImage(out pImage, path);
                ShowImgae(pImage);
            }
        }

    }

    /// <summary>
    /// 颜色类
    /// </summary>
    class CColor
    {


        public const string black = "black";

        public const string white = "white";

        public const string red = "red";

        public const string green = "green";

        public const string blue = "blue";

        public const string dim_gray = "dim gray";

        public const string gray = "gray";

        public const string light_gray = "light gray";

        public const string cyan = "cyan";

        public const string magenta = "magenta";

        public const string yellow = "yellow";

        public const string medium_slate_blue = "medium slate blue";

        public const string coral = "coral";

        public const string slate_blue = "slate blue";

        public const string spring_green = "spring green";

        public const string orange_red = "orange red";

        public const string dark_olive_green = "dark olive green";

        public const string pink = "pink";

        public const string cadet_blue = "cadet blue";

        public const string goldenrod = "goldenrod";

        public const string orange = "orange";

        public const string gold = "gold";

        public const string forest_green = "forest green";

        public const string cornflower_blue = "cornflower blue";

        public const string navy = "navy";

        public const string turquoise = "turquoise";

        public const string dark_slate_blue = "dark slate blue";

        public const string light_blue = "light blue";

        public const string indian_red = "indian red";

        public const string violet_red = "violet red";

        public const string light_steel_blue = "light steel blue";

        public const string medium_blue = "medium blue";

        public const string khaki = "khaki";

        public const string violet = "violet";

        public const string firebrick = "firebrick";

        public const string midnight_blue = "midnight blue";
    }

}

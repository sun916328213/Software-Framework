using Cognex.VisionPro;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Core.ImageEx
{
    public static  class IntPtrEx 
    {
        /// <summary>
        /// 指针转换成HImage Byte
        /// </summary>
        /// <param name="intPtr">指针数据</param>
        /// <param name="width">图像宽度</param>
        /// <param name="height">图像高度</param>
        /// <returns></returns>
        public static HObject ToHImageByte(this IntPtr intPtr, int width, int height)
        {
            HObject image = null;
            HOperatorSet.GenImage1(out image, "byte", width, height, intPtr);
            return image;
        }

        /// <summary>
        /// 指针转换成HImage  Uint2
        /// </summary>
        /// <param name="intPtr">指针数据</param>
        /// <param name="width">图像宽度</param>
        /// <param name="height">图像高度</param>
        /// <returns></returns>
        public static HObject ToHImageUint2(this IntPtr intPtr, int width, int height)
        {
            HObject image = null;
            HOperatorSet.GenImage1(out image, "uint2", width, height, intPtr);//取内存数据，生成图像，halcon实现
            return image;
        }

        /// <summary>
        /// 指针转换成HImage RGB
        /// </summary>
        /// <param name="intPtr">指针数据</param>
        /// <param name="width">图像宽度</param>
        /// <param name="height">图像高度</param>
        /// <returns></returns>
        public static HObject ToHImageRGB(this IntPtr intPtr, int width, int height)
        {
            HObject image = null;
            HOperatorSet.GenImageInterleaved(out image, (HTuple)intPtr, "bgr", width, height, 0, "byte", width, height, 0, 0, 8, 0);
            return image;
        }


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

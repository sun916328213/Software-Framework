using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Imaging.Halcon
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
    }
}

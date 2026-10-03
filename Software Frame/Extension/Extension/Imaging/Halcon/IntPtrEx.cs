// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：IntPtrEx。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：HalconDotNet、System、System.Collections.Generic、System.Linq、System.Text、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Imaging.Halcon
{
    /// <summary>
    /// IntPtrEx 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
    /// </summary>
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

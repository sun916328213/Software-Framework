using Extension.Core.FileEx;
using HalconDotNet;
using OpenCvSharp;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Extension.Core.ImageEx
{
    public static  class HImageEx
    {
        /// <summary>
        /// 保存图像
        /// </summary>
        /// <param name="image"></param>
        /// <param name="path"></param>
        /// <param name="filename"></param>
        public static void SaveImage(this HObject image, string path, string filename)
        {
            DirectoryEx.JudgeDirExist(path);
            Task.Run(() =>
            {
                if (image != null)
                {
                    path = System.IO.Path.Combine(path, filename);
                    path = System.IO.Path.GetFullPath(path);
                    HOperatorSet.WriteImage(image, "bmp", 0, path);
                }
            });
        }

        /// <summary>
        /// 保存图像
        /// </summary>
        /// <param name="image"></param>
        /// <param name="path"></param>
        public static void SaveImage(this HObject image, string path)
        {
            var d1 = DirectoryEx.GetParent(path);
            var d2 = DirectoryEx.GetDirName(path);
            SaveImage(image, d1, d2);
        }


        /// <summary>
        /// 从文件中读取图像
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="filename">文件名称</param>
        /// <returns>HObject</returns>
        public static HObject OpenImage(string path, string filename)
        {
            path = System.IO.Path.Combine(path, filename);
            HOperatorSet.ReadImage(out HObject image, path);
            return image;
        }

        /// <summary>
        /// 从文件中读取图像
        /// </summary>
        /// <param name="filepath">文件路径</param>
        /// <returns></returns>
        public static HObject OpenImage(string filepath)
        {
            HOperatorSet.ReadImage(out HObject image, filepath);
            return image;
        }

        /// <summary>
        /// 获取图像指针和相关信息
        /// </summary>
        /// <param name="image"></param>
        /// <param name="intPtr"></param>
        /// <param name="imagewidth"></param>
        /// <param name="imageheight"></param>
        /// <param name="imagechannels"></param>
        public static void ToIntPtr(this HObject image, out IntPtr intPtr, out int imagewidth, out int imageheight, out int imagechannels)
        {
            // 获取图像指针和相关信息
            HOperatorSet.GetImagePointer1(image, out HTuple pointer, out HTuple type, out HTuple width, out HTuple height);

            HOperatorSet.CountChannels(image, out HTuple channels);

            // 根据图像类型确定字节数组大小
            int pixelCount = width.I * height.I;
            int bytesPerPixel;

            string strType = type.S;
            switch (strType)
            {
                case "byte":
                    bytesPerPixel = 1;
                    break;
                case "int2":
                    bytesPerPixel = 2;
                    break;
                case "uint2":
                    bytesPerPixel = 2;
                    break;
                case "int4":
                    bytesPerPixel = 4;
                    break;
                default:
                    bytesPerPixel = 1;
                    break;
            }

            intPtr = pointer;
            imagewidth = width.I;
            imageheight = height.I;
            imagechannels = channels.I;
        }
        /// <summary>
        /// 将HObject转换为OpenCV的Mat对象
        /// </summary>
        /// <param name="image">HObject图像</param>
        /// <returns>Mat对象</returns>
        public static Mat ToMat(this HObject image)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            // 获取图像指针和相关信息
            HOperatorSet.GetImagePointer1(image, out HTuple pointer, out HTuple type, out HTuple width, out HTuple height);
            HOperatorSet.CountChannels(image, out HTuple channels);

            int imgWidth = width.I;
            int imgHeight = height.I;
            int imgChannels = channels.I;
            string imgType = type.S;

            Mat mat = null;

            // 根据图像类型创建对应的Mat对象
            switch (imgType)
            {
                case "byte":
                    if (imgChannels == 1)
                    {
                        mat = new Mat(height, width, MatType.CV_8UC1);
                        unsafe
                        {
                            Buffer.MemoryCopy((void*)pointer.IP, (void*)mat.Data, width * height, width * height);
                        }              
                    }
                    else if (imgChannels == 3)
                    {
                        // 彩色图像
                        mat = new Mat(height, width, MatType.CV_8UC3);
                        unsafe
                        {
                            Buffer.MemoryCopy((void*)pointer.IP, (void*)mat.Data, width * height * 3, width * height * 3);
                        }
                    }
                    break;
                case "uint2":
                    mat = new Mat(height, width, MatType.CV_16UC1);
                    unsafe
                    {
                        Buffer.MemoryCopy((void*)pointer.IP, (void*)mat.Data, width * height * 2, width * height * 2);
                    }
                    break;
                case "int2":
                    mat = new Mat(height, width, MatType.CV_16SC1);
                    unsafe
                    {
                        Buffer.MemoryCopy((void*)pointer.IP, (void*)mat.Data, width * height * 2, width * height * 2);
                    }
                    break;
                case "int4":
                    mat = new Mat(height, width, MatType.CV_32SC1);
                    unsafe
                    {
                        Buffer.MemoryCopy((void*)pointer.IP, (void*)mat.Data, width * height * 4, width * height * 4);
                    }
                    break;
                case "real":
                    mat = new Mat(height, width, MatType.CV_32FC1);
                    unsafe
                    {
                        Buffer.MemoryCopy((void*)pointer.IP, (void*)mat.Data, width * height * 4, width * height * 4);
                    }
                    break;
                case "double":
                    mat = new Mat(height, width, MatType.CV_64FC1);
                    unsafe
                    {
                        Buffer.MemoryCopy((void*)pointer.IP, (void*)mat.Data, width * height * 8, width * height * 8);
                    }
                    break;
                default:
                    throw new NotSupportedException($"Unsupported image type: {imgType}");
            }

            // 创建Mat的深拷贝，避免指针释放后出现问题
            return mat.Clone();
        }
 
    }
}

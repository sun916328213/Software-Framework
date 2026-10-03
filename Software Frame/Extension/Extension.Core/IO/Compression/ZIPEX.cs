// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：ZIPEX。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.IO、System.IO.Compression、System.Linq、System.Text。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.IO.Compression
{
    /// <summary>
    /// ZIPEX 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
    /// </summary>
    public static class ZIPEX
    {
        /// <summary>
        /// GZIP解压成string
        /// </summary>
        /// <param name="path">GZIP文件路径</param>
        /// <returns></returns>
        public static string  GZIPExtractToString( string path)
        {
            using (FileStream fileStream = new FileStream(path, FileMode.Open))
            {
                using (GZipStream gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
                {
                    using (StreamReader reader = new StreamReader(gzipStream))
                    {
                       return reader.ReadToEnd();
                    }
                }
            }
        }
    }
}

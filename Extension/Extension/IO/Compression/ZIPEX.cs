using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.IO.Compression
{
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

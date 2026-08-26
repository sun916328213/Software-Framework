using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Core.FileEx
{
    public static class FileEx
    {
        /// <summary>
        /// 读取文件数据
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="fileName">文件名称</param>
        /// <returns></returns>
        public static string Read(string filePath, string fileName)
        {
            var path = Path.Combine(filePath, fileName);
            return File.ReadAllText(path);
        }

        /// <summary>
        /// 写入文件数据（覆盖前将旧文件轮换为 .bak 备份，保留上一版本；备份失败不阻断写入）
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="filePath">文件路径</param>
        /// <param name="fileName">文件名称</param>
        public static void Write(this string data, string filePath, string fileName)
        {
            var path = string.IsNullOrEmpty(filePath) ? fileName : Path.Combine(filePath, fileName);

            // 覆盖前将旧文件备份为 .bak（仅保留一代）
            if (File.Exists(path))
            {
                try { File.Copy(path, path + ".bak", true); }
                catch { /* 备份失败不阻断写入 */ }
            }

            File.WriteAllText(path, data);
        }
    }
}

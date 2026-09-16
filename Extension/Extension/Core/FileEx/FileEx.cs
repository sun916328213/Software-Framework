using System;
using System.Collections.Generic;
using System.Diagnostics;
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
                // 备份失败不能阻断主流程的写入（否则"备份盘满了"会直接导致业务数据丢失），
                // 但也不能完全静默：备份失败的后果是"出问题时没有上一版本可回滚"，
                // 必须在调试输出留痕，否则直到真需要 .bak 时才发现它从来没生成过。
                try { File.Copy(path, path + ".bak", true); }
                catch (Exception ex) when (ex is IOException
                                              or UnauthorizedAccessException
                                              or NotSupportedException
                                              or ArgumentException
                                              or System.Security.SecurityException)
                {
                    Debug.WriteLine($"[FileEx] 写入前备份 '{path}' 失败，已跳过备份继续写入：{ex.Message}");
                }
            }

            File.WriteAllText(path, data);
        }
    }
}

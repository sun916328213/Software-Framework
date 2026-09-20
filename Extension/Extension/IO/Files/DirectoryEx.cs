using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Extension.IO.Files
{
    public static  class DirectoryEx 
    {
        /// <summary>
        /// 判断文件夹是否存在 不存在则创建
        /// </summary>
        /// <param name="pPath"></param>
        public static void JudgeDirExist(this string pPath)
        {
            if (!Directory.Exists(pPath))
                Directory.CreateDirectory(pPath);
        }

        /// <summary>
        /// 获取文件夹内指定名称的文件
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public static FileInfo[] GetFiles(this string filePath,string filter)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(filePath);
            return directoryInfo.GetFiles($"*{filter}*"); // 查找包含日期的文件
        }

        /// <summary>
        /// 根据路径,产品ID 新建路径 pPath//Year//Month//Day//pID//
        /// </summary>
        /// <param name="pPath">路径</param>
        /// <param name="pID">产品ID</param>
        public static string CreatDir(this string pPath, string pID)
        {
            DateTime Now = DateTime.Now;
            pPath = Path.Combine(pPath, Now.Year.ToString(), Now.Month.ToString(), Now.Day.ToString());
            if (pID != null)
                pPath = Path.Combine(pPath, pID);
            return pPath;
        }

        /// <summary>
        /// 根据时间获取路径 //Year//Month//Day//pID//
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public static string GetPath(this DateTime dateTime)
        {
            return Path.Combine(dateTime.Year.ToString(), dateTime.Month.ToString(), dateTime.Day.ToString());
        }

 
        /// <summary>
        /// 根据文件路径读取文件 
        /// </summary>
        /// <param name="path">基础路径</param>
        /// <returns>包含所有符合条件文件信息的集合</returns>
        public static Dictionary<string, FileInfo[]> ReadFile(this string path)
        {
            if (!Directory.Exists(path)) return null;

            Dictionary<string, FileInfo[]> fileInfos = new Dictionary<string, FileInfo[]>();

            // 获取该日期路径下的所有文件夹(产品ID文件夹)
            DirectoryInfo dateDirectory = new DirectoryInfo(path);
            DirectoryInfo[] productDirectories = dateDirectory.GetDirectories();

            // 遍历每个产品ID文件夹
            foreach (DirectoryInfo productDir in productDirectories)
            {
                // 获取该产品ID文件夹下的所有文件
                FileInfo[] files = productDir.GetFiles();

                // 添加到集合中
                fileInfos.Add(productDir.Name, files );
            }
            return fileInfos;
        }


        /// <summary>
        /// 获取文件上一级
        /// </summary>
        /// <param name="pPath"></param>
        /// <returns></returns>
        public static string GetParent(this string pPath)
        {
            DirectoryInfo dirInfo = Directory.GetParent(pPath);
            if (dirInfo != null)
            {
                return dirInfo.FullName;
            }
            return null;
        }


        /// <summary>
        /// 获取文件上二级
        /// </summary>
        /// <param name="pPath"></param>
        /// <returns></returns>
        public static string GetParentSecond(this string pPath)
        {
            DirectoryInfo dirInfo = Directory.GetParent(pPath).Parent;
            if (dirInfo != null)
            {
                return dirInfo.FullName;
            }
            return null;
        }

        /// <summary>
        /// 获取文件夹名称
        /// </summary>
        /// <param name="pPath"></param>
        /// <returns></returns>
        public static string GetDirName(this string pPath)
        {
            var dirInfo = new DirectoryInfo(pPath);
            if (dirInfo != null)
            {
                return dirInfo.Name;
            }
            return null;
        }

        /// <summary>
        /// 文件夹复制
        /// </summary>
        /// <param name="srcPath">源文件夹路径</param>
        /// <param name="destPath">目标文件夹路径</param>
        public static async Task DirCopy(this string srcPath, string destPath)
        {
            await Task.Run(() =>
            {                // 简单复制（自动创建目录结构）
                Directory.CreateDirectory(destPath);  // 确保目标目录存在
                foreach (string file in Directory.EnumerateFiles(srcPath, "*", SearchOption.AllDirectories))
                {
                    string destFile = file.Replace(srcPath, destPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destFile));
                    File.Copy(file, destFile, overwrite: true);
                }
            });
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Core.FileEx
{
    internal class ScheduledDeletionEx
    {
        /// <summary>
        /// 删除超过指定保存天数的文件，同时根据磁盘剩余空间进行删除
        /// </summary>
        /// <param name="directoryPath">要清理的目录路径</param>
        /// <param name="keepDays">保留天数</param>
        /// <param name="minFreeSpaceMB">最小剩余空间(MB)，当磁盘空间低于此值时会删除旧文件</param>
        /// <returns>被删除的文件夹路径列表</returns>
        public async Task<List<string>> DeleteExpiredFiles(string directoryPath, int keepDays, long minFreeSpaceMB = 1024 * 50)
        {
            List<string> deletedFolders = await Task.Run(() =>
            {
                if (!Directory.Exists(directoryPath))
                {
                    throw new Exception($"目录不存在: {directoryPath}");
                }
 
                int deletedCount = 0;
                var directoryInfo = new DirectoryInfo(directoryPath);
                List<string> deletedFolderPaths = new List<string>();

                // 获取所有日期格式的子文件夹（支持多级目录结构）
                var dateFolders = new List<DateFolderInfo>();
                GetAllDateFolders(directoryInfo, dateFolders);

                // 计算过期日期
                DateTime expireDate = DateTime.Now.AddDays(-keepDays);
            
                // 删除过期文件夹
                foreach (var folderInfo in dateFolders)
                {
                    // 如果文件夹日期早于过期日期，则删除整个文件夹
                    if (folderInfo.Date < expireDate.Date)
                    {
                        try
                        {
                            Directory.Delete(folderInfo.DirectoryInfo.FullName, true);
                            deletedFolderPaths.Add(folderInfo.DirectoryInfo.FullName);
                            deletedCount++;
                            throw new Exception($"删除过期文件夹: {folderInfo.DirectoryInfo.FullName}, 文件夹日期: {folderInfo.Date:yyyy-MM-dd}");
                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"删除文件夹失败: {folderInfo.DirectoryInfo.FullName}, 错误: {ex.Message}");
                        }
                    }
                }

                // 检查磁盘剩余空间，如果不足则继续删除旧文件夹
                try
                {
                    DriveInfo drive = new DriveInfo(directoryInfo.Root.FullName);
                    long freeSpaceMB = drive.AvailableFreeSpace / (1024 * 1024);
                
                    if (freeSpaceMB < minFreeSpaceMB)
                    {
                        // 计算需要释放的空间
                        long needReleaseMB = minFreeSpaceMB - freeSpaceMB;
                        long releasedMB = 0;

                        // 按日期从早到晚删除文件夹，直到释放足够空间
                        foreach (var folderInfo in dateFolders.Where(f => f.Date >= expireDate.Date).OrderBy(f => f.Date))
                        {
                            if (releasedMB >= needReleaseMB)
                                break;

                            try
                            {
                                long folderSizeMB = GetDirectorySize(folderInfo.DirectoryInfo) / (1024 * 1024);
                                Directory.Delete(folderInfo.DirectoryInfo.FullName, true);
                                deletedFolderPaths.Add(folderInfo.DirectoryInfo.FullName);
                                releasedMB += folderSizeMB;
                                deletedCount++;
                                throw new Exception($"因空间不足删除文件夹: {folderInfo.DirectoryInfo.FullName}, 大小: {folderSizeMB}MB, 已释放: {releasedMB}MB");
                            }
                            catch (Exception ex)
                            {
                                throw new Exception($"删除文件夹失败: {folderInfo.DirectoryInfo.FullName}, 错误: {ex.Message}");
                            }
                        }

                        throw new Exception($"空间释放完成，共释放: {releasedMB}MB");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"检查磁盘空间或释放空间时出错: {ex.Message}");
                }

                return deletedFolderPaths;
            });

            return deletedFolders;
        }

        /// <summary>
        /// 递归获取所有日期格式的文件夹
        /// </summary>
        /// <param name="directory">目录信息</param>
        /// <param name="dateFolders">日期文件夹列表</param>
        private void GetAllDateFolders(DirectoryInfo directory, List<DateFolderInfo> dateFolders)
        {
            try
            {
                // 递归处理子目录
                DirectoryInfo[] subDirectories = directory.GetDirectories();
                foreach (DirectoryInfo subDir in subDirectories)
                {
                    // 尝试解析当前目录的日期
                    if (TryParseDateFromPath(subDir.FullName, out DateTime date))
                    {
                        dateFolders.Add(new DateFolderInfo { DirectoryInfo = subDir, Date = date });
                    }

                    // 继续递归处理子目录
                    GetAllDateFolders(subDir, dateFolders);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"获取目录文件时出错: {directory.FullName}, 错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 从路径解析日期（支持多级目录结构，如 C:\Image\2025\9\17）
        /// </summary>
        /// <param name="path">路径</param>
        /// <param name="date">解析出的日期</param>
        /// <returns>是否解析成功</returns>
        private bool TryParseDateFromPath(string path, out DateTime date)
        {
            date = DateTime.MinValue;

            try
            {
                // 分割路径
                string[] parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                // 从后往前查找可能的日期部分
                if (parts.Length >= 3)
                {
                    // 假设格式为 C:\Image\2025\9\17
                    string year = parts[parts.Length - 3];
                    string month = parts[parts.Length - 2];
                    string day = parts[parts.Length - 1];

                    // 检查是否都是数字
                    if (year.All(char.IsDigit) && month.All(char.IsDigit) && day.All(char.IsDigit))
                    {
                        if (int.TryParse(year, out int y) && int.TryParse(month, out int m) && int.TryParse(day, out int d))
                        {
                            date = new DateTime(y, m, d);
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception ($"解析路径日期失败: {path}, 错误: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// 获取目录大小
        /// </summary>
        /// <param name="directory">目录信息</param>
        /// <returns>目录大小（字节）</returns>
        private long GetDirectorySize(DirectoryInfo directory)
        {
            try
            {
                long size = 0;

                // 计算文件大小
                FileInfo[] files = directory.GetFiles();
                foreach (FileInfo file in files)
                {
                    size += file.Length;
                }

                // 递归计算子目录大小
                DirectoryInfo[] subDirectories = directory.GetDirectories();
                foreach (DirectoryInfo subDir in subDirectories)
                {
                    size += GetDirectorySize(subDir);
                }

                return size;
            }
            catch (Exception ex)
            {
                throw new Exception($"计算目录大小时出错: {directory.FullName}, 错误: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 日期文件夹信息
    /// </summary>
    public class DateFolderInfo
    {
        /// <summary>
        /// 目录信息
        /// </summary>
        public DirectoryInfo DirectoryInfo { get; set; }

        /// <summary>
        /// 目录日期
        /// </summary>
        public DateTime Date { get; set; }
    }
}

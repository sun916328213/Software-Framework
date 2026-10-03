// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：ScheduledDeletion、DateFolder。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.Diagnostics、System.IO、System.Linq、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Extension.IO.Retention
{
    /// <summary>删除已过期或为回收磁盘空间而需腾退的按日期分区的目录。</summary>
    public static class ScheduledDeletion
    {
        private const long BytesPerMegabyte = 1024L * 1024L;

        /// <summary>
        /// 清理按 根目录/年/月/日 布局存放的目录，并返回成功删除的天级目录。
        /// 缺失的根目录与单个删除失败按尽力而为处理，不抛异常。
        /// </summary>
        public static Task<List<string>> DeleteExpiredFiles(
            string directoryPath,
            int keepDays,
            long minFreeSpaceMB = 1024 * 50)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
            ArgumentOutOfRangeException.ThrowIfNegative(keepDays);
            ArgumentOutOfRangeException.ThrowIfNegative(minFreeSpaceMB);

            if (!Directory.Exists(directoryPath))
                return Task.FromResult(new List<string>());

            return Task.Run(() => DeleteExpiredFilesCore(directoryPath, keepDays, minFreeSpaceMB));
        }

        /// <summary>
        /// 执行删除操作；对应入口为 DeleteExpiredFilesCore。
        /// 正文中的关键协作调用包括 Exists、ToList、OrderBy、FindDateFolders、AddDays；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        private static List<string> DeleteExpiredFilesCore(string directoryPath, int keepDays, long minFreeSpaceMB)
        {
            var deleted = new List<string>();
            if (!Directory.Exists(directoryPath)) return deleted;

            var root = new DirectoryInfo(directoryPath);
            var dateFolders = FindDateFolders(root).OrderBy(folder => folder.Date).ToList();
            var expireBefore = DateTime.Today.AddDays(-keepDays);

            foreach (var folder in dateFolders.Where(folder => folder.Date < expireBefore))
                TryDelete(folder.Directory, deleted);

            if (minFreeSpaceMB == 0) return deleted;

            var requiredBytes = ToBytesSaturated(minFreeSpaceMB);
            long availableBytes;
            try
            {
                availableBytes = new DriveInfo(root.Root.FullName).AvailableFreeSpace;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                Trace.TraceWarning($"Unable to inspect free space for '{directoryPath}': {ex.Message}");
                return deleted;
            }

            var bytesToRelease = requiredBytes > availableBytes ? requiredBytes - availableBytes : 0;
            if (bytesToRelease == 0) return deleted;

            long releasedBytes = 0;
            var deletedSet = new HashSet<string>(deleted, StringComparer.OrdinalIgnoreCase);
            foreach (var folder in dateFolders.Where(folder => folder.Date >= expireBefore))
            {
                if (releasedBytes >= bytesToRelease) break;
                if (deletedSet.Contains(folder.Directory.FullName)) continue;

                var folderBytes = GetDirectorySize(folder.Directory);
                if (TryDelete(folder.Directory, deleted))
                {
                    deletedSet.Add(folder.Directory.FullName);
                    releasedBytes = AddSaturated(releasedBytes, folderBytes);
                }
            }

            return deleted;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 FindDateFolders。
        /// 正文中的关键协作调用包括 SafeDirectories、TryParse；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        private static IEnumerable<DateFolder> FindDateFolders(DirectoryInfo root)
        {
            foreach (var yearDirectory in SafeDirectories(root))
            {
                if (yearDirectory.Name.Length != 4 || !int.TryParse(yearDirectory.Name, out var year) || year < 1900)
                    continue;

                foreach (var monthDirectory in SafeDirectories(yearDirectory))
                {
                    if (!int.TryParse(monthDirectory.Name, out var month)) continue;
                    foreach (var dayDirectory in SafeDirectories(monthDirectory))
                    {
                        if (!int.TryParse(dayDirectory.Name, out var day)) continue;
                        DateTime date;
                        try { date = new DateTime(year, month, day); }
                        catch (ArgumentOutOfRangeException) { continue; }
                        yield return new DateFolder(dayDirectory, date);
                    }
                }
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 SafeDirectories。
        /// 正文中的关键协作调用包括 GetDirectories、TraceWarning、Empty；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        private static IEnumerable<DirectoryInfo> SafeDirectories(DirectoryInfo directory)
        {
            try { return directory.GetDirectories(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Trace.TraceWarning($"Unable to enumerate '{directory.FullName}': {ex.Message}");
                return Array.Empty<DirectoryInfo>();
            }
        }

        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryDelete。
        /// 正文中的关键协作调用包括 Delete、Add、TraceWarning；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        private static bool TryDelete(DirectoryInfo directory, ICollection<string> deleted)
        {
            try
            {
                Directory.Delete(directory.FullName, recursive: true);
                deleted.Add(directory.FullName);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Trace.TraceWarning($"Unable to delete '{directory.FullName}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetDirectorySize。
        /// 正文中的关键协作调用包括 EnumerateFiles、AddSaturated、TraceWarning；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        private static long GetDirectorySize(DirectoryInfo directory)
        {
            try
            {
                long size = 0;
                foreach (var file in directory.EnumerateFiles("*", SearchOption.AllDirectories))
                    size = AddSaturated(size, file.Length);
                return size;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Trace.TraceWarning($"Unable to measure '{directory.FullName}': {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ToBytesSaturated。
        /// </summary>
        private static long ToBytesSaturated(long megabytes) =>
            megabytes > long.MaxValue / BytesPerMegabyte ? long.MaxValue : megabytes * BytesPerMegabyte;

        /// <summary>
        /// 执行添加操作；对应入口为 AddSaturated。
        /// </summary>
        private static long AddSaturated(long left, long right) =>
            right > long.MaxValue - left ? long.MaxValue : left + right;

        /// <summary>
        /// DateFolder 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
        /// </summary>
        private readonly record struct DateFolder(DirectoryInfo Directory, DateTime Date);
    }
}
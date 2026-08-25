using Extension.Core.ControlEx;
using Extension.Core.SystemEx;
using Prism.Ioc;
using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Threading;
using System.Timers;
using WPFControl.ProgramControl.Models;
using Timer = System.Timers.Timer;

namespace WPFControl.ProgramControl.Services
{
    public class ResourceMonitorService : IDisposable
    {
        public ResourceMonitorModel Model { get; private set; }

        // CPU性能计数器
        private CpuMonitorEx cpuMonitor;
       
        // 内存性能计数器
        private PerformanceCounter ramCounter;
     
        private readonly object lockObject = new object();
        
        // 系统总内存（MB）
        private double totalSystemMemory = 0;
        
         //刷新任务
        Timer? refreshTimer;


        public ResourceMonitorService(IContainerProvider container)
        {
            Model = container.Resolve<ResourceMonitorModel>();
            
            // 获取系统总内存
            GetTotalSystemMemory();
            
            // 初始化性能计数器
            InitializePerformanceCounters();
        }
        
        /// <summary>
        /// 获取系统总内存
        /// </summary>
        private void GetTotalSystemMemory()
        {
            try
            {
                 var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (ManagementBaseObject obj in searcher.Get())
                {
                    totalSystemMemory= Convert.ToUInt64(obj["TotalPhysicalMemory"]) / (1024.0 * 1024.0 );
                }
            }
            catch
            {
                // 如果所有方法都失败，使用默认值
                totalSystemMemory = 16384 * 1; // 默认16GB
            }
        }
        
       
        
        /// <summary>
        /// 初始化性能计数器
        /// </summary>
        private void InitializePerformanceCounters()
        {
            try
            {
                // 使用改进的CpuMonitor类来获取CPU使用率
                // 这个类直接使用与任务管理器相同的性能计数器，结果更准确
                cpuMonitor = new  CpuMonitorEx();
                
                // 内存使用率计数器
                try
                {
                    ramCounter = new PerformanceCounter("Memory", "Available MBytes");
                    // 预先调用一次以初始化计数器
                    ramCounter.NextValue();
                }
                catch
                {
                    // 如果无法创建内存计数器，将ramCounter设置为null
                    ramCounter = null;
                }
            }
            catch
            {
                // 如果无法初始化性能计数器，将所有计数器设置为null
                cpuMonitor = null;
                ramCounter = null;
            }
        }
        
        /// <summary>
        /// 开始自动刷新
        /// </summary>
        /// <param name="interval">刷新间隔（毫秒），默认1000ms</param>
        public void StartAutoRefresh(int interval = 1000)
        {
            lock (lockObject)
            {
                if (refreshTimer != null) return;

                refreshTimer = new Timer(interval);
                refreshTimer.Elapsed += OnRefreshTimerElapsed;
                refreshTimer.AutoReset = true;
                refreshTimer.Start();
            }
        }

        /// <summary>
        /// 停止自动刷新
        /// </summary>
        public void StopAutoRefresh()
        {
            lock (lockObject)
            {
                if (refreshTimer == null) return;
                refreshTimer.Stop();
                refreshTimer.Elapsed -= OnRefreshTimerElapsed;
                refreshTimer.Dispose();
                refreshTimer = null;
            }
        }

        private void OnRefreshTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                // 在线程池线程上采集数据（PerformanceCounter、WMI 等是阻塞 IO）
                var cpuUsage = ReadSystemCpu();
                var (memUsage, memRate) = ReadSystemMemory();
                var (diskUsage, diskRate) = ReadDisk("D");

                // 将 UI 更新调度到 UI 线程
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher == null) return;
                dispatcher.Invoke(() =>
                {
                    Model.SystemCPU = cpuUsage;
                    Model.SystemCPURate = $"{cpuUsage:F1}%";
                    Model.SystemMemory = memUsage;
                    Model.SystemMemoryRate = memRate;
                    Model.Disk = diskUsage;
                    Model.DiskRate = diskRate;
                });
            }
            catch
            {
                // 忽略刷新资源信息时的异常
            }
        }

        private float ReadSystemCpu()
        {
            try
            {
                if (cpuMonitor != null)
                    return cpuMonitor.GetSystemCpuUsage();
            }
            catch { }
            return 0;
        }

        private (float Usage, string Rate) ReadSystemMemory()
        {
            try
            {
                if (ramCounter != null)
                {
                    float availableMemory = ramCounter.NextValue();
                    if (totalSystemMemory <= 0)
                        GetTotalSystemMemory();
                    float usedMemory = Math.Max(0, (float)totalSystemMemory - availableMemory);
                    float usedMemoryGB = usedMemory / 1024.0f;
                    float totalSystemMemoryGB = (float)totalSystemMemory / 1024.0f;
                    float usage = totalSystemMemoryGB > 0 ? (usedMemoryGB / totalSystemMemoryGB) * 100 : 0;
                    return (usage, $"{usedMemoryGB:F1}GB/{totalSystemMemoryGB:F1}GB");
                }
            }
            catch { }
            return (0, "0GB/0GB");
        }

        private (double Usage, string Rate) ReadDisk(string driveLetter)
        {
            try
            {
                var driveInfo = new DriveInfo($"{driveLetter}:");
                if (driveInfo.IsReady)
                {
                    double totalSizeGB = driveInfo.TotalSize / (1024.0 * 1024.0 * 1024.0);
                    double usedSpaceGB = (driveInfo.TotalSize - driveInfo.AvailableFreeSpace) / (1024.0 * 1024.0 * 1024.0);
                    double usage = totalSizeGB > 0 ? (usedSpaceGB / totalSizeGB) * 100 : 0;
                    return (usage, $"{usedSpaceGB:F0}GB/{totalSizeGB:F0}GB");
                }
            }
            catch { }
            return (0, "0GB/0GB");
        }

        /// <summary>
        /// 刷新资源信息的回调方法
        /// </summary>
        [Obsolete("使用 OnRefreshTimerElapsed 替代，内部已处理线程调度")]
        private void RefreshResources()
        {
            try
            {
                CalCPUSystem();
                CalMemorySystem();
                CalDisk("D");
            }
            catch { }
        }
        
        /// <summary>
        /// 计算整个系统的CPU使用情况 - 直接从Windows系统读取数据
        /// </summary>
        public void CalCPUSystem()
        {
            try
            {
                if (cpuMonitor != null)
                {
                    // 直接从Windows系统性能计数器读取系统CPU使用率
                    // 这种方法与任务管理器使用相同的数据源，结果更准确
                    float cpuUsage = cpuMonitor.GetSystemCpuUsage();
                    
                    // 系统CPU使用率显示（百分比）- 与任务管理器保持一致
                    Model.SystemCPU = cpuUsage;
                    Model.SystemCPURate = $"{cpuUsage:F1}%"; // 在数值基础上添加百分号
                }
                else
                {
                    Model.SystemCPU = 0;
                    Model.SystemCPURate = "0.0%";
                }
            }
            catch
            {
                Model.SystemCPU = 0;
                Model.SystemCPURate = "0.0%";
            }
        }
        
        /// <summary>
        /// 计算CPU使用情况 (显示当前程序的CPU使用量) - 直接从Windows系统读取数据
        /// </summary>
        public void CalCPU()
        {
            try
            {
                if (cpuMonitor != null)
                {
                    // 直接从Windows系统性能计数器读取进程CPU使用率
                    // 这种方法与任务管理器使用相同的数据源，结果更准确
                    float processCpuUsage = cpuMonitor.GetProcessCpuUsage();
                    
                    // CPU使用率显示（百分比）- 当前程序的CPU使用量，与任务管理器保持一致
                    Model.CPU = processCpuUsage;
                    Model.CPURate = $"{processCpuUsage:F1}%"; // 在数值基础上添加百分号
                }
                else
                {
                    Model.CPU = 0;
                    Model.CPURate = "0.0%";
                }
            }
            catch
            {
                Model.CPU = 0;
                Model.CPURate = "0.0%";
            }
        }
        
        /// <summary>
        /// 计算内存使用情况 (显示当前程序的内存使用量)
        /// </summary>
        public void CalMemory()
        {
            try
            {
                // 获取当前进程的内存使用情况
                var currentProcess = Process.GetCurrentProcess();
                var workingSet = currentProcess.WorkingSet64; // 当前工作集（物理内存）

                // 转换为GB
                double workingSetGB = workingSet / (1024.0 * 1024.0 * 1024.0);

                // 获取系统总内存
                if (totalSystemMemory <= 0)
                {
                    GetTotalSystemMemory();
                }

                // 转换总内存为GB
                double totalSystemMemoryGB = totalSystemMemory / 1024.0;

                // 计算内存使用率（基于工作集与系统总内存的比例）
                double memoryUsage = totalSystemMemory > 0 ? (workingSetGB / totalSystemMemoryGB) * 100 : 0;

                // Memory属性存储内存使用率数值（用于ProgressBar，0-100范围）
                Model.Memory = memoryUsage;
                // MemoryRate属性存储"使用GB/总量GB"格式的字符串
                Model.MemoryRate = $"{workingSetGB:F1}GB/{totalSystemMemoryGB:F1}GB";
            }
            catch
            {
                Model.Memory = 0;
                Model.MemoryRate = "0GB/0GB";
            }
        }
        
        /// <summary>
        /// 计算整个系统的内存使用情况
        /// </summary>
        public void CalMemorySystem()
        {
            try
            {
                if (ramCounter != null)
                {
                    // 获取可用内存（MB）
                    float availableMemory = ramCounter.NextValue();

                    // 如果总内存为0，尝试重新获取
                    if (totalSystemMemory <= 0)
                    {
                        GetTotalSystemMemory();
                    }

                    // 计算已使用内存
                    float usedMemory = (float)totalSystemMemory - availableMemory;

                    // 确保使用内存不为负数
                    usedMemory = Math.Max(0, usedMemory);

                    // 转换为GB
                    float usedMemoryGB = usedMemory / 1024.0f;
                    float totalSystemMemoryGB = (float)totalSystemMemory / 1024.0f;

                    // 计算内存使用率
                    float memoryUsage = totalSystemMemoryGB > 0 ? (usedMemoryGB / totalSystemMemoryGB) * 100 : 0;

                    // SystemMemory属性存储内存使用率数值（用于ProgressBar，0-100范围）
                    Model.SystemMemory = memoryUsage;
                    // SystemMemoryRate属性存储"使用GB/总量GB"格式的字符串
                    Model.SystemMemoryRate = $"{usedMemoryGB:F1}GB/{totalSystemMemoryGB:F1}GB";
                }
                else
                {
                    Model.SystemMemory = 0;
                    Model.SystemMemoryRate = "0GB/0GB";
                }
            }
            catch
            {
                Model.SystemMemory = 0;
                Model.SystemMemoryRate = "0GB/0GB";
            }
        }
        
        /// <summary>
        /// 计算磁盘使用情况
        /// </summary>
        /// <param name="driveLetter">驱动器字母，例如 "C"</param>
        public void CalDisk(string driveLetter = "C")
        {
            try
            {
                var driveInfo = new DriveInfo($"{driveLetter}:");
                if (driveInfo.IsReady)
                {
                    long totalSize = driveInfo.TotalSize;
                    long freeSpace = driveInfo.AvailableFreeSpace;
                    long usedSpace = totalSize - freeSpace;

                    // 转换为GB并显示为使用空间/总空间
                    double totalSizeGB = totalSize / (1024.0 * 1024.0 * 1024.0);
                    double usedSpaceGB = usedSpace / (1024.0 * 1024.0 * 1024.0);

                    // 计算磁盘使用率
                    double diskUsage = totalSizeGB > 0 ? (usedSpaceGB / totalSizeGB) * 100 : 0;

                    // Disk属性存储磁盘使用率数值（用于ProgressBar，0-100范围）
                    Model.Disk = diskUsage;
                    // DiskRate属性存储"使用GB/总量GB"格式的字符串
                    Model.DiskRate = $"{usedSpaceGB:F0}GB/{totalSizeGB:F0}GB";
                }
                else
                {
                    Model.Disk = 0;
                    Model.DiskRate = "0GB/0GB";
                }
            }
            catch
            {
                Model.Disk = 0;
                Model.DiskRate = "0GB/0GB";
            }
        }
        
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            StopAutoRefresh();
            cpuMonitor?.Dispose();
            ramCounter?.Dispose();
        }
    }
}
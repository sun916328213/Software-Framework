using System;
using System.Diagnostics;
using System.Threading;
using System.Linq;

namespace Extension.Core.SystemEx
{
    /// <summary>
    /// 通过直接读取任务管理器使用的性能计数器来获取CPU使用率
    /// 这种方法比原来的实现更准确，因为它直接使用与任务管理器相同的计数器
    /// </summary>
    public class CpuMonitorEx : IDisposable
    {
        // 系统CPU使用率计数器
        private PerformanceCounter systemCpuCounter;

        // 当前进程CPU使用率计数器
        private PerformanceCounter processCpuCounter;

        // 逻辑处理器数量，用于多核系统上的CPU使用率计算
        private readonly int processorCount;

        // 是否已初始化
        private bool isInitialized = false;

        public CpuMonitorEx()
        {
            processorCount = Environment.ProcessorCount;
            InitializeCounters();
        }

        /// <summary>
        /// 初始化性能计数器
        /// </summary>
        private void InitializeCounters()
        {
            try
            {
                // 初始化系统CPU计数器
                // Windows 10任务管理器首选使用"Processor Information\% Processor Utility"
                try
                {
                    systemCpuCounter = new PerformanceCounter("Processor Information", "% Processor Utility", "_Total");
                    systemCpuCounter.NextValue(); // 初始化
                }
                catch
                {
                    // 如果"% Processor Utility"不可用，回退到"% Processor Time"
                    try
                    {
                        systemCpuCounter = new PerformanceCounter("Processor Information", "% Processor Time", "_Total");
                        systemCpuCounter.NextValue(); // 初始化
                    }
                    catch
                    {
                        // 如果"Processor Information"不可用，回退到"Processor"
                        systemCpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                        systemCpuCounter.NextValue(); // 初始化
                    }
                }

                // 初始化进程CPU计数器
                string processInstanceName = GetProcessInstanceName(Process.GetCurrentProcess().Id);
                if (string.IsNullOrEmpty(processInstanceName))
                {
                    processInstanceName = Process.GetCurrentProcess().ProcessName;
                }

                processCpuCounter = new PerformanceCounter("Process", "% Processor Time", processInstanceName, true);
                processCpuCounter.NextValue(); // 初始化

                isInitialized = true;

                // 短暂延迟以确保计数器正确初始化
                Thread.Sleep(100);
            }
            catch (Exception)
            {
                isInitialized = false;
            }
        }

        /// <summary>
        /// 获取进程在性能计数器中的实例名称
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns>性能计数器的实例名称</returns>
        private string GetProcessInstanceName(int processId)
        {
            try
            {
                PerformanceCounterCategory category = new PerformanceCounterCategory("Process");
                string[] instanceNames = category.GetInstanceNames();

                // 首先尝试精确匹配进程ID
                foreach (string instanceName in instanceNames)
                {
                    try
                    {
                        using (PerformanceCounter counter = new PerformanceCounter("Process", "ID Process", instanceName, true))
                        {
                            long val = counter.RawValue;
                            if (val == processId)
                            {
                                return instanceName;
                            }
                        }
                    }
                    catch
                    {
                        // 跳过无法访问的实例
                        continue;
                    }
                }

                // 如果没有找到精确匹配，尝试查找包含进程ID的实例名称
                string processName = Process.GetProcessById(processId).ProcessName;
                var matchingInstances = instanceNames
                    .Where(name => name.StartsWith(processName, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(name => name)
                    .ToList();

                if (matchingInstances.Count > 0)
                {
                    // 如果只有一个匹配项，直接返回
                    if (matchingInstances.Count == 1)
                    {
                        return matchingInstances[0];
                    }
                    
                    // 如果有多个匹配项，返回第一个（通常是正确的）
                    return matchingInstances[0];
                }
            }
            catch
            {
                // 忽略异常
            }

            return string.Empty;
        }

        /// <summary>
        /// 获取系统CPU使用率（与任务管理器一致）
        /// </summary>
        /// <returns>系统CPU使用率（0-100）</returns>
        public float GetSystemCpuUsage()
        {
            if (!isInitialized || systemCpuCounter == null)
                return 0f;

            try
            {
                // 读取CPU使用率
                float currentUsage = systemCpuCounter.NextValue();

                // 确保值在有效范围内
                return Math.Max(0f, Math.Min(100f, currentUsage));
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// 获取当前进程CPU使用率（与任务管理器一致）
        /// </summary>
        /// <returns>进程CPU使用率（0-100）</returns>
        public float GetProcessCpuUsage()
        {
            if (!isInitialized || processCpuCounter == null)
                return 0f;

            try
            {
                // 读取CPU使用率
                float currentUsage = processCpuCounter.NextValue();

                // 在多核系统上，进程CPU使用率可能需要标准化处理
                // Process "% Processor Time" 计数器返回的是相对于单个核心的百分比
                // 为了与任务管理器显示一致，需要除以逻辑处理器数量
                float normalizedUsage = currentUsage / processorCount;

                // 确保值在有效范围内
                return Math.Max(0f, Math.Min(100f, normalizedUsage));
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            systemCpuCounter?.Dispose();
            processCpuCounter?.Dispose();
        }
    }
}
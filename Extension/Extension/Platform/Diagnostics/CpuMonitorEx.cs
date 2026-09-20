using System;
using System.Diagnostics;
using System.Threading;
using System.Linq;

namespace Extension.Platform.Diagnostics
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
                catch (Exception ex) when (IsCounterUnavailable(ex))
                {
                    // 如果"% Processor Utility"不可用，回退到"% Processor Time"。
                    // 性能计数器在不同 Windows 版本/语言/权限下可用类别不同，回退本身是预期行为；
                    // 但要留痕，否则"一直在用回退计数器、数值与任务管理器对不上"会无从查起。
                    Debug.WriteLine(
                        $"[CpuMonitorEx] 'Processor Information\\% Processor Utility' 不可用，回退到 '% Processor Time'：{ex.Message}");
                    try
                    {
                        systemCpuCounter = new PerformanceCounter("Processor Information", "% Processor Time", "_Total");
                        systemCpuCounter.NextValue(); // 初始化
                    }
                    catch (Exception ex2) when (IsCounterUnavailable(ex2))
                    {
                        // 如果"Processor Information"不可用，回退到"Processor"
                        Debug.WriteLine(
                            $"[CpuMonitorEx] 'Processor Information' 不可用，回退到 'Processor'：{ex2.Message}");
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
            catch (Exception ex) when (IsCounterUnavailable(ex))
            {
                // 初始化失败后所有读取都返回 0f，属于"监控降级"而非崩溃，这个行为保留。
                // 但必须留痕：否则 CPU 数据恒为 0 时，无法区分"真的空闲"和"计数器根本没起来"。
                Debug.WriteLine($"[CpuMonitorEx] 性能计数器初始化失败，CPU 监控不可用：{ex.Message}");
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
                    catch (Exception ex) when (IsCounterUnavailable(ex))
                    {
                        // 跳过无法访问的实例（进程可能刚好退出，或权限不足），继续匹配下一个。
                        Debug.WriteLine($"[CpuMonitorEx] 无法访问 Process 计数器实例 '{instanceName}'，跳过：{ex.Message}");
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
            catch (Exception ex) when (IsCounterUnavailable(ex))
            {
                // 拿不到实例名时返回空串，由调用方回退到 ProcessName，这层降级保留。
                Debug.WriteLine($"[CpuMonitorEx] 获取进程实例名称失败，返回空串：{ex.Message}");
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
            catch (Exception ex) when (IsCounterUnavailable(ex))
            {
                // 读取失败返回 0f（监控降级），但留痕以区分"真的空闲"和"计数器读不到"。
                Debug.WriteLine($"[CpuMonitorEx] 读取系统 CPU 使用率失败，返回 0：{ex.Message}");
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
            catch (Exception ex) when (IsCounterUnavailable(ex))
            {
                Debug.WriteLine($"[CpuMonitorEx] 读取进程 CPU 使用率失败，返回 0：{ex.Message}");
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

        /// <summary>
        /// 判断异常是否属于"性能计数器不可用"这一类环境问题
        /// （类别不存在、权限不足、计数器已损坏、平台不支持等）。
        /// 这类异常可以按降级策略处理；其余异常说明代码有缺陷，继续抛出。
        /// </summary>
        private static bool IsCounterUnavailable(Exception ex) =>
            ex is InvalidOperationException
                or UnauthorizedAccessException
                or System.ComponentModel.Win32Exception
                or PlatformNotSupportedException
                or NotSupportedException
                or ArgumentException
                or System.Security.SecurityException;
    }
}
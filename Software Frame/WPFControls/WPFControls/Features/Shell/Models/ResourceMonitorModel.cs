// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：ResourceMonitorModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WPFControls.ProgramControl.Models
{
    /// <summary>
    /// ResourceMonitorModel 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// 继承或实现的契约：ObservableObject。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class ResourceMonitorModel : ObservableObject
    {
        // 程序CPU相关属性
        private double cpu;
        public double CPU
        {
            get => cpu;
            set => SetProperty(ref cpu, value);
        }

        private string cpuRate;
        public string CPURate
        {
            get => cpuRate;
            set => SetProperty(ref cpuRate, value);
        }

        // 系统CPU相关属性
        private double systemCpu;
        public double SystemCPU
        {
            get => systemCpu;
            set => SetProperty(ref systemCpu, value);
        }

        private string systemCpuRate;
        public string SystemCPURate
        {
            get => systemCpuRate;
            set => SetProperty(ref systemCpuRate, value);
        }

        // 程序内存相关属性
        private double memory;
        public double Memory
        {
            get => memory;
            set => SetProperty(ref memory, value);
        }

        private string memoryRate;
        public string MemoryRate
        {
            get => memoryRate;
            set => SetProperty(ref memoryRate, value);
        }

        // 系统内存相关属性
        private double systemMemory;
        public double SystemMemory
        {
            get => systemMemory;
            set => SetProperty(ref systemMemory, value);
        }

        private string systemMemoryRate;
        public string SystemMemoryRate
        {
            get => systemMemoryRate;
            set => SetProperty(ref systemMemoryRate, value);
        }

        // 磁盘相关属性
        private double disk;
        public double Disk
        {
            get => disk;
            set => SetProperty(ref disk, value);
        }

        private string diskRate;
        public string DiskRate
        {
            get => diskRate;
            set => SetProperty(ref diskRate, value);
        }
    }
}
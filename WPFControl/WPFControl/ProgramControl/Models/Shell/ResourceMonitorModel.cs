using CommunityToolkit.Mvvm.ComponentModel;

namespace WPFControl.ProgramControl.Models
{
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
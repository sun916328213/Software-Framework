// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：CapacityStatisticsModel、CapacityStatisticsData、CapacityStatisticsRow。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel、System.Collections.ObjectModel、System.ComponentModel、WPFControls.Charts.Models。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.ComponentModel;
using WPFControls.Charts.Models;

namespace WPFControls.ProgramControl.Models
{
    /// <summary>
    /// 产能统计数据模型
    /// </summary>
    public class CapacityStatisticsModel : ObservableObject
    {
        /// <summary>
        /// 产能统计数据集合
        /// </summary>
        public ObservableCollection<CapacityStatisticsRow> CapacityStatisticsDatas { get; set; }=new ObservableCollection<CapacityStatisticsRow>();

        /// <summary>
        /// 产量趋势X轴
        /// </summary>
        public ObservableCollection<string> AxisX
        {
            get => _axisX;
            set => SetProperty(ref _axisX, value);
        }
        private ObservableCollection<string> _axisX = new ObservableCollection<string>();

        /// <summary>
        /// 产量趋势折线图数据
        /// </summary>
        public ObservableCollection<ChartLineData> OutputTrend
        {
            get => _outputTrend;
            set => SetProperty(ref _outputTrend, value);
        }
        private ObservableCollection<ChartLineData> _outputTrend = new ObservableCollection<ChartLineData>();

        /// <summary>
        /// 良品/不良品饼图数据
        /// </summary>
        public ObservableCollection<ChartPieData> PieData { get; set; } = new ObservableCollection<ChartPieData>();


        int totalOutput, goodCount,defectCount;
        /// <summary>
        /// 区间总产量
        /// </summary>
        public int TotalOutput
        {
            get => totalOutput;
            set=>SetProperty(ref totalOutput, value);
        }

        

        /// <summary>
        /// 良品数
        /// </summary>
        public int GoodCount
        {
            get => goodCount;
            set =>SetProperty(ref goodCount, value);
        }

        /// <summary>
        /// 不良品数
        /// </summary>
        public int DefectCount
        {
            get => defectCount;
            set => SetProperty(ref defectCount, value);
        }

        double yieldRate, utilizationRate, runTime;
        /// <summary>
        /// 良率(%)
        /// </summary>
        public double YieldRate 
                    { get => yieldRate;
            set => SetProperty(ref yieldRate, value);
        }

        /// <summary>
        /// 稼动率(%)
        /// </summary>
        public double UtilizationRate
        {
            get => utilizationRate;
            set => SetProperty(ref utilizationRate, value);
        }

        /// <summary>
        /// 运行时长(h)
        /// </summary>
        public double RunTime
        {
            get => runTime;
            set => SetProperty(ref runTime, value);
        }

      

        private DateTime _startTime;
        private DateTime _endTime;

        public DateTime StartTime
        {
            get => _startTime;
            set => SetProperty(ref _startTime, value);
        }

        public DateTime EndTime
        {
            get => _endTime;
            set => SetProperty(ref _endTime, value);
        }
    }

    /// <summary>
    /// 产能统计数据结构体
    /// </summary>
    public class CapacityStatisticsData 
    {
        [DisplayName("0-1点合格数量")]public int OKCOunt1 { get; set; }

        [DisplayName("0-1点不良数量")]public int NGCount1 { get; set; }

        [DisplayName("1-2点合格数量")]public int OKCOunt2 { get; set; }

        [DisplayName("1-2点不良数量")]public int NGCount2 { get; set; }

        [DisplayName("2-3点合格数量")]public int OKCOunt3 { get; set; }

        [DisplayName("2-3点不良数量")]public int NGCount3 { get; set; }

        [DisplayName("3-4点合格数量")] public int OKCOunt4 { get; set; }

        [DisplayName("3-4点不良数量")] public int NGCount4 { get; set; }

        [DisplayName("4-5点合格数量")] public int OKCOunt5 { get; set; }

        [DisplayName("4-5点不良数量")] public int NGCount5 { get; set; }
 
        [DisplayName("5-6点合格数量")] public int OKCOunt6 { get; set; }

        [DisplayName("5-6点不良数量")] public int NGCount6 { get; set; }

        [DisplayName("6-7点合格数量")] public int OKCOunt7 { get; set; }

        [DisplayName("6-7点不良数量")] public int NGCount7 { get; set; }

        [DisplayName("7-8点合格数量")] public int OKCOunt8 { get; set; }

        [DisplayName("7-8点不良数量")] public int NGCount8 { get; set; }

        [DisplayName("8-9点合格数量")] public int OKCOunt9 { get; set; }

        [DisplayName("8-9点不良数量")] public int NGCount9 { get; set; }

        [DisplayName("9-10点合格数量")] public int OKCOunt10 { get; set; }

        [DisplayName("9-10点不良数量")] public int NGCount10 { get; set; }

        [DisplayName("10-11点合格数量")] public int OKCOunt11 { get; set; }

        [DisplayName("10-11点不良数量")] public int NGCount11 { get; set; }

        [DisplayName("11-12点合格数量")] public int OKCOunt12 { get; set; }

        [DisplayName("11-12点不良数量")] public int NGCount12 { get; set; }

        [DisplayName("12-13点合格数量")] public int OKCOunt13 { get; set; }

        [DisplayName("12-13点不良数量")] public int NGCount13 { get; set; }

        [DisplayName("13-14点合格数量")] public int OKCOunt14 { get; set; }

        [DisplayName("13-14点不良数量")] public int NGCount14 { get; set; }

        [DisplayName("14-15点合格数量")] public int OKCOunt15 { get; set; }

        [DisplayName("14-15点不良数量")] public int NGCount15 { get; set; }

        [DisplayName("15-16点合格数量")] public int OKCOunt16 { get; set; }

        [DisplayName("15-16点不良数量")] public int NGCount16 { get; set; }

        [DisplayName("16-17点合格数量")] public int OKCOunt17 { get; set; }

        [DisplayName("16-17点不良数量")] public int NGCount17 { get; set; }

        [DisplayName("17-18点合格数量")] public int OKCOunt18 { get; set; }

        [DisplayName("17-18点不良数量")] public int NGCount18 { get; set; }

        [DisplayName("18-19点合格数量")] public int OKCOunt19 { get; set; }

        [DisplayName("18-19点不良数量")] public int NGCount19 { get; set; }

        [DisplayName("19-20点合格数量")] public int OKCOunt20 { get; set; }

        [DisplayName("19-20点不良数量")] public int NGCount20 { get; set; }

        [DisplayName("20-21点合格数量")] public int OKCOunt21 { get; set; }

        [DisplayName("20-21点不良数量")] public int NGCount21 { get; set; }

        [DisplayName("21-22点合格数量")] public int OKCOunt22 { get; set; }

        [DisplayName("21-22点不良数量")] public int NGCount22 { get; set; }

        [DisplayName("22-23点合格数量")] public int OKCOunt23 { get; set; }

        [DisplayName("22-23点不良数量")] public int NGCount23 { get; set; }

        [DisplayName("23-24点合格数量")] public int OKCOunt24 { get; set; }

        [DisplayName("23-24点不良数量")] public int NGCount24 { get; set; }

    }


    /// <summary>
    /// 产能统计信息类，继承自CapacityStatisticsData，供页面与查询服务使用
    /// </summary>
    public class CapacityStatisticsRow : CapacityStatisticsData
    {
        public string ID { get; set; }
        public DateTime Time { get; set; }
    }
}
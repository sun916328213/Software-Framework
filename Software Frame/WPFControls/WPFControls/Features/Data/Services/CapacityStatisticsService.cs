// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：CapacityStatisticsService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls.Queries.Data、WPFControls.Controls.Diagnostics、WPFControls.Integration.Queries、WPFControls.ProgramControl.Models、WPFControls.ProgramControl.Presentation.Data。导入不代表所有类型都会在运行时加载。
using WPFControls.Queries.Data;
using WPFControls.Controls.Diagnostics;
using WPFControls.Integration.Queries;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Presentation.Data;


namespace WPFControls.ProgramControl.Services
{
    /// <summary>
    /// 产能统计服务类
    /// </summary>
    public class CapacityStatisticsService
    {
        private readonly TryCatchHelper _errors;
        private readonly ICapacityStatisticsQueryService<CapacityStatisticsRow> _query;

        private readonly ICapacityStatisticsWriteService<CapacityStatisticsRow> _writer;


        public CapacityStatisticsModel Model = new CapacityStatisticsModel();

       

        /// <summary>
        /// 初始化 CapacityStatisticsService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public CapacityStatisticsService(TryCatchHelper? errors,
            ICapacityStatisticsQueryService<CapacityStatisticsRow> query,
            ICapacityStatisticsWriteService<CapacityStatisticsRow> writer)
        {
            _errors = errors ?? new();

            _query = query;
            _writer = writer;
            Model.StartTime = DateTime.Now.Date;
            Model.EndTime = DateTime.Now.Date;

        }

        /// <summary>
        /// 记录一条产能统计结果，自动累加到对应天/小时的 OK/NG 计数
        /// </summary>
        /// <param name="time">发生时间</param>
        /// <param name="isOK">true=良品 false=不良品</param>
        public bool SaveStatistics(DateTime time, bool isOK) =>
            _errors.TryExecuteWithResult(() => _writer.Record(time, isOK));

        /// <summary>
        /// 按小时单点更新产能统计(适用于PLC 10s循环读取累计值,每小时只更新该小时列)
        /// 每条数据按其 Time 字段定位"当天 + 对应小时列",只更新该列的 OK/NG 值,不影响其它23列
        /// </summary>
        /// <param name="datas">产能统计数据列表(每条 Time 决定归属小时,OK/NG 取该小时列的值)</param>
        /// <param name="seconds">归属时间偏移量(秒),避免整点时刻数据归属错误</param>    
        public bool SaveStatistics(CapacityStatisticsRow data, int seconds = -10) =>
            _errors.TryExecuteWithResult(() => _writer.SetHourly(data, seconds));

        /// <summary>
        /// 保存产能统计数据到 Excel 文件
        /// </summary>
        public Task<bool> SaveStatistics(List<CapacityStatisticsRow> data) => CapacityStatisticsPresentation.Save(Model, data);

        /// <summary>
        /// 获取指定时间区间的产能统计数据
        /// </summary>
        public void GetStatistics() => CapacityStatisticsPresentation.Apply(Model, _query.Query(Model.StartTime, Model.EndTime));
    }
}

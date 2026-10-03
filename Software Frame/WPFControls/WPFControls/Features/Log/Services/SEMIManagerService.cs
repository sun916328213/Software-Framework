// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：SEMIManagerService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls.ProgramControl.Services、WPFControls、Prism.Ioc、SecsGem.Core、System、System.Collections.ObjectModel。导入不代表所有类型都会在运行时加载。
using WPFControls.ProgramControl.Services;
using WPFControls;
using Prism.Ioc;
using SecsGem.Core;
using System;
using System.Collections.ObjectModel;
using WPFControls.ProgramControl.Models;

namespace WPFControls.ProgramControl.Services.Log
{
    /// <summary>
    /// SEMIManagerService 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// </summary>
    internal class SEMIManagerService
    {
        public SEMIManagerModel Model { get; set; } = new SEMIManagerModel();

        private readonly ISecsGemConfiguration _configuration;

        /// <summary>
        /// 初始化 SEMIManagerService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public SEMIManagerService(ISecsGemConfiguration configuration) => _configuration = configuration;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ReportLoad。
        /// 正文中的关键协作调用包括 LoadReports；具体条件和先后次序见方法体。
        /// </summary>
        public void ReportLoad()
        {
            Model.Reports = new ObservableCollection<SecsGemReportDefinition>(_configuration.LoadReports());
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 EventLoad。
        /// 正文中的关键协作调用包括 LoadEvents；具体条件和先后次序见方法体。
        /// </summary>
        public void EventLoad()
        {
            Model.Events = new ObservableCollection<SecsGemEventDefinition>(_configuration.LoadEvents());
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ReportSave。
        /// 正文中的关键协作调用包括 SaveReports；具体条件和先后次序见方法体。
        /// </summary>
        public void ReportSave() => _configuration.SaveReports();
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 EventSave。
        /// 正文中的关键协作调用包括 SaveEvents；具体条件和先后次序见方法体。
        /// </summary>
        public void EventSave() => _configuration.SaveEvents();

    }
}

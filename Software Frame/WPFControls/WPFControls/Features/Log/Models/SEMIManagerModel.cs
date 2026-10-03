// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：SEMIManagerModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel、SecsGem.Core、System、System.Collections.Generic、System.Collections.ObjectModel。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using SecsGem.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPFControls.ProgramControl.Models
{
    /// <summary>
    /// SEMIManagerModel 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// 继承或实现的契约：ObservableObject。具体约束按这些契约及各成员实现解释。
    /// </summary>
    internal class SEMIManagerModel:ObservableObject
    {
        private ObservableCollection<SecsGemReportDefinition> iReports;
        public ObservableCollection<SecsGemReportDefinition> Reports
        {
            get => iReports;
            set => SetProperty(ref iReports, value);
        }

        private ObservableCollection<SecsGemEventDefinition> iEvents;
        public ObservableCollection<SecsGemEventDefinition> Events
        {
            get => iEvents;
            set => SetProperty(ref iEvents, value);
        }
    }
}

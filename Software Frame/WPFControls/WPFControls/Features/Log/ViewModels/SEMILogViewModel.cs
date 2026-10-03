// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：SEMILogViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel、WPFControls.ProgramControl.Services、System.Collections.ObjectModel。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using WPFControls.ProgramControl.Services;
using System.Collections.ObjectModel;

namespace WPFControls.ProgramControl.ViewModels
{
    /// <summary>
    /// SEMILogViewModel 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// 继承或实现的契约：ObservableObject。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class SEMILogViewModel: ObservableObject
    {
        readonly SEMILogService Service;

        public ObservableCollection<string> ItemsSource => Service.Model.DataCollection;

        /// <summary>
        /// 初始化 SEMILogViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public SEMILogViewModel(SEMILogService model)
        {
            Service = model;
        }
    }
}

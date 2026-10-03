// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：ControlViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls.ProgramControl.ViewModels、WPFControls、CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input、WPFControls.ProgramControl.Models、WPFControls.ProgramControl.Services。导入不代表所有类型都会在运行时加载。
using WPFControls.ProgramControl.ViewModels;
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Services;
using System.Collections.ObjectModel;

namespace WPFControls.ProgramControl.ViewModels.Shell
{
    /// <summary>
    /// ControlViewModel 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// 继承或实现的契约：ObservableObject。具体约束按这些契约及各成员实现解释。
    /// </summary>
    class ControlViewModel : ObservableObject
    {
        private readonly ControlService Service;


        public ObservableCollection<CControlItem> NavigationItems => Service.ControlItems;


        private CControlItem selectNavigationItems;

        public CControlItem SelectNavigationItems
        {
            get => selectNavigationItems;
            set => SetProperty(ref selectNavigationItems, value);
        }

        public RelayCommand SelectCommand { get; }

        /// <summary>
        /// 初始化 ControlViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Invoke；具体条件和先后次序见方法体。
        /// </summary>
        public ControlViewModel(ControlService model)
        {
            this.Service = model;

            SelectCommand = new RelayCommand(() =>
            {
                if (SelectNavigationItems?.Action != null)
                {
                    SelectNavigationItems.Action.Invoke();
                }
            });
        }
    }
}

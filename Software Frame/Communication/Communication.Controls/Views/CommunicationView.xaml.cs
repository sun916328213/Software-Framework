// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：CommunicationView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Controls、System.Collections.Specialized、System.Windows.Controls。导入不代表所有类型都会在运行时加载。
using Communication.Controls;
using System.Collections.Specialized;
using System.Windows.Controls;

namespace Communication.Controls.Views;

/// <summary>
/// 通讯测试页：设备列表 + 读写/收发 + 日志，全部经 CommunicationNew 一套接口。
/// DataContext 由 Prism 的导航注册（RegisterNavigationView）自动装配。
/// </summary>
public partial class CommunicationView : UserControl
{
    private bool _autoScrollHooked;

    /// <summary>
    /// 初始化 CommunicationView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 InitializeComponent、HookAutoScroll；具体条件和先后次序见方法体。
    /// </summary>
    public CommunicationView()
    {
        InitializeComponent();
        Loaded += (_, _) => HookAutoScroll();
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 HookAutoScroll。
    /// 正文中的关键协作调用包括 ScrollIntoView；具体条件和先后次序见方法体。
    /// </summary>
    private void HookAutoScroll()
    {
        if (_autoScrollHooked || DataContext is not ViewModels.CommunicationViewModel vm)
        {
            return;
        }

        _autoScrollHooked = true;
        vm.Logs.CollectionChanged += (_, e) =>
        {
            if (vm.AutoScrollLog && e.Action == NotifyCollectionChangedAction.Add)
            {
                LogList.ScrollIntoView(e.NewItems![^1]);
            }
        };
    }
}

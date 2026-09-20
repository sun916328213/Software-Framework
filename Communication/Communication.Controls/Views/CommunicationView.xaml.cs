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

    public CommunicationView()
    {
        InitializeComponent();
        Loaded += (_, _) => HookAutoScroll();
    }

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

// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：PortDialogService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、WPFCustomControls.PropertyGrid.Core.Services、WPFCustomControls.PropertyGrid.Core.Models、WPFCustomControls.PropertyGrid.Core.ViewModels、WPFCustomControls.PropertyGrid.Views。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using WPFCustomControls.PropertyGrid.Core.Services;
using WPFCustomControls.PropertyGrid.Core.Models;
using WPFCustomControls.PropertyGrid.Core.ViewModels;
using WPFCustomControls.PropertyGrid.Views;

namespace WPFCustomControls.PropertyGrid.Services;

/// <summary>
/// IPortDialogService 默认实现：按端口方向包装 InputPortEditDialog（输入）/ OutputPortEditDialog（输出）窗口。
/// 负责对话框创建、Owner 设置与结果转换，隔离 ViewModel 与具体 View。
/// </summary>
public class PortDialogService : IPortDialogService
{
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ShowAddPortDialog。
    /// 正文中的关键协作调用包括 ShowDialog、ToInputResult；具体条件和先后次序见方法体。
    /// </summary>
    public PortDialogResult? ShowAddPortDialog(PortDialogRequest request)
    {
        if (!request.IsInputPort)
        {
            var vm = new OutputPortEditViewModel(request.AvailableProperties);
            var dialog = new OutputPortEditDialog(vm)
            {
                Title = request.Title,
                Owner = System.Windows.Application.Current.MainWindow
            };
            return dialog.ShowDialog() == true
                ? new PortDialogResult
                {
                    PortName = vm.PortName,
                    BoundPropertyName = vm.BoundPropertyName,
                    IsToolProperty = vm.IsToolProperty
                }
                : null;
        }

        var inputVm = new InputPortEditViewModel(
            request.UpstreamNodes,
            request.GetUpstreamPorts,
            request.CurrentPortName,
            null, null);
        var inputDialog = new InputPortEditDialog(inputVm)
        {
            Title = request.Title,
            Owner = System.Windows.Application.Current.MainWindow
        };
        return inputDialog.ShowDialog() == true ? ToInputResult(inputVm) : null;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ShowModifyBindingDialog。
    /// 正文中的关键协作调用包括 Empty、IsNullOrEmpty、ShowDialog、ToInputResult；具体条件和先后次序见方法体。
    /// </summary>
    public PortDialogResult? ShowModifyBindingDialog(PortDialogRequest request)
    {
        var vm = new InputPortEditViewModel(
            request.UpstreamNodes,
            request.GetUpstreamPorts ?? (_ => Array.Empty<string>()),
            request.CurrentPortName,
            request.CurrentUpstreamNodeId,
            request.CurrentUpstreamPortName);
        var dialog = new InputPortEditDialog(vm)
        {
            Title = string.IsNullOrEmpty(request.CurrentPortName) ? "添加端口绑定" : "修改端口绑定",
            Owner = System.Windows.Application.Current.MainWindow
        };
        return dialog.ShowDialog() == true ? ToInputResult(vm) : null;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ToInputResult。
    /// </summary>
    private static PortDialogResult ToInputResult(InputPortEditViewModel vm) => new()
    {
        PortName = vm.PortName,
        IsUpstreamData = vm.IsUpstreamData,
        UpstreamNodeId = vm.SelectedUpstreamNode?.NodeId,
        UpstreamPortName = vm.SelectedUpstreamPort
    };
}
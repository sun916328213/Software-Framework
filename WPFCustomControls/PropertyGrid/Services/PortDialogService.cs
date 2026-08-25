using WPFCustomControls.PropertyGrid.Models;
using WPFCustomControls.PropertyGrid.ViewModels;
using WPFCustomControls.PropertyGrid.Views;

namespace WPFCustomControls.PropertyGrid.Services;

/// <summary>
/// IPortDialogService 默认实现：按端口方向包装 InputPortEditDialog（输入）/ OutputPortEditDialog（输出）窗口。
/// 负责对话框创建、Owner 设置与结果转换，隔离 ViewModel 与具体 View。
/// </summary>
public class PortDialogService : IPortDialogService
{
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

    private static PortDialogResult ToInputResult(InputPortEditViewModel vm) => new()
    {
        PortName = vm.PortName,
        IsUpstreamData = vm.IsUpstreamData,
        UpstreamNodeId = vm.SelectedUpstreamNode?.NodeId,
        UpstreamPortName = vm.SelectedUpstreamPort
    };
}
// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：OutputPortEditViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections.ObjectModel、CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input。导入不代表所有类型都会在运行时加载。
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WPFCustomControls.PropertyGrid.Core.ViewModels;

/// <summary>输出端口编辑对话框 ViewModel（绑定工具属性 / 自定义端口两种模式）。</summary>
public partial class OutputPortEditViewModel : ObservableObject
{
    [ObservableProperty] private string? _portName;

    [ObservableProperty] private string? _boundPropertyName;

    [ObservableProperty] private bool _isToolProperty = true;

    [ObservableProperty] private string? _customPortName;

    public ObservableCollection<string> AvailableProperties { get; } = new();
    public bool HasAvailableProperties => AvailableProperties.Count > 0;

    /// <summary>请求关闭对话框，参数为 DialogResult 值。</summary>
    public event Action<bool?>? CloseRequested;

    /// <summary>
    /// 初始化 OutputPortEditViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
    /// </summary>
    public OutputPortEditViewModel(IEnumerable<string> availableProperties)
    {
        foreach (var p in availableProperties)
            AvailableProperties.Add(p);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Confirm。
    /// 正文中的关键协作调用包括 IsNullOrWhiteSpace；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand]
    private void Confirm()
    {
        if (IsToolProperty)
        {
            if (BoundPropertyName == null) return;
            PortName = BoundPropertyName;
        }
        else
        {
            PortName = CustomPortName?.Trim();
            if (string.IsNullOrWhiteSpace(PortName)) return;
            BoundPropertyName = null;
        }
        CloseRequested?.Invoke(true);
    }

    /// <summary>
    /// 请求取消当前操作；对应入口为 Cancel。
    /// </summary>
    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);
}
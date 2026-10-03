// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：PortRowViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input、System.Collections.ObjectModel。导入不代表所有类型都会在运行时加载。
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace WPFCustomControls.PropertyGrid.Core.ViewModels;

/// <summary>
/// PortRowViewModel 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
/// 继承或实现的契约：ObservableObject。具体约束按这些契约及各成员实现解释。
/// </summary>
public partial class PortRowViewModel : ObservableObject
{
    [ObservableProperty] private string _id = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string? _boundPropertyName;
    [ObservableProperty] private bool _isInput;
    [ObservableProperty] private object? _value;

    /// <summary>该端口行的只读状态（由父级区域锁定控制）</summary>
    [ObservableProperty] private bool _isReadOnly = true;

    /// <summary>端口当前值的字符串表示，供 UI 绑定</summary>
    public string ValueText => Value?.ToString() ?? "(空)";

    /// <summary>是否为自定义端口（未绑定属性）</summary>
    public bool IsCustomPort => string.IsNullOrEmpty(BoundPropertyName);

    /// <summary>是否显示为只读文本（已绑定数据/已绑定属性的端口 显示 TextBlock）</summary>
    public bool ShowAsTextBlock => IsBound || !IsCustomPort;

    /// <summary>是否显示为可编辑文本框（未绑定的自定义输入端口 显示 TextBox）</summary>
    public bool ShowAsTextBox => !IsBound && IsInput && IsCustomPort;

    /// <summary>是否显示为下拉框（未绑定属性的输出端口 显示 ComboBox）</summary>
    public bool ShowAsComboBox => !IsBound && !IsInput && IsCustomPort;

    /// <summary>有预设选项时显示 ComboBox，否则显示 TextBox</summary>
    public bool HasAvailableProperties => AvailableProperties.Count > 0;
    public List<string> AvailableProperties { get; } = new();

    // ── 数据绑定状态 ──

    /// <summary>该端口是否已绑定到上游节点的属性</summary>
    [ObservableProperty] private bool _isBound;

    /// <summary>绑定状态描述文本，如 "已绑定: NodeA.Value"</summary>
    [ObservableProperty] private string? _bindingStatusText;

    /// <summary>该端口是否处于未绑定状态（仅输入端口使用）</summary>
    public bool IsUnbound => IsInput && !IsBound;

    /// <summary>绑定的上游节点 ID（用于修改对话框预填）</summary>
    public string? BoundUpstreamNodeId { get; set; }

    /// <summary>绑定的上游属性名（用于修改对话框预填）</summary>
    public string? BoundUpstreamProperty { get; set; }

    /// <summary>修改绑定请求事件（用于已绑定端口修改绑定）</summary>
    public event Action? ModifyBindingRequested;

    /// <summary>绑定请求事件（用于未绑定端口发起绑定）</summary>
    public event Action? BindRequested;

    /// <summary>解除绑定请求事件</summary>
    public event Action? UnbindRequested;

    /// <summary>
    /// 初始化 PortRowViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public PortRowViewModel() { }

    /// <summary>
    /// 初始化 PortRowViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public PortRowViewModel(string id, string name, bool isInput, string? boundProp = null)
    {
        Id = id;
        Name = name;
        IsInput = isInput;
        BoundPropertyName = boundProp;
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnBoundPropertyNameChanged。
    /// 正文中的关键协作调用包括 OnPropertyChanged、nameof；具体条件和先后次序见方法体。
    /// </summary>
    partial void OnBoundPropertyNameChanged(string? oldValue, string? newValue)
    {
        OnPropertyChanged(nameof(IsCustomPort));
        OnPropertyChanged(nameof(ShowAsTextBlock));
        OnPropertyChanged(nameof(ShowAsTextBox));
        OnPropertyChanged(nameof(ShowAsComboBox));
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnIsInputChanged。
    /// 正文中的关键协作调用包括 OnPropertyChanged、nameof；具体条件和先后次序见方法体。
    /// </summary>
    partial void OnIsInputChanged(bool oldValue, bool newValue)
    {
        OnPropertyChanged(nameof(ShowAsTextBlock));
        OnPropertyChanged(nameof(ShowAsTextBox));
        OnPropertyChanged(nameof(ShowAsComboBox));
        OnPropertyChanged(nameof(IsUnbound));
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnIsBoundChanged。
    /// 正文中的关键协作调用包括 OnPropertyChanged、nameof；具体条件和先后次序见方法体。
    /// </summary>
    partial void OnIsBoundChanged(bool oldValue, bool newValue)
    {
        OnPropertyChanged(nameof(ShowAsTextBlock));
        OnPropertyChanged(nameof(ShowAsTextBox));
        OnPropertyChanged(nameof(ShowAsComboBox));
        OnPropertyChanged(nameof(IsUnbound));
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnValueChanged。
    /// 正文中的关键协作调用包括 OnPropertyChanged、nameof；具体条件和先后次序见方法体。
    /// </summary>
    partial void OnValueChanged(object? oldValue, object? newValue)
    {
        OnPropertyChanged(nameof(ValueText));
    }

    /// <summary>是否为输入端口（用于 UI 判断）</summary>
    public bool IsInputPort => IsInput;

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ModifyBinding。
    /// </summary>
    [RelayCommand]
    private void ModifyBinding() => ModifyBindingRequested?.Invoke();

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Bind。
    /// </summary>
    [RelayCommand]
    private void Bind() => BindRequested?.Invoke();

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Unbind。
    /// </summary>
    [RelayCommand]
    private void Unbind() => UnbindRequested?.Invoke();

    /// <summary>
    /// 执行删除操作；对应入口为 Delete。
    /// </summary>
    [RelayCommand]
    private void Delete() => DeleteRequested?.Invoke();

    public event Action? DeleteRequested;

    /// <summary>填充可用选项后调用，通知 UI 刷新 ComboBox/TextBox 切换</summary>
    public void NotifyPropertiesReady()
    {
        OnPropertyChanged(nameof(HasAvailableProperties));
        OnPropertyChanged(nameof(AvailableProperties));
    }
}
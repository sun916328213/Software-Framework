using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace WPFCustomControls.PropertyGrid.ViewModels;

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

    public PortRowViewModel() { }

    public PortRowViewModel(string id, string name, bool isInput, string? boundProp = null)
    {
        Id = id;
        Name = name;
        IsInput = isInput;
        BoundPropertyName = boundProp;
    }

    partial void OnBoundPropertyNameChanged(string? oldValue, string? newValue)
    {
        OnPropertyChanged(nameof(IsCustomPort));
        OnPropertyChanged(nameof(ShowAsTextBlock));
        OnPropertyChanged(nameof(ShowAsTextBox));
        OnPropertyChanged(nameof(ShowAsComboBox));
    }

    partial void OnIsInputChanged(bool oldValue, bool newValue)
    {
        OnPropertyChanged(nameof(ShowAsTextBlock));
        OnPropertyChanged(nameof(ShowAsTextBox));
        OnPropertyChanged(nameof(ShowAsComboBox));
        OnPropertyChanged(nameof(IsUnbound));
    }

    partial void OnIsBoundChanged(bool oldValue, bool newValue)
    {
        OnPropertyChanged(nameof(ShowAsTextBlock));
        OnPropertyChanged(nameof(ShowAsTextBox));
        OnPropertyChanged(nameof(ShowAsComboBox));
        OnPropertyChanged(nameof(IsUnbound));
    }

    partial void OnValueChanged(object? oldValue, object? newValue)
    {
        OnPropertyChanged(nameof(ValueText));
    }

    /// <summary>是否为输入端口（用于 UI 判断）</summary>
    public bool IsInputPort => IsInput;

    [RelayCommand]
    private void ModifyBinding() => ModifyBindingRequested?.Invoke();

    [RelayCommand]
    private void Bind() => BindRequested?.Invoke();

    [RelayCommand]
    private void Unbind() => UnbindRequested?.Invoke();

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
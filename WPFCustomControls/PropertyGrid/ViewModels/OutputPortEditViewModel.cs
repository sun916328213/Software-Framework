using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WPFCustomControls.PropertyGrid.ViewModels;

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

    public OutputPortEditViewModel(IEnumerable<string> availableProperties)
    {
        foreach (var p in availableProperties)
            AvailableProperties.Add(p);
    }

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

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);
}

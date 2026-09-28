using System.Windows;
using System.Windows.Controls;

namespace Secs.Controls.Views;

/// <summary>可由工作台宿主绑定状态的 SECS/GEM 概览视图。</summary>
public partial class SecsOverviewView : UserControl
{
    public static readonly DependencyProperty ConnectionStatusProperty =
        DependencyProperty.Register(nameof(ConnectionStatus), typeof(string), typeof(SecsOverviewView),
            new PropertyMetadata("未配置连接"));

    public static readonly DependencyProperty ModeStatusProperty =
        DependencyProperty.Register(nameof(ModeStatus), typeof(string), typeof(SecsOverviewView),
            new PropertyMetadata("未知"));

    public static readonly DependencyProperty DeviceStatusProperty =
        DependencyProperty.Register(nameof(DeviceStatus), typeof(string), typeof(SecsOverviewView),
            new PropertyMetadata("未知"));

    public string ConnectionStatus
    {
        get => (string)GetValue(ConnectionStatusProperty);
        set => SetValue(ConnectionStatusProperty, value);
    }

    public string ModeStatus
    {
        get => (string)GetValue(ModeStatusProperty);
        set => SetValue(ModeStatusProperty, value);
    }

    public string DeviceStatus
    {
        get => (string)GetValue(DeviceStatusProperty);
        set => SetValue(DeviceStatusProperty, value);
    }

    public SecsOverviewView() => InitializeComponent();
}

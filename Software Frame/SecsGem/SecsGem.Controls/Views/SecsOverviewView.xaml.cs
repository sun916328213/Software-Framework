// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SecsOverviewView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Controls、System.Windows、System.Windows.Controls。导入不代表所有类型都会在运行时加载。
using SecsGem.Controls;
using System.Windows;
using System.Windows.Controls;

namespace SecsGem.Controls.Views;

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

    /// <summary>
    /// 初始化 SecsOverviewView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
    /// </summary>
    public SecsOverviewView() => InitializeComponent();
}

using System.ComponentModel;
using System.Windows;
using MotionControl.Workbench.ViewModels;

namespace MotionControl.Workbench.Views;

/// <summary>
/// Workbench 主窗：设备选择、单轴/多轴验证页的组合根。
/// <para>只做视图接线（把 <see cref="MainViewModel.SelectedCard"/> 推到复用控件），不承载业务逻辑。</para>
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    /// <summary>初始化主窗。</summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelChanged;
        Closing += (_, _) => _viewModel.Dispose();
    }

    private void OnConnect(object sender, RoutedEventArgs e)
    {
        _viewModel.Connect();
        PushCard();
    }

    private void OnDisconnect(object sender, RoutedEventArgs e)
    {
        _viewModel.Disconnect();
        PushCard();
    }

    private void OnAxisSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        => PushAxis();

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedCard))
            PushCard();
    }

    private void PushCard()
    {
        var card = _viewModel.SelectedCard;
        StatusControl.MotionCard = card;
        JogControl.MotionCard = card;
        OperationControl.MotionCard = card;
        OverviewControl.MotionCard = card;
        PushAxis();
    }

    private void PushAxis()
    {
        var axis = _viewModel.SelectedAxisIndex;
        StatusControl.AxisIndex = axis;
        JogControl.AxisIndex = axis;
        OperationControl.AxisIndex = axis;
    }
}

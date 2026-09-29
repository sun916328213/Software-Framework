using System.ComponentModel;
using System.Runtime.CompilerServices;
using MotionControl.Core.Abstractions;
using MotionControl.Core.Definitions;
using MotionControl.Runtime.Services;

namespace MotionControl.Workbench.ViewModels;

/// <summary>
/// Workbench 连接与设备选择状态。
/// <para>仅为人工调试/联调宿主，不对外提供业务 API，不可被其他业务项目依赖。</para>
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly MotionManager _manager = new();
    private EMotionCardType _selectedVendor = EMotionCardType.Simulation;
    private int _axisCount = 2;
    private string _ipAddress = "127.0.0.1";
    private IMotionCard? _selectedCard;
    private int[] _axisIndices = [0, 1];
    private int _selectedAxisIndex;
    private string _statusMessage = "未连接";
    private bool _isConnected;
    private bool _disposed;

    /// <summary>可选厂商（含 Simulation；真实厂商无硬件时连接失败并显示诊断信息）。</summary>
    public EMotionCardType[] Vendors { get; } = Enum.GetValues<EMotionCardType>();

    /// <summary>当前选择的厂商。</summary>
    public EMotionCardType SelectedVendor
    {
        get => _selectedVendor;
        set => SetProperty(ref _selectedVendor, value);
    }

    /// <summary>创建调试卡的轴数（1~16）。</summary>
    public int AxisCount
    {
        get => _axisCount;
        set => SetProperty(ref _axisCount, value);
    }

    /// <summary>网络型控制器的 IP 地址。</summary>
    public string IpAddress
    {
        get => _ipAddress;
        set => SetProperty(ref _ipAddress, value ?? string.Empty);
    }

    /// <summary>当前连接的卡（未连接时为 null）。</summary>
    public IMotionCard? SelectedCard
    {
        get => _selectedCard;
        private set => SetProperty(ref _selectedCard, value);
    }

    /// <summary>当前卡的轴索引集合（供单轴页选择）。</summary>
    public int[] AxisIndices
    {
        get => _axisIndices;
        private set => SetProperty(ref _axisIndices, value);
    }

    /// <summary>单轴页当前轴。</summary>
    public int SelectedAxisIndex
    {
        get => _selectedAxisIndex;
        set => SetProperty(ref _selectedAxisIndex, value);
    }

    /// <summary>连接/断开状态说明（含失败诊断）。</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>是否已连接。</summary>
    public bool IsConnected
    {
        get => _isConnected;
        private set => SetProperty(ref _isConnected, value);
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>按当前选择创建并连接设备（异常只写状态，不外抛）。</summary>
    public void Connect()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (AxisCount < 1 || AxisCount > 16)
        {
            Fail("轴数必须在 1 到 16 之间。");
            return;
        }

        try
        {
            var parameter = new MotionCardParam
            {
                Name = "Workbench",
                CardType = SelectedVendor,
                IpAddress = IpAddress,
            };
            for (var i = 0; i < AxisCount; i++)
                parameter.Axises.Add(new AxisParam { Name = $"Axis{i}" });

            _manager.Config([parameter]);
            var card = _manager[0];
            var result = card.ConnectResult();
            if (result.IsSuccess)
            {
                SelectedCard = card;
                AxisIndices = Enumerable.Range(0, card.AxisCount).ToArray();
                SelectedAxisIndex = 0;
                StatusMessage = $"已连接：{card.Name}（{SelectedVendor}，{card.AxisCount} 轴）。";
                IsConnected = true;
            }
            else
            {
                Fail($"连接失败：{result.Message ?? result.Code.ToString()}");
            }
        }
        catch (Exception ex)
        {
            Fail($"连接异常：{ex.Message}");
        }
    }

    /// <summary>断开全部设备并清空选择。</summary>
    public void Disconnect()
    {
        try
        {
            _manager.DisconnectAllResult();
        }
        catch
        {
            // 调试宿主断开阶段继续收尾，不外抛。
        }
        finally
        {
            SelectedCard = null;
            AxisIndices = [];
            IsConnected = false;
            StatusMessage = "已断开";
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        try
        {
            _manager.Dispose();
        }
        catch
        {
            // 关闭期清理不外抛。
        }
    }

    private void Fail(string message)
    {
        SelectedCard = null;
        AxisIndices = [];
        IsConnected = false;
        StatusMessage = message;
    }

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

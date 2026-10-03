// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：IDeviceProfile、DevicePageModelBase。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Controls、System.Collections.ObjectModel、System.IO、CommunityToolkit.Mvvm.ComponentModel、WPFCustomControls.Controls。导入不代表所有类型都会在运行时加载。
using Device.Controls;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using WPFCustomControls.Controls;

namespace Device.Controls.Models;

/// <summary>设备档案的共同形状：页面基类需要按 Name/DriverId 做去重、回填与指纹。</summary>
public interface IDeviceProfile
{
    string Name { get; set; }
    string DriverId { get; set; }
}

/// <summary>
/// 设备页状态 Model 基类：档案集合 + 驱动目录 + 连接状态（一页一 JSON 文件约定）。
/// 各类别页在其上追加自己的展示状态。
/// </summary>
public abstract partial class DevicePageModelBase<TProfile> : ConfigManagerModel<TProfile>
    where TProfile : class, IDeviceProfile
{
    /// <summary>
    /// 初始化 DevicePageModelBase 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 Combine；具体条件和先后次序见方法体。
    /// </summary>
    protected DevicePageModelBase(string fileName)
    {
        FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
        FileName = fileName;
    }

    [ObservableProperty]
    private TProfile? selectedProfile;

    [ObservableProperty]
    private bool isConnected;

    [ObservableProperty]
    private string statusText = "未连接";

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnSelectedProfileChanged。
    /// </summary>
    partial void OnSelectedProfileChanged(TProfile? value)
    {
        // 档案切换 = 连接要素变更，状态复位；实例由流程按指纹重建。
        IsConnected = false;
        StatusText = "未连接";
    }

    /// <summary>驱动下拉数据（工厂 DriverId 字符串，与档案/JSON 存的值同一套，不做中文名映射）。</summary>
    public ObservableCollection<string> Drivers { get; } = [];

    /// <summary>当前档案（空即报“请先添加”）。</summary>
    public TProfile SelectedProfileOrThrow() =>
        SelectedProfile ?? throw new InvalidOperationException("请先添加设备档案");
}

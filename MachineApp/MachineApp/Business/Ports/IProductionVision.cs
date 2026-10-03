// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：IProductionVision。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MachineApp.Business.Models、HalconDotNet、MachineApp.Business.Station、System.Windows、WPFControls.ProgramControl.Models。导入不代表所有类型都会在运行时加载。
using MachineApp.Business.Models;
using HalconDotNet;
using MachineApp.Business.Station;
using System.Windows;
using WPFControls.ProgramControl.Models;

namespace MachineApp.Business.Ports;

/// <summary>Operations used by station flows. Existing image/geometry representations are preserved.</summary>
/// <remarks>
/// IProductionVision 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
/// </remarks>
public interface IProductionVision
{
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 GrabImage。
    /// </summary>
    HObject GrabImage(string id, string station, VisionInspectionItem item, bool isSaveImg = true);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 GrabImages。
    /// </summary>
    Task<List<HObject>> GrabImages(string id, string station, int camera, VisionInspectionItem item, int triggernum);
    /// <summary>
    /// 读取当前实现提供的数据；对应入口为 ReadFOSBID。
    /// </summary>
    string ReadFOSBID(HObject image);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 DetectAccessory。
    /// </summary>
    AccessoryInfo DetectAccessory(HObject image, string id);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 HICircle。
    /// </summary>
    List<CCircleInfo> HICircle(HObject image, string id, int pCardType = 0);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CaliberTemplateMatch。
    /// </summary>
    Point CaliberTemplateMatch(HObject image);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Caliber。
    /// </summary>
    void Caliber(List<Point> camera, List<Point> robot);
    /// <summary>
    /// 读取当前实现提供的数据；对应入口为 ReadLabel。
    /// </summary>
    string ReadLabel(HObject image, string id);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 HeatSealDetect。
    /// </summary>
    List<int> HeatSealDetect(List<HObject> images);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ALDetect。
    /// </summary>
    List<Point?> ALDetect(HObject first, HObject second, string id);
}

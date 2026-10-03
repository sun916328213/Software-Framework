// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：ProductionFlowCameraProvider。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Cameras、FlowNodes.Device。导入不代表所有类型都会在运行时加载。
using Device.Core.Cameras;
using FlowNodes.Device;

namespace MachineApp.Infra.Device;

/// <summary>将节点的按名查询映射到既有生产设备会话；相机所有权仍属于该会话。</summary>
public sealed class ProductionFlowCameraProvider(ProductionDeviceSession session) : ICameraProvider
{
    /// <inheritdoc />
    public IReadOnlyList<string> Names => session.Cameras.Select(camera => camera.Name).ToArray();

    /// <inheritdoc />
    /// <remarks>
    /// 获取当前实现提供的值或对象；对应入口为 Get。
    /// 正文中的关键协作调用包括 Camera；具体条件和先后次序见方法体。
    /// </remarks>
    public ICamera Get(string name) => session.Camera(name);
}

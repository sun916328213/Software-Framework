// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
// 本文件定义：WPFControlsComposition。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Controls、DataManager.Controls.Models、Prism.Modularity、Prism.Ioc。导入不代表所有类型都会在运行时加载。
using DataManager.Controls;
using DataManager.Controls.Models;
using Prism.Modularity;
using Prism.Ioc;

namespace WPFControls;

/// <summary>装配可复用页面模块；页面选择、数据库实体和硬件启动由宿主负责。</summary>
public static class WPFControlsComposition
{
    /// <summary>在各页面模块注册前固定节点来源，允许宿主预先替换目录。</summary>
    public static void RegisterServices(IContainerRegistry registry)
    {
        // 只使用明确的节点来源，避免扫描进程里所有程序集时把测试节点或其他宿主节点混入生产目录。
        // 宿主若预先注册了自己的目录，此处保留它；因此应在页面模块初始化之前调用本方法。
        ArgumentNullException.ThrowIfNull(registry);
        if (!registry.IsRegistered<FlowEditor.Controls.Discovery.IFlowNodeCatalog>())
            registry.RegisterInstance<FlowEditor.Controls.Discovery.IFlowNodeCatalog>(
                FlowNodes.Integration.FlowNodesComposition.CreateNodeCatalog());
    }

    /// <summary>
    /// 配置当前对象或组合入口；对应入口为 ConfigureModules。
    /// 正文中的关键协作调用包括 ThrowIfNull、AddModule；具体条件和先后次序见方法体。
    /// </summary>
    public static void ConfigureModules<TSqlEntity, TExcelEntity>(IModuleCatalog catalog)
        where TSqlEntity : class, ISqlEntity, new()
        where TExcelEntity : class, IExcelTestEntity, new()
    {
        // 泛型实体来自机型宿主，通用页面库只要求其实现数据管理页面的契约，不固定具体表结构。
        // 这里只加入可用模块，不连接设备、不执行启动脚本，也不决定实际打开哪些页面。
        ArgumentNullException.ThrowIfNull(catalog);
        catalog.AddModule<Device.Controls.DeviceControlsModule>();
        catalog.AddModule<Communication.Controls.CommunicationModule>();
        catalog.AddModule<MotionControl.Controls.MotionControlsModule>();
        catalog.AddModule<Vision.Controls.VisionControlsModule>();
        catalog.AddModule<DataManagerModule<TSqlEntity, TExcelEntity>>();
        catalog.AddModule<SecsGem.Controls.SecsGemModule>();
        catalog.AddModule<WPFCustomControls.WPFCustomControlsModule>();
        catalog.AddModule<FlowEditor.Controls.FlowEditorModule>();
        catalog.AddModule<ScriptEditor.Controls.ScriptEditorModule>();
        catalog.AddModule<WPFControlsModule>();
    }
}

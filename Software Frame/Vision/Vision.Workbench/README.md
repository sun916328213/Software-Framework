# Vision.Workbench

独立的**视觉流程薄宿主**，不是旧单工具调试界面，也不并入FlowNodes.Workbench。

## 新引擎启动配置（P2第四批）

输出/发布目录中的vision-engine.json必须存在，启动时读取为不可变选择，不监听热更新；修改后重启。默认visionBackendId为opencv、aiBackendId/aiDeviceId为null。传统视觉也可选择halcon；AI目前只接受halcon与显式cpu。重复/未知字段、缺失必需字段、非法类型/设备组合及尚未接通后端均明确报错，不静默默认或回退。

Controls的RegisterVisionEngine负责可复用Prism装配，宿主只给配置并拥有返回的异步释放句柄。注册和关闭未使用句柄都不初始化SDK，第一次解析IVisionEngine才检查所选Runtime；合法配置不代表License/模型/设备可用。初始化失败不换实现。已解析实例唯一，关闭等待正在初始化的工厂真正返回，拒绝迟到发布。

App沿原WorkbenchLifetime在页面/节点停止之后等待新引擎关闭；异常初始化时也保留释放入口。其他宿主可选择完全不调用该装配方法，不引用Vision.Workbench。

**过渡边界：旧节点、工具箱、编辑会话仍走原链，尚未消费新IVisionEngine。此配置只控制新引擎，不改变旧节点的厂商或AI选择，不能声称整个工作台已经限制为唯一后端。P3必须迁移，P4必须删除旧入口，不能长期双轨。**

本批4项主要配置/装配测试和1项实际Prism启动/正常关闭测试通过；显式宿主增量构建0警告0错误（重编仍有4条旧RoiEditorView警告）。未运行全量、HALCON Runtime或真实硬件。详见CORE_REDESIGN_PLAN第11节。


启动项目：`Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj`。

- 只负责启动、模块注册、主窗/主题和导航。
- 唯一导航进入Controls的VisionFlowView，内部复用原FlowChartContainerView。
- 工具目录/工厂归Controls，节点常用参数/输出声明归FlowNode，算法/完整参数归Tools。
- 双击节点使用Controls原完整编辑器，共用同一Session；保存/重开、运行/停止、结果失效沿原链。
- 不恢复ToolBrowserView、列表任务页、重复工具箱或宿主内算法代码。
- FlowNodes.Workbench保持原通用流程节点宿主，二者不互相引用。

2026-10-26宿主恢复阶段：679视觉逻辑、525视觉WPF、原FlowNodes 28项通过。随后清理4类废弃View/资源共11文件：最新679逻辑/529WPF通过，Vision宿主保留4条既有nullable警告、0错误。随后再去除诊断页和ToolDebug桥7文件，修复Browser空会话时文件按钮消失；最新679逻辑/531WPF通过，宿主增量0警告0错误，原重编警告仍在。本轮不涉及MachineApp。
最新：VisionBrowserView及旧资源/注册删除，独立与节点均直接用VisionToolEditorView。节点原会话真实支持打开/保存/另存工具配置、导图及执行；工具.visionproj与整个.flow分开保存，加载不换节点/Session/DTO或连接。最终679逻辑/536WPF通过，宿主4条原nullable警告、0错误。MachineApp未涉及。
Native/物理/实图验收边界仍保留；详见[代码归位与验证](../Vision.Controls/docs/CODE_ORGANIZATION.md)。

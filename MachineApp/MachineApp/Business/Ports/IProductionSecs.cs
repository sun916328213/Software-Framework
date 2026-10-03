// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：IProductionSecs。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Extension.Automation.Secs。导入不代表所有类型都会在运行时加载。
using Extension.Automation.Secs;

namespace MachineApp.Business.Ports;

/// <summary>
/// IProductionSecs 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
/// </summary>
public interface IProductionSecs
{
    ESecsMode CurrentMode { get; }
    /// <summary>
    /// 更新当前实现管理的数据或状态；对应入口为 Update。
    /// </summary>
    void Update(string name, object value);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Append。
    /// </summary>
    void Append(string name, object value);
    /// <summary>
    /// 运行当前处理流程；对应入口为 Run。
    /// </summary>
    Task Run(Func<Task> offline, Func<Task> onlineLocal, Func<Task> onlineRemote);
    /// <summary>
    /// 执行发送操作；对应入口为 SendS6F11。
    /// </summary>
    Task SendS6F11(string order);
    /// <summary>
    /// 执行发送操作；对应入口为 SendRecipeReport。
    /// </summary>
    Task SendRecipeReport(string recipeJson);
    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetS2F41。
    /// </summary>
    Task<bool> GetS2F41(string lotId, string order);
    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetS2F41_Recipe。
    /// </summary>
    Task<string> GetS2F41_Recipe(string lotId, string order);
}

// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：ILogProviderFactory。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.Log.Definition。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.Log.Definition;

namespace DataManager.Core.Log;

/// <summary>
/// P12 Core seam：日志 Provider 工厂契约。Manager 只经此接口创建具体日志，
/// 测试可注入 Fake/Stub；新增日志后端在 Runtime 注册/扩展，不修改 Controls。
/// </summary>
public interface ILogProviderFactory
{
    /// <summary>按参数创建尚未注册的日志实例（不负责注册到管理器）。</summary>
    ILog Create(LogParam param);
}

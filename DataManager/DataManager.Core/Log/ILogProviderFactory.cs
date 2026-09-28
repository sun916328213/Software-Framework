using DataManager.Log.Definition;

namespace DataManager.Log;

/// <summary>
/// P12 Core seam：日志 Provider 工厂契约。Manager 只经此接口创建具体日志，
/// 测试可注入 Fake/Stub；新增日志后端在 Runtime 注册/扩展，不修改 Controls。
/// </summary>
public interface ILogProviderFactory
{
    /// <summary>按参数创建尚未注册的日志实例（不负责注册到管理器）。</summary>
    ILog Create(LogParam param);
}

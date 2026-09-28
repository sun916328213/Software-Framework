using DataManager.SQL.Definition;

namespace DataManager.SQL;

/// <summary>
/// P12 Core seam：SQL Provider 工厂契约。Manager 只经此接口创建具体 Provider，
/// 测试可注入 Fake/Stub；新增 Provider 在 Runtime 注册/扩展，不修改 Controls。
/// </summary>
public interface ISqlProviderFactory
{
    /// <summary>按参数创建与工具类型匹配的 SQL 实例（不负责注册到管理器）。</summary>
    ISQL<T> Create<T>(SQLParam param) where T : class, new();
}

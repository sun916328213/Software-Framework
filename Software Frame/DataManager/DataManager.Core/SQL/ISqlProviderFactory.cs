// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：ISqlProviderFactory。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.SQL.Definition。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.SQL.Definition;

namespace DataManager.Core.SQL;

/// <summary>
/// P12 Core seam：SQL Provider 工厂契约。Manager 只经此接口创建具体 Provider，
/// 测试可注入 Fake/Stub；新增 Provider 在 Runtime 注册/扩展，不修改 Controls。
/// </summary>
public interface ISqlProviderFactory
{
    /// <summary>按参数创建与工具类型匹配的 SQL 实例（不负责注册到管理器）。</summary>
    ISQL<T> Create<T>(SQLParam param) where T : class, new();
}

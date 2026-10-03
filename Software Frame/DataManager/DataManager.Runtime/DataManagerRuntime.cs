// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：DataManagerRuntime。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
namespace DataManager.Runtime;

/// <summary>
/// DataManager.Runtime 显式初始化入口（Framework-Refactoring §4.4）。
/// 实现保留在 Runtime，模块 OnInitialized 调用；保持幂等，不打开硬件/数据库/业务文件，不启动任务。
/// </summary>
public static class DataManagerRuntime
{
    /// <summary>Runtime 进程级初始化（当前仅 Npgsql 时间戳兼容开关）。</summary>
    public static void Initialize() => ApplyNpgsqlLegacyTimestampBehavior();

    /// <summary>
    /// Npgsql 时间戳兼容开关（原 DataManagerModule 内联魔法代码，现显式命名 + 文档化）。
    /// 原因：Npgsql 6.0+ 默认拒绝写入 Local（DateTime.Now）时间戳并抛错；
    /// 本库 EFCore/Dapper/SqlSugar 测试大量使用 DateTime.Now/Today，保持 Legacy 行为可避免全量改 UTC。
    /// 各 Provider 基类静态构造同样设置本开关（库层面兜底）；此处由模块初始化显式调用，时序更早。
    /// 如未来统一用 UTC 存储，在此删除调用并将实体时间改为 DateTime.UtcNow 即可。
    /// AppContext.SetSwitch 本身幂等，可重复调用。
    /// </summary>
    public static void ApplyNpgsqlLegacyTimestampBehavior() =>
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
}

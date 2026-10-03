// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：ICapacityStatisticsStore。成员说明可结合下方具体实现阅读。
namespace WPFControls.Queries.Data;

/// <summary>Persistence and record mapping required by the existing write use cases.</summary>
/// <remarks>
/// ICapacityStatisticsStore 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
/// </remarks>
public interface ICapacityStatisticsStore<T> where T : class
{
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 FindDay。
    /// </summary>
    T? FindDay(DateTime day);
    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateDay。
    /// </summary>
    T CreateDay(string id, DateTime day);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Time。
    /// </summary>
    DateTime Time(T row);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Good。
    /// </summary>
    int Good(T row, int hour);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Defect。
    /// </summary>
    int Defect(T row, int hour);
    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetGood。
    /// </summary>
    void SetGood(T row, int hour, int value);
    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetDefect。
    /// </summary>
    void SetDefect(T row, int hour, int value);
    /// <summary>
    /// 更新当前实现管理的数据或状态；对应入口为 UpdateDay。
    /// </summary>
    void UpdateDay(DateTime day, T row);
    /// <summary>
    /// 执行添加操作；对应入口为 Add。
    /// </summary>
    void Add(T row);
}

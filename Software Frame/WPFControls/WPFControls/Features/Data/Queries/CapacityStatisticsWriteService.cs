// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：ICapacityStatisticsWriteService、CapacityStatisticsWriteService。成员说明可结合下方具体实现阅读。
namespace WPFControls.Queries.Data;

/// <summary>
/// ICapacityStatisticsWriteService 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
/// </summary>
public interface ICapacityStatisticsWriteService<T> where T : class
{
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Record。
    /// </summary>
    void Record(DateTime time, bool isGood);
    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetHourly。
    /// </summary>
    void SetHourly(T? data, int seconds = -10);
}

/// <summary>Existing day/hour updates; no page state, container or SQL implementation.</summary>
/// <remarks>
/// CapacityStatisticsWriteService 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
/// 继承或实现的契约：ICapacityStatisticsWriteService&lt;T&gt;。具体约束按这些契约及各成员实现解释。
/// </remarks>
public sealed class CapacityStatisticsWriteService<T>(ICapacityStatisticsStore<T> store)
    : ICapacityStatisticsWriteService<T> where T : class
{
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Record。
    /// 正文中的关键协作调用包括 ToString、FindDay、SetGood、Good、SetDefect；具体条件和先后次序见方法体。
    /// </summary>
    public void Record(DateTime time, bool isGood)
    {
        var day = time.Date;
        var hour = time.Hour;
        var id = day.ToString("yyyyMMdd");
        var existing = store.FindDay(day);
        if (existing != null)
        {
            if (isGood) store.SetGood(existing, hour, store.Good(existing, hour) + 1);
            else store.SetDefect(existing, hour, store.Defect(existing, hour) + 1);
            store.UpdateDay(day, existing);
        }
        else
        {
            var row = store.CreateDay(id, day);
            if (isGood) store.SetGood(row, hour, 1); else store.SetDefect(row, hour, 1);
            store.Add(row);
        }
    }

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetHourly。
    /// 正文中的关键协作调用包括 AddSeconds、Time、ToString、FindDay、SetGood；具体条件和先后次序见方法体。
    /// </summary>
    public void SetHourly(T? data, int seconds = -10)
    {
        if (data == null) return;
        var curtime = store.Time(data).AddSeconds(seconds);
        var day = curtime.Date;
        var hour = curtime.Hour;
        var id = day.ToString("yyyyMMdd");
        var existing = store.FindDay(day);
        if (existing != null)
        {
            store.SetGood(existing, hour, store.Good(data, hour));
            store.SetDefect(existing, hour, store.Defect(data, hour));
            store.UpdateDay(day, existing);
        }
        else
        {
            var row = store.CreateDay(id, day);
            store.SetGood(row, hour, store.Good(data, hour));
            store.SetDefect(row, hour, store.Defect(data, hour));
            store.Add(row);
        }
    }
}

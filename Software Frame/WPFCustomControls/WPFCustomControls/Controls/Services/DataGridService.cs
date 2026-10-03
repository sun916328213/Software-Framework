// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：DataGridService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、System.Collections.Concurrent、System.Windows、System.Windows.Threading。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Threading;

namespace WPFCustomControls.Controls;

/// <summary>Buffers DataGrid updates and applies them in batches on the UI thread.</summary>
/// <remarks>
/// DataGridService 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
/// </remarks>
public class DataGridService<T>
{
    private readonly DispatcherTimer _batchTimer;
    private readonly ConcurrentQueue<T> _pendingItems = new();
    private int _retainNum = 200;

    /// <summary>
    /// 初始化 DataGridService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 FromMilliseconds、FlushBatch；具体条件和先后次序见方法体。
    /// </summary>
    public DataGridService()
    {
        Model = new DataGridModel<T>();
        _batchTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _batchTimer.Tick += (_, _) => FlushBatch();
    }

    public DataGridModel<T> Model { get; }

    /// <summary>
    /// 执行添加操作；对应入口为 Add。
    /// 正文中的关键协作调用包括 Enqueue、Start；具体条件和先后次序见方法体。
    /// </summary>
    public virtual void Add(T data, int retainNum = 200)
    {
        _retainNum = retainNum;
        _pendingItems.Enqueue(data);
        if (!_batchTimer.IsEnabled)
            _batchTimer.Start();
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Clear。
    /// 正文中的关键协作调用包括 Dispatch；具体条件和先后次序见方法体。
    /// </summary>
    public virtual void Clear() => Dispatch(Model.DataCollection.Clear);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ShowData。
    /// 正文中的关键协作调用包括 Dispatch、Clear、Add；具体条件和先后次序见方法体。
    /// </summary>
    public virtual void ShowData(List<T>? data) => Dispatch(() =>
    {
        Model.DataCollection.Clear();
        if (data == null)
            return;

        foreach (var item in data)
            Model.DataCollection.Add(item);
    });

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 FlushBatch。
    /// 正文中的关键协作调用包括 Stop、TryDequeue、Add、Min、Max；具体条件和先后次序见方法体。
    /// </summary>
    private void FlushBatch()
    {
        _batchTimer.Stop();
        var items = new List<T>();
        while (_pendingItems.TryDequeue(out var item))
            items.Add(item);
        if (items.Count == 0)
            return;

        var removeCount = Math.Min(
            Math.Max(0, Model.DataCollection.Count + items.Count - _retainNum),
            Model.DataCollection.Count);
        for (var index = 0; index < removeCount; index++)
            Model.DataCollection.RemoveAt(0);
        foreach (var item in items)
            Model.DataCollection.Add(item);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Dispatch。
    /// 正文中的关键协作调用包括 CheckAccess、action、BeginInvoke、WriteLine；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private static void Dispatch(Action action)
    {
        try
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                action();
            else
                dispatcher.BeginInvoke(action);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"DataGridService UI 更新失败: {exception.Message}");
        }
    }
}

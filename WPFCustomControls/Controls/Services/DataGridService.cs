using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Threading;

namespace WPFCustomControls.Controls;

/// <summary>Buffers DataGrid updates and applies them in batches on the UI thread.</summary>
public class DataGridService<T>
{
    private readonly DispatcherTimer _batchTimer;
    private readonly ConcurrentQueue<T> _pendingItems = new();
    private int _retainNum = 200;

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

    public virtual void Add(T data, int retainNum = 200)
    {
        _retainNum = retainNum;
        _pendingItems.Enqueue(data);
        if (!_batchTimer.IsEnabled)
            _batchTimer.Start();
    }

    public virtual void Clear() => Dispatch(Model.DataCollection.Clear);

    public virtual void ShowData(List<T>? data) => Dispatch(() =>
    {
        Model.DataCollection.Clear();
        if (data == null)
            return;

        foreach (var item in data)
            Model.DataCollection.Add(item);
    });

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

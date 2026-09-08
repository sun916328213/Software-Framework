using System;
using System.Windows;

namespace WPFCustomControls.Controls;

public class CustListViewService
{
    public ListViewModel Model { get; set; } = new();

    public void Add(string data) => Dispatch(() => Model.DataCollection.Add(data));

    public void Clear() => Dispatch(Model.DataCollection.Clear);

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
                $"CustListViewService UI 更新失败: {exception.Message}");
        }
    }
}

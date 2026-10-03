// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：CustListViewService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、System、System.Windows。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using System;
using System.Windows;

namespace WPFCustomControls.Controls;

/// <summary>
/// CustListViewService 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
/// </summary>
public class CustListViewService
{
    public ListViewModel Model { get; set; } = new();

    /// <summary>
    /// 执行添加操作；对应入口为 Add。
    /// 正文中的关键协作调用包括 Dispatch、Add；具体条件和先后次序见方法体。
    /// </summary>
    public void Add(string data) => Dispatch(() => Model.DataCollection.Add(data));

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Clear。
    /// 正文中的关键协作调用包括 Dispatch；具体条件和先后次序见方法体。
    /// </summary>
    public void Clear() => Dispatch(Model.DataCollection.Clear);

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
                $"CustListViewService UI 更新失败: {exception.Message}");
        }
    }
}

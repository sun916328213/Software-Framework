// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：PrintService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls.ProgramControl.Services、WPFControls、Device.Controls.Models、Device.Controls.Services、Extension.Core.Models、Extension.UI.Controls。导入不代表所有类型都会在运行时加载。
using WPFControls.ProgramControl.Services;
using WPFControls;
using Device.Controls.Models;
using Device.Controls.Services;
using Extension.Core.Models;
using Extension.UI.Controls;
using System.Collections.ObjectModel;
using WPFControls.Controls.Models;
using WPFControls.Controls.Diagnostics;

namespace WPFControls.ProgramControl.Services.Printer;

// Compatibility for the existing queue/preview UI; all device operations use the new label workspace.
/// <summary>
/// PrintService 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
/// </summary>
internal sealed class PrintService(LabelPrintWorkspace workspace, TryCatchHelper? errors = null)
{
        private readonly TryCatchHelper _errors = errors ?? new();

    internal PrintModel Model { get; } = new();

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 OpenFile。
    /// 正文中的关键协作调用包括 TryExecuteAsync、OpenFile、IsNullOrWhiteSpace、ConnectAsync、LoadPrintersAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    public Task OpenFile() => _errors.TryExecuteAsync(async () =>
    {
        var path = FileDialogEx.OpenFile();
        if (string.IsNullOrWhiteSpace(path)) return;
        Model.PrintInfo = null!;
        Model.LabelFormat = path;
        workspace.Model.TemplatePath = path;
        await workspace.ConnectAsync();
        await workspace.LoadPrintersAsync();
        await workspace.OpenTemplateAsync();
        await workspace.RenderPreviewAsync();
        Model.PrintNames = new ObservableCollection<string>(workspace.Model.Printers);
        UpdateFieldsAndPreview();
    });

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetKeyAsync。
    /// 正文中的关键协作调用包括 TryExecuteAsync、ReloadFieldsAsync、UpdateFieldsAndPreview；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    public Task GetKeyAsync() => _errors.TryExecuteAsync(async () =>
    {
        await workspace.ReloadFieldsAsync();
        UpdateFieldsAndPreview();
    });

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetValueAsync。
    /// 正文中的关键协作调用包括 TryExecuteAsync、Clear、Add、ApplyFieldsAsync、RenderPreviewAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    public Task SetValueAsync() => _errors.TryExecuteAsync(async () =>
    {
        workspace.Model.Fields.Clear();
        foreach (var field in Model.LabelField)
            workspace.Model.Fields.Add(new LabelTemplateFieldRow(field.Key, field.Value));
        await workspace.ApplyFieldsAsync();
        await workspace.RenderPreviewAsync();
        Model.LabelImage = workspace.Model.Preview;
        Model.PrintInfo = new PrintInfo
        {
            LabelPath = Model.LabelFormat,
            LabelName = System.IO.Path.GetFileNameWithoutExtension(Model.LabelFormat),
            PrintName = workspace.Model.SelectedPrinter,
            LabelFieldInfo = Model.LabelField.Select(field => new KeyValue(field.Key, field.Value)).ToList()
        };
    });

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Print。
    /// 正文中的关键协作调用包括 TryExecuteAsync；具体条件和先后次序见方法体。
    /// </summary>
    public Task Print() => _errors.TryExecuteAsync(workspace.PrintAsync);
    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetPrinterName。
    /// </summary>
    public void SetPrinterName(string name) => workspace.Model.SelectedPrinter = name;

    /// <summary>
    /// 更新当前实现管理的数据或状态；对应入口为 UpdateFieldsAndPreview。
    /// 正文中的关键协作调用包括 Select；具体条件和先后次序见方法体。
    /// </summary>
    private void UpdateFieldsAndPreview()
    {
        Model.LabelField = new ObservableCollection<KeyValue>(workspace.Model.Fields.Select(field => new KeyValue(field.Name, field.Value)));
        Model.LabelImage = workspace.Model.Preview;
    }
}

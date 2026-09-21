using System.IO;
using System.Windows.Media.Imaging;
using Device.Core.Devices;
using Device.Core.LabelPrinting;
using Device.Controls.Models;
using Extension.UI.Controls;

namespace Device.Controls.Services;

/// <summary>
/// 标签打印页流程：ILabelPrintEngine 不是 IDevice（无连接语义），
/// 用 <see cref="EngineDevice"/> 适配进通用「创建→连接→操作→释放」生命周期；
/// 页命令走引擎专属契约：打印机列表 / 打开模板 / 字段读写 / PNG 预览 / 打印 / 关闭模板。
/// </summary>
public sealed class LabelPrintWorkspace(ILabelPrintEngineFactory factory)
    : DeviceWorkspaceBase<LabelPrintPageModel, LabelPrintProfile>(factory.AvailableDrivers)
{
    protected override string CategoryName => "标签打印引擎";

    protected override string FallbackProfileName => "标签打印01";

    protected override string BuildKey(LabelPrintProfile profile) =>
        $"{profile.DriverId}:{profile.DefaultPrinterName}:{profile.PrintTimeoutMilliseconds}:{profile.PreviewDpi}";

    protected override IDevice CreateDevice(LabelPrintProfile profile) =>
        new EngineDevice(factory.Create(profile.ToOptions()));

    /// <summary>读取系统打印机列表并回填下拉（默认打印机在列表中则预选）。</summary>
    public async Task LoadPrintersAsync()
    {
        var engine = Engine();
        var result = await engine.GetInstalledPrintersAsync().ConfigureAwait(false);
        if (!result.Success)
            throw new InvalidOperationException(result.Message ?? result.ErrorCode ?? "读取打印机列表失败");

        var printers = result.Value ?? [];
        var preferred = Model.SelectedProfile?.DefaultPrinterName;
        DispatcherEx.UIInvokeSafe(() =>
        {
            Model.Printers.Clear();
            foreach (var printer in printers)
                Model.Printers.Add(printer);
            Model.SelectedPrinter =
                printers.Contains(preferred ?? string.Empty) ? preferred
                : printers.Count > 0 ? printers[0]
                : null;
        });
    }

    /// <summary>打开 Model.TemplatePath 指向的模板并刷新字段表。</summary>
    public async Task OpenTemplateAsync()
    {
        var path = Model.TemplatePath;
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("请先填写模板路径");

        var engine = Engine();
        var result = await engine.OpenTemplateAsync(path).ConfigureAwait(false);
        if (!result.Success)
            throw new InvalidOperationException(result.Message ?? result.ErrorCode ?? "打开模板失败");

        await ReloadFieldsAsync().ConfigureAwait(false);
    }

    /// <summary>重新读取当前模板的命名字段。</summary>
    public async Task ReloadFieldsAsync()
    {
        var engine = Engine();
        var result = await engine.GetFieldsAsync().ConfigureAwait(false);
        if (!result.Success)
            throw new InvalidOperationException(result.Message ?? result.ErrorCode ?? "读取字段失败");

        var fields = result.Value ?? [];
        DispatcherEx.UIInvokeSafe(() =>
        {
            Model.Fields.Clear();
            foreach (var field in fields)
                Model.Fields.Add(new LabelTemplateFieldRow(field.Name, field.Value));
        });
    }

    /// <summary>把字段表当前值批量下发到模板。</summary>
    public async Task ApplyFieldsAsync()
    {
        var engine = Engine();
        var values = Model.Fields.ToDictionary(f => f.Name, f => f.Value);
        var result = await engine.SetFieldsAsync(values).ConfigureAwait(false);

        if (!result.Success)
            throw new InvalidOperationException(result.Message ?? result.ErrorCode ?? "字段下发失败");
    }

    /// <summary>渲染 PNG 预览并回填。</summary>
    public async Task RenderPreviewAsync()
    {
        var engine = Engine();
        var result = await engine.RenderPreviewAsync().ConfigureAwait(false);
        if (!result.Success || result.Value is null)
            throw new InvalidOperationException(result.Message ?? result.ErrorCode ?? "渲染预览失败");

        using var stream = new MemoryStream(result.Value.Data.ToArray());
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();

        DispatcherEx.UIInvokeSafe(() => Model.Preview = bitmap);
    }

    /// <summary>按当前份数与选中打印机打印。</summary>
    public async Task PrintAsync()
    {
        var engine = Engine();
        var result = await engine
            .PrintAsync(Math.Max(1, Model.Copies), Model.SelectedPrinter)
            .ConfigureAwait(false);

        if (!result.Success)
            throw new InvalidOperationException(result.Message ?? result.ErrorCode ?? "打印失败");
    }

    /// <summary>关闭当前模板（保留引擎）。</summary>
    public async Task CloseTemplateAsync()
    {
        var engine = Engine();
        var result = await engine.CloseTemplateAsync().ConfigureAwait(false);
        if (!result.Success)
            throw new InvalidOperationException(result.Message ?? result.ErrorCode ?? "关闭模板失败");

        DispatcherEx.UIInvokeSafe(() =>
        {
            Model.Fields.Clear();
            Model.TemplatePath = string.Empty;
            Model.Preview = null;
        });
    }

    private ILabelPrintEngine Engine()
    {
        if (Device is not EngineDevice wrapper
            || wrapper.State != DeviceState.Connected)
            throw new InvalidOperationException($"请先连接{CategoryName}");
        return wrapper.Engine;
    }

    /// <summary>把无连接语义的引擎适配成 IDevice：连接即视为可用，断开即关闭模板。</summary>
    private sealed class EngineDevice(ILabelPrintEngine engine) : IDevice
    {
        private DeviceState _state = DeviceState.Disconnected;

        public ILabelPrintEngine Engine => engine;

        public string Name => engine.Name;

        public DeviceState State => _state;

        public ValueTask<DeviceResult> ConnectAsync(CancellationToken cancellationToken = default)
        {
            _state = DeviceState.Connected;
            return ValueTask.FromResult(DeviceResult.Ok());
        }

        public async ValueTask<DeviceResult> DisconnectAsync(CancellationToken cancellationToken = default)
        {
            var result = await engine.CloseTemplateAsync(cancellationToken).ConfigureAwait(false);
            if (result.Success)
                _state = DeviceState.Disconnected;
            return result;
        }

        public async ValueTask DisposeAsync() => await engine.DisposeAsync().ConfigureAwait(false);
    }
}

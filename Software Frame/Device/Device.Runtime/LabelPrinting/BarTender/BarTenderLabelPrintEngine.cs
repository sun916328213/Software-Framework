// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：BarTenderLabelPrintEngine。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Runtime.LabelPrinting、Device.Core.Devices、Device.Core.LabelPrinting、Seagull.BarTender.Print。导入不代表所有类型都会在运行时加载。
using Device.Runtime.LabelPrinting;
using Device.Core.Devices;
using Device.Core.LabelPrinting;
using Seagull.BarTender.Print;

namespace Device.Runtime.LabelPrinting.BarTender;

/// <summary>
/// 基于 Seagull Scientific BarTender SDK 的标签模板打印适配器。
/// </summary>
internal sealed class BarTenderLabelPrintEngine : ILabelPrintEngine
{
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private Engine? _engine;
    private LabelFormatDocument? _format;
    private int _disposeStarted;

    /// <summary>创建尚未启动 BarTender 进程的标签打印引擎。</summary>
    public BarTenderLabelPrintEngine(LabelPrintEngineOptions options)
    {
        Options = options;
    }

    /// <inheritdoc />
    public string Name => Options.Name;

    /// <inheritdoc />
    public LabelPrintEngineOptions Options { get; }

    /// <inheritdoc />
    public string? TemplatePath { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// 获取当前实现提供的值或对象；对应入口为 GetInstalledPrintersAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、WaitAsync、ThrowIfDisposed、Run、Ok；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public async ValueTask<DeviceResult<IReadOnlyList<string>>> GetInstalledPrintersAsync(
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            var printers = await Task.Run(ReadInstalledPrinters, cancellationToken).ConfigureAwait(false);
            return DeviceResult<IReadOnlyList<string>>.Ok(printers);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return DeviceResult<IReadOnlyList<string>>.Failure(
                "label-print.bartender.printers_failed",
                exception.Message);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 OpenTemplateAsync。
    /// 正文中的关键协作调用包括 IsNullOrWhiteSpace、Failure、Equals、GetExtension、Exists；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public async ValueTask<DeviceResult> OpenTemplateAsync(
        string templatePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templatePath))
            return DeviceResult.Failure("label-print.bartender.template_path_empty", "标签模板路径不能为空。");
        if (!string.Equals(Path.GetExtension(templatePath), ".btw", StringComparison.OrdinalIgnoreCase))
            return DeviceResult.Failure("label-print.bartender.template_format_invalid", "BarTender 模板必须使用 .btw 扩展名。");
        if (!File.Exists(templatePath))
            return DeviceResult.Failure("label-print.bartender.template_not_found", $"标签模板不存在：{templatePath}");

        var fullPath = Path.GetFullPath(templatePath);
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            await Task.Run(() => OpenTemplateCore(fullPath), cancellationToken).ConfigureAwait(false);
            TemplatePath = fullPath;
            return DeviceResult.Ok();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return DeviceResult.Failure("label-print.bartender.open_failed", exception.Message);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 获取当前实现提供的值或对象；对应入口为 GetFieldsAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、WaitAsync、ThrowIfDisposed、TemplateNotOpen、Run；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public async ValueTask<DeviceResult<IReadOnlyList<LabelTemplateField>>> GetFieldsAsync(
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (_format is null)
                return TemplateNotOpen<IReadOnlyList<LabelTemplateField>>();

            var fields = await Task.Run(ReadFields, cancellationToken).ConfigureAwait(false);
            return DeviceResult<IReadOnlyList<LabelTemplateField>>.Ok(fields);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return DeviceResult<IReadOnlyList<LabelTemplateField>>.Failure(
                "label-print.bartender.fields_failed",
                exception.Message);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 设置当前实现管理的值或状态；对应入口为 SetFieldsAsync。
    /// 正文中的关键协作调用包括 ThrowIfNull、ConfigureAwait、WaitAsync、ThrowIfDisposed、TemplateNotOpen；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public async ValueTask<DeviceResult> SetFieldsAsync(
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (_format is null)
                return TemplateNotOpen();

            var invalidName = values.Keys.FirstOrDefault(string.IsNullOrWhiteSpace);
            if (invalidName is not null)
                return DeviceResult.Failure("label-print.bartender.field_name_empty", "模板字段名称不能为空。");

            await Task.Run(() => SetFieldsCore(values), cancellationToken).ConfigureAwait(false);
            return DeviceResult.Ok();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return DeviceResult.Failure("label-print.bartender.set_fields_failed", exception.Message);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 RenderPreviewAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、WaitAsync、ThrowIfDisposed、TemplateNotOpen、Run；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public async ValueTask<DeviceResult<LabelPreview>> RenderPreviewAsync(
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (_format is null)
                return TemplateNotOpen<LabelPreview>();

            var preview = await Task.Run(RenderPreviewCore, cancellationToken).ConfigureAwait(false);
            return DeviceResult<LabelPreview>.Ok(preview);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return DeviceResult<LabelPreview>.Failure("label-print.bartender.preview_failed", exception.Message);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 PrintAsync。
    /// 正文中的关键协作调用包括 Failure、IsNullOrWhiteSpace、Trim、ConfigureAwait、WaitAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public async ValueTask<DeviceResult> PrintAsync(
        int copies = 1,
        string? printerName = null,
        CancellationToken cancellationToken = default)
    {
        if (copies <= 0)
            return DeviceResult.Failure("label-print.bartender.copies_invalid", "打印份数必须大于 0。");

        var selectedPrinter = string.IsNullOrWhiteSpace(printerName)
            ? Options.DefaultPrinterName
            : printerName.Trim();
        if (string.IsNullOrWhiteSpace(selectedPrinter))
            return DeviceResult.Failure("label-print.bartender.printer_missing", "没有指定打印机。");

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (_format is null)
                return TemplateNotOpen();

            return await Task.Run(
                () => PrintCore(selectedPrinter, copies),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return DeviceResult.Failure("label-print.bartender.print_failed", exception.Message);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行关闭步骤；对应入口为 CloseTemplateAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、WaitAsync、ThrowIfDisposed、Ok、Run；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public async ValueTask<DeviceResult> CloseTemplateAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (_format is null)
                return DeviceResult.Ok();

            await Task.Run(CloseTemplateCore, CancellationToken.None).ConfigureAwait(false);
            return DeviceResult.Ok();
        }
        catch (Exception exception)
        {
            return DeviceResult.Failure("label-print.bartender.close_failed", exception.Message);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 DisposeAsync。
    /// 正文中的关键协作调用包括 Exchange、ConfigureAwait、WaitAsync、TryCleanup、Stop；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
            return;

        await _operationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            Exception? firstError = null;
            TryCleanup(CloseTemplateCore, ref firstError);

            var engine = _engine;
            _engine = null;
            if (engine is not null)
            {
                // 即使停止进程失败，也必须继续释放 SDK 对象，避免遗留 BarTender 资源。
                TryCleanup(() => engine.Stop(SaveOptions.DoNotSaveChanges), ref firstError);
                TryCleanup(engine.Dispose, ref firstError);
            }

            if (firstError is not null)
                throw firstError;
        }
        finally
        {
            _operationGate.Release();
            _operationGate.Dispose();
        }
    }

    /// <summary>
    /// 读取当前实现提供的数据；对应入口为 ReadInstalledPrinters。
    /// 正文中的关键协作调用包括 Add、ToArray、OrderBy；具体条件和先后次序见方法体。
    /// </summary>
    private static IReadOnlyList<string> ReadInstalledPrinters()
    {
        var result = new List<string>();
        foreach (Seagull.BarTender.Print.Printer printer in new Seagull.BarTender.Print.Printers())
            result.Add(printer.PrinterName);
        return result.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 OpenTemplateCore。
    /// 正文中的关键协作调用包括 CloseTemplateCore、Open；具体条件和先后次序见方法体。
    /// </summary>
    private void OpenTemplateCore(string fullPath)
    {
        CloseTemplateCore();
        _engine ??= new Engine(true);
        _format = _engine.Documents.Open(fullPath);
    }

    /// <summary>
    /// 读取当前实现提供的数据；对应入口为 ReadFields。
    /// 正文中的关键协作调用包括 GetEnumerator、MoveNext、Add；具体条件和先后次序见方法体。
    /// </summary>
    private IReadOnlyList<LabelTemplateField> ReadFields()
    {
        var fields = new List<LabelTemplateField>();
        var enumerator = _format!.SubStrings.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var field = enumerator.Current;
            fields.Add(new LabelTemplateField(field.Name, field.Value));
        }
        return fields;
    }

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetFieldsCore。
    /// 正文中的关键协作调用包括 SetSubString；具体条件和先后次序见方法体。
    /// </summary>
    private void SetFieldsCore(IReadOnlyDictionary<string, string> values)
    {
        foreach (var pair in values)
            _format!.SubStrings.SetSubString(pair.Key, pair.Value ?? string.Empty);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 RenderPreviewCore。
    /// 正文中的关键协作调用包括 Combine、GetTempPath、NewGuid、ExportImageToFile、ReadAllBytes；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private LabelPreview RenderPreviewCore()
    {
        var temporaryPath = Path.Combine(
            Path.GetTempPath(),
            $"device-label-preview-{Guid.NewGuid():N}.png");
        try
        {
            _format!.ExportImageToFile(
                temporaryPath,
                ImageType.PNG,
                ColorDepth.ColorDepth24bit,
                new Resolution(Options.PreviewDpi),
                OverwriteOptions.Overwrite);
            return new LabelPreview("image/png", File.ReadAllBytes(temporaryPath));
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch
            {
                // 临时预览清理失败不能覆盖真正的导出或读取错误。
            }
        }
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 PrintCore。
    /// 正文中的关键协作调用包括 Failure、Print、Ok、Join、Select；具体条件和先后次序见方法体。
    /// </summary>
    private DeviceResult PrintCore(string printerName, int copies)
    {
        if (copies > 1 && !_format!.PrintSetup.SupportsIdenticalCopies)
        {
            return DeviceResult.Failure(
                "label-print.bartender.copies_not_supported",
                "当前模板或打印机不支持一次打印多份相同标签。");
        }

        _format!.PrintSetup.PrinterName = printerName;
        _format.PrintSetup.IdenticalCopiesOfLabel = copies;
        var result = _format.Print(Name, Options.PrintTimeoutMilliseconds, out var messages);
        if (result == Result.Success)
            return DeviceResult.Ok();

        var details = string.Join(
            Environment.NewLine,
            messages.Select(message => message.Text));
        var errorCode = result == Result.Timeout
            ? "label-print.bartender.print_timeout"
            : "label-print.bartender.print_failed";
        return DeviceResult.Failure(errorCode, string.IsNullOrWhiteSpace(details) ? result.ToString() : details);
    }

    /// <summary>
    /// 执行关闭步骤；对应入口为 CloseTemplateCore。
    /// 正文中的关键协作调用包括 Close；具体条件和先后次序见方法体。
    /// </summary>
    private void CloseTemplateCore()
    {
        if (_format is null)
            return;

        try
        {
            _format.Close(SaveOptions.DoNotSaveChanges);
        }
        finally
        {
            _format = null;
            TemplatePath = null;
        }
    }

    /// <summary>执行一项释放操作，并保留最先出现的异常供全部清理完成后报告。</summary>
    private static void TryCleanup(Action cleanup, ref Exception? firstError)
    {
        try
        {
            cleanup();
        }
        catch (Exception exception)
        {
            firstError ??= exception;
        }
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 TemplateNotOpen。
    /// 正文中的关键协作调用包括 Failure；具体条件和先后次序见方法体。
    /// </summary>
    private static DeviceResult TemplateNotOpen() =>
        DeviceResult.Failure("label-print.bartender.template_not_open", "尚未打开标签模板。");

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 TemplateNotOpen。
    /// 正文中的关键协作调用包括 Failure；具体条件和先后次序见方法体。
    /// </summary>
    private static DeviceResult<T> TemplateNotOpen<T>() =>
        DeviceResult<T>.Failure("label-print.bartender.template_not_open", "尚未打开标签模板。");

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ThrowIfDisposed。
    /// 正文中的关键协作调用包括 ThrowIf、Read；具体条件和先后次序见方法体。
    /// </summary>
    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeStarted) != 0, this);
}

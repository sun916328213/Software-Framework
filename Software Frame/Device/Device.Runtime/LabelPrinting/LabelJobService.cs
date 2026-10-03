// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：LabelJobService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Devices、Device.Core.LabelPrinting。导入不代表所有类型都会在运行时加载。
using Device.Core.Devices;
using Device.Core.LabelPrinting;

namespace Device.Runtime.LabelPrinting;

/// <summary>Owns and releases each engine, serializing full jobs so templates/fields never interleave.</summary>
/// <remarks>
/// LabelJobService 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
/// 继承或实现的契约：ILabelJobService。具体约束按这些契约及各成员实现解释。
/// </remarks>
public sealed class LabelJobService(ILabelPrintEngineFactory factory) : ILabelJobService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 PrintAsync。
    /// 正文中的关键协作调用包括 ThrowIfNull、ThrowIfNullOrWhiteSpace、nameof、ConfigureAwait、WaitAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    public async Task PrintAsync(LabelPrintEngineOptions options, LabelPrintRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TemplatePath);
        if (request.Copies < 1) throw new ArgumentOutOfRangeException(nameof(request));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var engine = factory.Create(options);
            Ensure(await engine.OpenTemplateAsync(request.TemplatePath, cancellationToken).ConfigureAwait(false));
            Exception? failure = null;
            try
            {
                if (request.Fields is { Count: > 0 })
                    Ensure(await engine.SetFieldsAsync(request.Fields, cancellationToken).ConfigureAwait(false));
                Ensure(await engine.PrintAsync(request.Copies, request.PrinterName, cancellationToken).ConfigureAwait(false));
            }
            catch (Exception error) { failure = error; throw; }
            finally
            {
                // Cleanup also runs when the caller cancels; DisposeAsync is the final ownership guarantee.
                try { Ensure(await engine.CloseTemplateAsync(CancellationToken.None).ConfigureAwait(false)); }
                catch (Exception cleanup) when (failure is not null) { throw new AggregateException(failure, cleanup); }
            }
        }
        finally { _gate.Release(); }
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Ensure。
    /// </summary>
    private static void Ensure(DeviceResult result)
    {
        if (!result.Success) throw new InvalidOperationException(result.Message ?? result.ErrorCode ?? "标签打印失败");
    }
}

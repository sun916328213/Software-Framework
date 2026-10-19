using Prism.Ioc;
using Secs4Net;
using Secs4Net.Sml;
using SecsGem.Runtime.Manager;
using SecsGem.Runtime.Order.S7;

namespace SecsGem.Runtime;

/// <summary>目录只读快照，不含可编辑的业务配方对象。</summary>
public sealed record SecsGemProcessProgramSummary(
    string PPID, bool HasBinary, int? BinaryLength, bool HasFormatted,
    string MDLN, string SOFTREV, int? CommandCount)
{
    public string Formats => (HasBinary, HasFormatted) switch
    {
        (true, true) => "B / Formatted",
        (true, false) => "B",
        (false, true) => "Formatted",
        _ => "仅目录"
    };
}

public partial class SecsGemRuntime
{
    /// <summary>S7F17 实际删除成功后的实例级通知；空数组表示全部删除。不是 SECS 事件上报。</summary>
    public event Action<string[]>? ProcessProgramsDeleted;

    /// <summary>S7F23 实际接受后的 PPID 通知；不表示工艺应用或配方切换。</summary>
    public event Action<string>? FormattedProcessProgramReceived;

    private void NotifyFormattedProcessProgramReceived(SecsMessage request, Item? reply)
    {
        if (_disposed || request.S != 7 || request.F != 23
            || reply is not { Format: SecsFormat.Binary, Count: 1 } || reply.GetMemory<byte>().Span[0] != 0
            || request.SecsItem is not { Format: SecsFormat.List, Count: 4 } body
            || FormattedProcessProgramReceived is not { } handlers) return;
        var id = body.Items[0].GetString();
        foreach (Action<string> handler in handlers.GetInvocationList())
        {
            try { handler(id); }
            catch (Exception ex) { RaiseError($"S7F23 已接收，但目录通知失败：{ex.Message}"); }
        }
    }

    private void NotifyProcessProgramDeletion(SecsMessage request, Item? reply)
    {
        if (_disposed || request is null || request.S != 7 || request.F != 17
            || reply is not { Format: SecsFormat.Binary, Count: 1 } || reply.GetMemory<byte>().Span[0] != 0
            || request.SecsItem is not { Format: SecsFormat.List } body
            || ProcessProgramsDeleted is not { } handlers) return;
        var ids = body.Items.Select(item => item.GetString()).ToArray();
        foreach (Action<string[]> handler in handlers.GetInvocationList())
        {
            try { handler(ids.ToArray()); }
            catch (Exception ex) { RaiseError($"S7F17 已删除，但目录通知失败：{ex.Message}"); }
        }
    }

    /// <summary>仅查询内存库，不连接设备；Controls 不直接持有 DeviceManager。</summary>
    public IReadOnlyList<SecsGemProcessProgramSummary> GetProcessPrograms()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _container.Resolve<DeviceManager>().GetProcessProgramSummaries();
    }

    /// <summary>读取配方的线协议表示。返回字符串快照，不暴露借用 Item 或业务编辑对象。</summary>
    public string GetProcessProgramPreview(string ppid)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var manager = _container.Resolve<DeviceManager>();
        var parts = new List<string>();
        var binary = manager.GetUnformattedProcessProgram(ppid);
        if (binary != null)
        {
            using var message = new SecsMessage(7, 6)
                { SecsItem = Item.L(Item.A(ppid), Item.B(binary)) };
            parts.Add(message.ToSml());
        }
        var formatted = manager.GetProcessProgramData(ppid);
        if (formatted != null)
        {
            using var message = new SecsMessage(7, 26)
                { SecsItem = ProcessProgramCodec.WriteFormatted(formatted) };
            parts.Add(message.ToSml());
        }
        return parts.Count == 0 ? "仅有目录项，当前内存库没有该配方正文。" : string.Join(Environment.NewLine, parts);
    }

    /// <summary>读取二进制正文的独立副本，格式解析由宿主负责；未找到返回 null。</summary>
    public byte[]? GetUnformattedProcessProgram(string ppid)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ProcessProgramCodec.ReadId(Item.A(ppid));
        return _container.Resolve<DeviceManager>().GetUnformattedProcessProgram(ppid);
    }

    /// <summary>读取格式化正文的独立副本，工艺映射由 Controls 按宿主配置完成。</summary>
    public ProcessProgramData? GetFormattedProcessProgram(string ppid)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ProcessProgramCodec.ReadId(Item.A(ppid));
        return _container.Resolve<DeviceManager>().GetProcessProgramData(ppid);
    }

    /// <summary>显式写入当前内存库；不持久化、不选择或执行配方。</summary>
    public void StoreUnformattedProcessProgram(string ppid, byte[] body)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _container.Resolve<DeviceManager>().StoreUnformattedProcessProgram(ppid, body);
    }

    /// <summary>仅检查通用 S7 格式结构，不保存、不限定机型工艺字段。</summary>
    public static void ValidateFormattedProcessProgram(ProcessProgramData program)
    {
        ArgumentNullException.ThrowIfNull(program);
        using var item = ProcessProgramCodec.WriteFormatted(program);
    }

    /// <summary>显式写入格式化配方的独立副本；不持久化、不选择或执行配方。</summary>
    public void StoreFormattedProcessProgram(ProcessProgramData program)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(program);
        ProcessProgramCodec.ReadId(Item.A(program.PPID));
        _container.Resolve<DeviceManager>().UpdateProcessProgramData(program);
    }
}

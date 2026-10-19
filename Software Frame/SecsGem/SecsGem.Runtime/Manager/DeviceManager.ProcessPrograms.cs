using SecsGem.Runtime.Order.S7;

namespace SecsGem.Runtime.Manager;

public partial class DeviceManager
{
    private readonly object _programGate = new();
    private readonly Dictionary<string, byte[]> _unformattedPrograms = new(StringComparer.Ordinal);

    /// <summary>内存配方库；写入仅存储，不选择、不执行配方，不写设备或文件。</summary>
    public void StoreUnformattedProcessProgram(string ppid, byte[] body)
    {
        ProcessProgramCodec.ReadId(Secs4Net.Item.A(ppid));
        ArgumentNullException.ThrowIfNull(body);
        lock (_programGate)
        {
            _unformattedPrograms[ppid] = body.ToArray();
            ProcessProgramDirectory[ppid] = new ProcessProgramInfo { PPID = ppid };
        }
    }

    public byte[]? GetUnformattedProcessProgram(string ppid)
    {
        lock (_programGate)
            return _unformattedPrograms.TryGetValue(ppid, out var body) ? body.ToArray() : null;
    }

    internal IReadOnlyList<SecsGemProcessProgramSummary> GetProcessProgramSummaries()
    {
        lock (_programGate)
        {
            return ProcessProgramDirectory.Keys.OrderBy(id => id, StringComparer.Ordinal).Select(id =>
            {
                _unformattedPrograms.TryGetValue(id, out var binary);
                ProcessProgramDataStore.TryGetValue(id, out var formatted);
                return new SecsGemProcessProgramSummary(id, binary != null, binary?.Length,
                    formatted != null, formatted?.MDLN ?? "", formatted?.SOFTREV ?? "", formatted?.PPBODY.Count);
            }).ToArray();
        }
    }

    /// <summary>空集合表示全部删除。先验证所有 ID，未知 ID 返回 ACKC7=4，不部分删除。</summary>
    internal byte DeleteProcessPrograms(string[] ids)
    {
        lock (_programGate)
        {
            if (ids.Length == 0)
            {
                ProcessProgramDirectory.Clear();
                ProcessProgramDataStore.Clear();
                _unformattedPrograms.Clear();
                return 0;
            }
            if (ids.Any(id => !ProcessProgramDirectory.ContainsKey(id))) return 4;
            foreach (var id in ids)
            {
                ProcessProgramDirectory.TryRemove(id, out _);
                ProcessProgramDataStore.TryRemove(id, out _);
                _unformattedPrograms.Remove(id);
            }
            return 0;
        }
    }
}

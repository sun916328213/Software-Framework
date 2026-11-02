using System.IO;
using System.Text;
using Extension.Security.Licensing;

namespace Licensing.Authorization;

/// <summary>从激活工具导出的申请单里读到的内容。</summary>
/// <param name="MachineCode">已归一化为大写的 V2 机器码。</param>
/// <param name="ProductId">客户激活工具里编译进去的产品标识；老申请单可能没有。</param>
/// <param name="RecoveryRequest">SFR3 时间恢复请求；客户没有时间异常时为空。</param>
/// <param name="Created">客户侧生成时间（含时区偏移），仅供核对，不参与校验。</param>
internal sealed record LicenseRequest(string MachineCode, string? ProductId, string? RecoveryRequest, string? Created);

/// <summary>
/// 解析激活工具导出的申请单（SFREQ1）。格式定义与 <c>Licensing.Activation.LicenseRequestFile</c> 严格对应，
/// 两端改动必须同步，由 Licensing.Wpf.Tests 的端到端用例锁定。
/// </summary>
internal static class LicenseRequestFile
{
    internal const string Magic = "SFREQ1";

    private const int MaxBytes = 64 * 1024;

    internal static LicenseRequest Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("申请单内容为空。");

        // 客户常用记事本另存，行尾可能是 CRLF / LF / CR 的任意组合。
        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        if (lines[0].Trim().TrimStart('\uFEFF') != Magic)
            throw new ArgumentException("这不是激活工具导出的申请单：首行应为 " + Magic + "。请让客户在激活工具里点「导出申请单」重新生成。");

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 1; index < lines.Length; index++)
        {
            string line = lines[index].Trim();
            if (line.Length == 0) continue;
            int equal = line.IndexOf('=');
            // 畸形行直接跳过，而不是让一行脏数据废掉整张单子；关键字段缺失会在下面统一报错。
            if (equal <= 0) continue;
            values[line[..equal].Trim()] = line[(equal + 1)..].Trim();
        }

        if (!values.TryGetValue("Machine", out string? machine) || string.IsNullOrWhiteSpace(machine))
            throw new ArgumentException("申请单里没有机器码（缺少 Machine 行）。");
        machine = machine.ToUpperInvariant();
        if (!MachineCodeEx.IsV2Format(machine))
            throw new ArgumentException("申请单里的机器码格式不正确，应形如 V2-XXXX-XXXX-XXXX-XXXX。");

        values.TryGetValue("Product", out string? product);
        values.TryGetValue("Recovery", out string? recovery);
        values.TryGetValue("Created", out string? created);
        return new LicenseRequest(machine,
            string.IsNullOrWhiteSpace(product) ? null : product,
            string.IsNullOrWhiteSpace(recovery) ? null : recovery,
            string.IsNullOrWhiteSpace(created) ? null : created);
    }

    internal static LicenseRequest ReadFile(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists) throw new FileNotFoundException("找不到申请单文件。", path);
        if (file.Length > MaxBytes) throw new InvalidDataException("申请单文件过大，可能选错了文件。");
        return Parse(File.ReadAllText(path, Encoding.UTF8));
    }
}

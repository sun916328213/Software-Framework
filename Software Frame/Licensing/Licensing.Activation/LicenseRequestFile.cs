using System.IO;
using System.Text;

namespace Licensing.Activation;

/// <summary>
/// 离线申请单（SFREQ1）：客户把机器码、必要时把时间恢复请求交回供应商的唯一载体。
/// 纯文本、可直接邮件或微信传输，不依赖剪贴板（远程桌面和隔离网络下剪贴板经常不可用），也不含任何密钥。
/// 这不是信任凭据：供应商读到后仍须自行核实客户身份；恢复请求本身由 SFR3 签名保护。
/// 格式与 <c>Licensing.Authorization.LicenseRequestFile</c> 的解析严格对应，两端改动必须同步，
/// 由 Licensing.Wpf.Tests 的端到端用例锁定。
/// </summary>
internal static class LicenseRequestFile
{
    /// <summary>首行魔数，同时是版本号。将来改格式升到 SFREQ2，旧解析会明确拒绝而不是误读。</summary>
    internal const string Magic = "SFREQ1";

    internal const string Extension = ".sfreq";

    internal static string Create(string machineCode, string? productId, string? recoveryRequest)
    {
        var text = new StringBuilder();
        text.AppendLine(Magic);
        text.AppendLine("Machine=" + machineCode);
        if (!string.IsNullOrWhiteSpace(productId)) text.AppendLine("Product=" + productId);
        // 带时区偏移，供应商那边能看出客户机器时钟本身是否可疑。
        text.AppendLine("Created=" + DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:sszzz"));
        if (!string.IsNullOrWhiteSpace(recoveryRequest)) text.AppendLine("Recovery=" + recoveryRequest.Trim());
        return text.ToString();
    }

    /// <summary>用机器码末四位区分客户，避免多份申请单在下载目录里混在一起。</summary>
    internal static string SuggestedFileName(string machineCode)
    {
        string trimmed = machineCode.Trim();
        string tail = trimmed.Length <= 4 ? trimmed : trimmed[^4..];
        var safe = new StringBuilder(tail.Length);
        foreach (char character in tail)
            safe.Append(char.IsLetterOrDigit(character) ? character : '_');
        return "申请单_" + safe + "_" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + Extension;
    }

    internal static void Write(string path, string machineCode, string? productId, string? recoveryRequest) =>
        File.WriteAllText(path, Create(machineCode, productId, recoveryRequest), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
}

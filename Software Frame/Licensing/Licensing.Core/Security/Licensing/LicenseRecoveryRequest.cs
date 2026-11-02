using System.Text;

namespace Extension.Security.Licensing;

/// <summary>离线恢复请求不是授权；必须由厂商审核后附入同产品的新签名授权。</summary>
public static class LicenseRecoveryRequest
{
    public const string ExtraKey = "SoftwareFrame.TimeRecovery";

    public static string GetMachineCode(string request) => Parse(request).Machine;
    public static string GetProductId(string request) => Parse(request).Product;

    public static int GetRequestCapacity(string productId)
    {
        LicenseProduct.Validate(productId);
        return 5 + 64 + 1 + EncodeProduct(productId).Length + 1 + 22 + 1;
    }

    internal static string Create(string nonceHex, string machineCode, string productId) =>
        $"SFR3.{nonceHex}.{EncodeProduct(productId)}.{machineCode}";

    private static (string Product, string Machine) Parse(string request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request);
        if (request.Length > 192) throw new ArgumentException("恢复请求过长。", nameof(request));
        var parts = request.Split('.');
        if (parts.Length != 4 || parts[0] != "SFR3" || parts[1].Length != 64 ||
            parts[1].Any(c => !(c is >= '0' and <= '9' or >= 'A' and <= 'F')) || !MachineCodeEx.IsV2Format(parts[3]))
            throw new ArgumentException("恢复请求格式错误。", nameof(request));
        string product;
        try
        {
            string value = parts[2].Replace('-', '+').Replace('_', '/');
            value += new string('=', (4 - value.Length % 4) % 4);
            product = Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
        catch (FormatException ex) { throw new ArgumentException("恢复请求产品标识错误。", nameof(request), ex); }
        if (!LicenseProduct.IsValid(product) || EncodeProduct(product) != parts[2])
            throw new ArgumentException("恢复请求产品标识错误。", nameof(request));
        return (product, parts[3]);
    }

    private static string EncodeProduct(string product) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(product)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>厂商侧调用：审核请求后附入限时授权；随后仍必须用厂商私钥签发。</summary>
    public static void AttachTo(LicenseInfo info, string request)
    {
        ArgumentNullException.ThrowIfNull(info);
        var parsed = Parse(request);
        if (!string.Equals(info.ProductId, parsed.Product, StringComparison.Ordinal))
            throw new ArgumentException("恢复请求不属于填写的产品。", nameof(request));
        if (info.IsPermanent) throw new ArgumentException("永久授权不需要恢复时间记录。", nameof(info));
        if (!string.IsNullOrWhiteSpace(info.MachineCode) &&
            !string.Equals(info.MachineCode.Trim(), parsed.Machine, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("恢复请求与填写的机器码不一致。", nameof(request));
        info.MachineCode = parsed.Machine;
        info.Extra ??= new();
        info.Extra[ExtraKey] = request;
    }
}

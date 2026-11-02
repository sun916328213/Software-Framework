using System.IO;
using System.Reflection;

namespace Licensing.Trust;

/// <summary>
/// 全仓**唯一**的信任锚：签发方公钥与产品标识。
/// <para>
/// 公钥在编译期嵌入本程序集（见 <c>Build/ClientProduct.targets</c>），客户无法替换 ——
/// 这是防「客户把公钥换成自己的、再用自签授权绕过」的关键。
/// </para>
/// <para>
/// 各宿主（激活工具、Workbench、未来的 MachineApp）只引用这里，不再各自嵌一份：
/// 换身份时只重新生成这一个程序集，不会出现「两个程序用了不同公钥」的漂移。
/// </para>
/// </summary>
public static class TrustedIssuer
{
    private static readonly Lazy<string?> PublicKeyValue = new(ReadPublicKey);
    private static readonly Lazy<string?> ProductIdValue = new(ReadProductId);

    /// <summary>Base64 DER RSA 公钥；未配置时为空，调用方必须视为不可用，不能放行。</summary>
    public static string? PublicKey => PublicKeyValue.Value;

    /// <summary>编译期嵌入的产品标识；未配置时为空。</summary>
    public static string? ProductId => ProductIdValue.Value;

    /// <summary>公钥与产品标识都就绪才算配置完整。</summary>
    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(PublicKey) && !string.IsNullOrWhiteSpace(ProductId);

    private static string? ReadPublicKey()
    {
        using var stream = typeof(TrustedIssuer).Assembly.GetManifestResourceStream("Licensing.Trust.PublicKey");
        using var reader = stream is null ? null : new StreamReader(stream);
        string? value = reader?.ReadToEnd().Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? ReadProductId() =>
        typeof(TrustedIssuer).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(item => item.Key == "LicenseProductId")?.Value;
}

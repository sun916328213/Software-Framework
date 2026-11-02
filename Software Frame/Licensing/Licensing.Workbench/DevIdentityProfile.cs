using System.IO;
using System.Reflection;
using System.Xml.Linq;

namespace Licensing.Workbench;

/// <summary>
/// 开发期身份配置。仅 Debug 构建会在程序集里记下 <c>.local/client.props</c> 的路径，
/// 于是 Workbench 可以在**运行时**读到最新的公钥与产品标识 —— 改完配置重启即生效，不必重建。
/// <para>
/// 这不会削弱发行包：<c>ClientProduct.targets</c> 只在 Debug 嵌入这个路径，Release 不嵌入，
/// 客户拿到的 Activation 仍然只能使用编译期固化的公钥，无法换成自己的。
/// Workbench 本身是开发验证宿主，从不交付客户。
/// </para>
/// </summary>
internal static class DevIdentityProfile
{
    internal readonly record struct Identity(string PublicKey, string ProductId);

    /// <summary>读取失败或不是 Debug 构建时返回 null，调用方退回编译期嵌入值。</summary>
    internal static Identity? Read()
    {
        string? path = typeof(DevIdentityProfile).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(item => item.Key == "LicenseDevProfile")?.Value;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try
        {
            var document = XDocument.Load(path);
            string? product = Value(document, "LicenseProductId");
            string? keyFile = Value(document, "LicensePublicKeyFile");
            if (string.IsNullOrWhiteSpace(product) || string.IsNullOrWhiteSpace(keyFile)) return null;

            string directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
            // client.props 里写的是 $(MSBuildThisFileDirectory)xxx，MSBuild 变量在运行时不存在，需替换成实际目录。
            string resolved = keyFile.Replace("$(MSBuildThisFileDirectory)", directory + Path.DirectorySeparatorChar);
            if (!Path.IsPathRooted(resolved)) resolved = Path.Combine(directory, resolved);
            if (!File.Exists(resolved)) return null;

            string publicKey = File.ReadAllText(resolved).Trim();
            return string.IsNullOrWhiteSpace(publicKey) ? null : new Identity(publicKey, product);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            return null;
        }
    }

    private static string? Value(XDocument document, string name) =>
        document.Descendants(name).FirstOrDefault()?.Value?.Trim();
}

using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Extension.Security.Licensing;

namespace Licensing.Authorization;

/// <summary>仅供厂商工具使用：保存签发记录和源码调试配置，不保存私钥。</summary>
internal static class IssuerFiles
{
    internal static string ArchiveDirectory => Path.Combine(
        AppContext.BaseDirectory, "IssuerData", "Issued");

    internal static string Record(LicenseInfo info, string license, string? directory = null)
    {
        LicenseProduct.Validate(info.ProductId);
        if (!Guid.TryParseExact(info.Id, "N", out var id))
            throw new ArgumentException("授权编号不是有效的 GUID。", nameof(info));
        ArgumentException.ThrowIfNullOrWhiteSpace(license);
        if (!MachineCodeEx.IsV2Format(info.MachineCode))
            throw new ArgumentException("授权记录必须包含有效的 V2 机器码。", nameof(info));
        directory ??= ArchiveDirectory;
        Directory.CreateDirectory(directory);
        string stamp = info.IssuedAtUtc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        char[] invalid = Path.GetInvalidFileNameChars();
        string customer = string.Concat((info.Customer ?? string.Empty).Take(24)
            .Select(character => char.IsControl(character) || invalid.Contains(character) ? '_' : character));
        string path = Path.Combine(directory, $"{info.ProductId}_{customer}_{info.MachineCode}_{stamp}_{id:N}.lic");
        WriteAtomic(path, license, overwrite: false);
        return path;
    }

    internal static string SaveDebugClientConfiguration(string moduleRoot, string publicKey, string productId)
    {
        LicenseProduct.Validate(productId);
        if (productId == LicenseProduct.DefaultId)
            throw new ArgumentException("请填写实际产品标识，不能使用默认测试标识。", nameof(productId));
        // 公钥与产品标识由 Licensing.Trust 承载，开发配置也写给它。
        if (!File.Exists(Path.Combine(moduleRoot, "Licensing.Trust", "Licensing.Trust.csproj")))
            throw new DirectoryNotFoundException("未找到授权模块源码。请在当前源码环境中配置开发版信任锚。");
        byte[] der = Convert.FromBase64String(publicKey);
        using var rsa = RSA.Create();
        rsa.ImportRSAPublicKey(der, out int consumed);
        if (consumed != der.Length || rsa.KeySize < 2048)
            throw new ArgumentException("必须使用没有尾随数据的 2048 位及以上 RSA 公钥。", nameof(publicKey));
        byte[] canonical = rsa.ExportRSAPublicKey();
        string directory = Path.Combine(moduleRoot, ".local");
        Directory.CreateDirectory(directory);
        string fingerprint = Convert.ToHexString(SHA256.HashData(canonical));
        string keyFile = "public-" + fingerprint + ".key";
        WriteAtomic(Path.Combine(directory, keyFile), Convert.ToBase64String(canonical), overwrite: true);
        var document = new XDocument(new XElement("Project", new XElement("PropertyGroup",
            new XElement("LicensePublicKeyFile", new XAttribute("Condition", "'$(LicensePublicKeyFile)' == ''"),
                "$(MSBuildThisFileDirectory)" + keyFile),
            // 覆盖层的钥匙和它的钉必须成对写出：只给钥匙不给指纹会被构建拦死，
            // 那等于把「用临时身份自测」这条路堵掉，只剩下改入库指纹这一种绕法。
            new XElement("LicenseExpectedPublicKeyFingerprint",
                new XAttribute("Condition", "'$(LicenseExpectedPublicKeyFingerprint)' == ''"), fingerprint),
            new XElement("LicenseProductId", new XAttribute("Condition", "'$(LicenseProductId)' == ''"), productId))));
        string configuration = Path.Combine(directory, "client.props");
        WriteAtomic(configuration, document.ToString(), overwrite: true);
        return configuration;
    }

    private static void WriteAtomic(string path, string content, bool overwrite)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(content);
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

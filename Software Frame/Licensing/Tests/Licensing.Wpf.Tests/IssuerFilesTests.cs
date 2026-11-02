using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Extension.Security.Cryptography;
using Extension.Security.Licensing;
using Licensing.Authorization;
using Xunit;

namespace Licensing.Wpf.Tests;

public sealed class IssuerFilesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "IssuerFiles-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void SameVendorKeySignsDifferentMachinesAndAutomaticallyKeepsSeparateRecords()
    {
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        foreach (string machine in new[] { "V2-1111-2222-3333-4444", "V2-AAAA-BBBB-CCCC-DDDD" })
        {
            var info = new LicenseInfo { Customer = "测试客户", ProductId = "Packer.ModelA", MachineCode = machine, ExpireAtUtc = DateTime.MaxValue };
            string license = LicenseManagerEx.CreateBound(info, privateKey);
            string path = IssuerFiles.Record(info, license, _root);
            Assert.Equal(license, File.ReadAllText(path));
            Assert.Contains(machine, Path.GetFileName(path));
            Assert.Contains(info.Customer, Path.GetFileName(path));
            var verification = LicenseManagerEx.Verify(File.ReadAllText(path), publicKey, checkMachine: false, expectedProductId: info.ProductId);
            Assert.True(verification.Ok, verification.Message);
            Assert.Equal(machine, verification.License!.MachineCode);
            Assert.Equal(info.Customer, verification.License.Customer);
            Assert.Throws<IOException>(() => IssuerFiles.Record(info, "must not overwrite", _root));
            Assert.Equal(license, File.ReadAllText(path));
        }
        Assert.Equal(2, Directory.GetFiles(_root).Length);
        Assert.All(Directory.GetFiles(_root), path => Assert.DoesNotContain(privateKey, File.ReadAllText(path)));
    }

    [Fact]
    public async Task DebugProfileContainsOnlyPublicTrustAndClientLimitsItToDebug()
    {
        PrepareClientProject();
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        string configuration = IssuerFiles.SaveDebugClientConfiguration(_root, publicKey, "Packer.ModelA");
        Assert.DoesNotContain(privateKey, File.ReadAllText(configuration));
        string keyFile = Assert.Single(Directory.GetFiles(Path.GetDirectoryName(configuration)!, "*.key"));
        Assert.Equal(publicKey, File.ReadAllText(keyFile));
        var document = System.Xml.Linq.XDocument.Load(configuration);
        Assert.Equal("Packer.ModelA", document.Descendants("LicenseProductId").Single().Value);
        Assert.Equal("$(MSBuildThisFileDirectory)" + Path.GetFileName(keyFile), document.Descendants("LicensePublicKeyFile").Single().Value);
        string localFingerprint = Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(publicKey)));
        Assert.Equal(localFingerprint, document.Descendants("LicenseExpectedPublicKeyFingerprint").Single().Value);
        // 实际项目必须只在 Debug 导入本机覆盖层；入库信任锚对 Debug 和 Release 都生效。
        var client = System.Xml.Linq.XDocument.Load(Path.Combine(_root, "Build", "ClientProduct.targets"));
        var imports = client.Elements().Single().Elements("Import").ToArray();
        var overrideImport = Assert.Single(imports, item => item.Attribute("Project")!.Value.EndsWith(".local\\client.props"));
        Assert.Contains("'$(Configuration)' == 'Debug'", overrideImport.Attribute("Condition")!.Value);
        Assert.Contains(imports, item => item.Attribute("Project")!.Value.EndsWith("Trust.props")
                                        && item.Attribute("Condition") is null);
        var debug = await EvaluateProfile("Debug");
        Assert.Equal("Packer.ModelA", debug.GetProperty("LicenseProductId").GetString());
        Assert.Equal(keyFile, debug.GetProperty("LicensePublicKeyFile").GetString());
        // 覆盖层带着自己的钉，仓库默认值让位；否则临时身份根本无法自测。
        Assert.Equal(localFingerprint, debug.GetProperty("LicenseExpectedPublicKeyFingerprint").GetString());
        var release = await EvaluateProfile("Release");
        Assert.Equal(string.Empty, release.GetProperty("LicenseProductId").GetString());
        // Release 不再依赖任何人手工提供的公钥：仓库里钉住的就是默认值。
        string expected = Path.Combine(_root, "Build", "Keys", CommittedKeyFileName());
        Assert.Equal(Path.GetFullPath(expected),
            Path.GetFullPath(release.GetProperty("LicensePublicKeyFile").GetString()!));
        Assert.Equal(CommittedFingerprint(), release.GetProperty("LicenseExpectedPublicKeyFingerprint").GetString());
    }

    [Theory]
    [InlineData("private")]
    [InlineData("trailing")]
    [InlineData("weak")]
    [InlineData("defaultProduct")]
    public void InvalidConfigurationDoesNotReplaceTheWorkingProfile(string invalid)
    {
        PrepareClientProject();
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        string configuration = IssuerFiles.SaveDebugClientConfiguration(_root, publicKey, "Packer.ModelA");
        string original = File.ReadAllText(configuration);
        string candidate = invalid switch
        {
            "private" => privateKey,
            "trailing" => Convert.ToBase64String(Convert.FromBase64String(publicKey).Concat(new byte[] { 0 }).ToArray()),
            "weak" => RsaEx.GenerateKeyPair(1024).PublicKeyBase64,
            _ => publicKey
        };
        string product = invalid == "defaultProduct" ? LicenseProduct.DefaultId : "Packer.ModelA";
        var failure = Record.Exception(() => IssuerFiles.SaveDebugClientConfiguration(_root, candidate, product));
        Assert.True(failure is CryptographicException or ArgumentException, failure?.ToString() ?? "Invalid configuration was accepted.");
        Assert.Equal(original, File.ReadAllText(configuration));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(configuration)!, "*.key"));
    }

    private async Task<JsonElement> EvaluateProfile(string configuration)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.Environment.Remove("LicensePublicKeyFile");
        start.Environment.Remove("LicenseProductId");
        start.Environment.Remove("LicenseExpectedPublicKeyFingerprint");
        foreach (string argument in new[] { "msbuild", Path.Combine(_root, "Licensing.Trust", "Licensing.Trust.csproj"),
                     "-nologo", "-p:Configuration=" + configuration,
                     "-getProperty:LicensePublicKeyFile,LicenseProductId,LicenseExpectedPublicKeyFingerprint" })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        string output = await stdout;
        Assert.True(process.ExitCode == 0, output + await stderr);
        using var document = JsonDocument.Parse(output);
        return document.RootElement.GetProperty("Properties").Clone();
    }

    private void PrepareClientProject([CallerFilePath] string source = "")
    {
        // 公钥与产品标识现在由 Licensing.Trust 承载，评估它的 csproj 即可验证 Debug/Release 的配置差异。
        string client = Path.Combine(_root, "Licensing.Trust");
        Directory.CreateDirectory(client);
        string module = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", ".."));
        File.Copy(Path.Combine(module, "Licensing.Trust", "Licensing.Trust.csproj"), Path.Combine(client, "Licensing.Trust.csproj"));
        Directory.CreateDirectory(Path.Combine(_root, "Build"));
        foreach (string name in new[] { "ClientProduct.targets", "Trust.props" })
            File.Copy(Path.Combine(module, "Build", name), Path.Combine(_root, "Build", name));
    }

    private string TrustPropsPath => Path.Combine(_root, "Build", "Trust.props");

    private string CommittedKeyFileName() =>
        Path.GetFileName(System.Xml.Linq.XDocument.Load(TrustPropsPath)
            .Descendants("LicensePublicKeyFile").Single().Value);

    private string CommittedFingerprint() =>
        System.Xml.Linq.XDocument.Load(TrustPropsPath)
            .Descendants("LicenseExpectedPublicKeyFingerprint").Single().Value;

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}

using System.IO;
using System.Windows.Controls;
using Xunit;
using ActivationRequest = Licensing.Activation.LicenseRequestFile;
using AuthorizationRequest = Licensing.Authorization.LicenseRequestFile;

namespace Licensing.Wpf.Tests;

/// <summary>
/// 申请单是激活工具与授权工具之间唯一的离线交接载体，两端各自实现生成与解析。
/// 这里做端到端锁定：Licensing.Activation 写出来的，Licensing.Authorization 必须能读；格式漂移会直接红。
/// </summary>
public sealed class LicenseRequestFileTests
{
    private const string Machine = "V2-1111-2222-3333-4444";

    [Fact]
    public void ActivationExportIsReadableByAuthorization()
    {
        string text = ActivationRequest.Create(Machine, "Packer.ModelA", "SFR3.payload.signature");

        var parsed = AuthorizationRequest.Parse(text);

        Assert.Equal(Machine, parsed.MachineCode);
        Assert.Equal("Packer.ModelA", parsed.ProductId);
        Assert.Equal("SFR3.payload.signature", parsed.RecoveryRequest);
        Assert.NotNull(parsed.Created);
    }

    [Fact]
    public void ExportWithoutProductOrRecoveryStaysValid()
    {
        // 开发构建下没有产品标识，客户仍然要能把机器码发出来。
        var parsed = AuthorizationRequest.Parse(ActivationRequest.Create(Machine, null, null));

        Assert.Equal(Machine, parsed.MachineCode);
        Assert.Null(parsed.ProductId);
        Assert.Null(parsed.RecoveryRequest);
    }

    [Theory]
    [InlineData("\r\n")]
    [InlineData("\n")]
    public void ToleratesNotepadLineEndingsAndUnknownFields(string newline)
    {
        string text = string.Join(newline, "SFREQ1", "Machine=" + Machine.ToLowerInvariant(), "Note=客户自己加的一行", "Product=Packer.ModelA");
        var parsed = AuthorizationRequest.Parse(text);
        Assert.Equal(Machine, parsed.MachineCode);
        Assert.Equal("Packer.ModelA", parsed.ProductId);
    }

    [Fact]
    public void ToleratesBomInHeader()
    {
        var parsed = AuthorizationRequest.Parse("\uFEFFSFREQ1\nMachine=" + Machine + "\n");
        Assert.Equal(Machine, parsed.MachineCode);
    }

    [Theory]
    [InlineData("", "申请单内容为空")]
    [InlineData("Machine=V2-1111-2222-3333-4444", "SFREQ1")]
    [InlineData("SFREQ2\nMachine=V2-1111-2222-3333-4444", "SFREQ1")]
    [InlineData("SFREQ1\nProduct=Packer.ModelA", "没有机器码")]
    [InlineData("SFREQ1\nMachine=NOT-A-MACHINE", "格式不正确")]
    public void RejectsForeignOrIncompleteFilesWithAnActionableMessage(string text, string expected)
    {
        var failure = Assert.Throws<ArgumentException>(() => AuthorizationRequest.Parse(text));
        Assert.Contains(expected, failure.Message);
    }

    [Fact]
    public void ReadFileRejectsOversizedInput()
    {
        string root = Path.Combine(Path.GetTempPath(), "RequestFile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "huge.sfreq");
            File.WriteAllText(path, "SFREQ1\nMachine=" + Machine + "\n" + new string('x', 70 * 1024));
            Assert.Throws<InvalidDataException>(() => AuthorizationRequest.ReadFile(path));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void SuggestedFileNameUsesMachineCodeTailAndAvoidsInvalidCharacters()
    {
        string name = ActivationRequest.SuggestedFileName(Machine);
        Assert.StartsWith("申请单_4444_", name);
        Assert.EndsWith(ActivationRequest.Extension, name);
        Assert.DoesNotContain(':', name);
        // 极短输入不能抛越界异常。
        Assert.StartsWith("申请单_AB_", ActivationRequest.SuggestedFileName("AB"));
    }

    [Fact]
    public void WrittenFileRoundTripsThroughDisk()
    {
        string root = Path.Combine(Path.GetTempPath(), "RequestFile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "request.sfreq");
            ActivationRequest.Write(path, Machine, "Packer.ModelA", "SFR3.payload.signature");
            var parsed = AuthorizationRequest.ReadFile(path);
            Assert.Equal(Machine, parsed.MachineCode);
            Assert.Equal("SFR3.payload.signature", parsed.RecoveryRequest);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public Task ExpiryDaysDrivesPreviewAndDefaultsTo365() => WpfLicenseSessionTests.RunSta(() =>
    {
        string root = Path.Combine(Path.GetTempPath(), "RequestUi-" + Guid.NewGuid().ToString("N"));
        var environment = new Licensing.Authorization.IssuerEnvironment(root);
        var window = new Licensing.Authorization.MainWindow(environment);
        try
        {
            var days = (TextBox)window.FindName("ExpiryDays");
            var preview = (TextBlock)window.FindName("ExpiryPreview");
            Assert.Equal("365", days.Text);
            Assert.Contains(DateTime.Today.AddDays(365).ToString("yyyy-MM-dd"), preview.Text);

            days.Text = "30";
            Assert.Contains(DateTime.Today.AddDays(30).ToString("yyyy-MM-dd"), preview.Text);

            days.Text = "0";
            Assert.Contains("1–36500", preview.Text);
        }
        finally
        {
            window.Close();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
        return Task.CompletedTask;
    });

    [Fact]
    public Task PermanentDisablesDayInputAndExplainsNoExpiry() => WpfLicenseSessionTests.RunSta(() =>
    {
        string root = Path.Combine(Path.GetTempPath(), "RequestUi-" + Guid.NewGuid().ToString("N"));
        var environment = new Licensing.Authorization.IssuerEnvironment(root);
        var window = new Licensing.Authorization.MainWindow(environment);
        try
        {
            var days = (TextBox)window.FindName("ExpiryDays");
            var permanent = (CheckBox)window.FindName("Permanent");
            var preview = (TextBlock)window.FindName("ExpiryPreview");

            permanent.IsChecked = true;
            Assert.False(days.IsEnabled);
            Assert.Contains("永久有效", preview.Text);

            permanent.IsChecked = false;
            Assert.True(days.IsEnabled);
            Assert.Contains(DateTime.Today.AddDays(365).ToString("yyyy-MM-dd"), preview.Text);
        }
        finally
        {
            window.Close();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
        return Task.CompletedTask;
    });

    [Fact]
    public Task ActivationWindowExposesExportButton() => WpfLicenseSessionTests.RunSta(() =>
    {
        var window = new Licensing.Activation.MainWindow(null, null);
        try
        {
            var export = (Button)window.FindName("ExportButton");
            Assert.Equal("导出申请单", export.Content);
            // 开发构建也要能导出：客户第一件事就是把机器码发出来，跟能不能激活无关。
            Assert.True(export.IsEnabled);
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    });
}

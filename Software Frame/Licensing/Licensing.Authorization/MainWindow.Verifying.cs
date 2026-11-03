using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Extension.Security.Licensing;
using Microsoft.Win32;

namespace Licensing.Authorization;

/// <summary>验证授权页：用当前签发身份复核签名、产品与期限，不校验机器码。</summary>
public partial class MainWindow
{
    private void Verify_Click(object sender, RoutedEventArgs e)
    {
        if (_operation != null) return;
        try
        {
            var identity = RequireIdentity(); string product = RequireProduct();
            var dialog = new OpenFileDialog { Filter = "授权文件|*.lic", CheckFileExists = true };
            if (dialog.ShowDialog(this) != true) return;
            ShowVerification(ReadLicense(dialog.FileName), identity, product);
        }
        catch (Exception ex) { ShowError(ex); }
    }

    // 客户常把授权文本直接发在聊天或邮件里，厂商无需先存成文件再验证。
    private void VerifyPaste_Click(object sender, RoutedEventArgs e)
    {
        if (_operation != null) return;
        try
        {
            var identity = RequireIdentity(); string product = RequireProduct();
            string text = NormalizeLicenseText(VerifyInput.Text);
            if (text.Length == 0) { StatusText.Text = "请先把客户发来的授权文本粘贴到输入框。"; return; }
            ShowVerification(text, identity, product);
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void ShowVerification(string text, IssuerIdentity identity, string product)
    {
        var result = LicenseManagerEx.Verify(text, identity.PublicKey, checkMachine: false, expectedProductId: product);
        var info = result.License;
        Verification.Text = result.Status + "：" + result.Message + (info == null ? "" :
            $"\n客户：{info.Customer}\n产品：{info.ProductId}\n机器码：{info.MachineCode}\n期限：{(info.IsPermanent ? "永久" : info.ExpireAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"))}");
    }

    // 清洗规则与客户端粘贴导入一致：Base64Url 不含空白，去掉 BOM 与各类空白即可。
    private static string NormalizeLicenseText(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (char character in text)
            if (character != '\uFEFF' && !char.IsWhiteSpace(character)) builder.Append(character);
        return builder.ToString();
    }

    private static string ReadLicense(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > 1024 * 1024) throw new InvalidDataException("授权文件过大。");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}

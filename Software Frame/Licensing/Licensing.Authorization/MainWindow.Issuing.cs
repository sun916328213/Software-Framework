using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Extension.Security.Licensing;
using Microsoft.Win32;

namespace Licensing.Authorization;

/// <summary>
/// 签发页：申请单读取、期限换算与生成授权文件。
/// 界面状态与其余分页共用同一个 partial 类，拆分只为让一屏职责一眼可辨。
/// </summary>
public partial class MainWindow
{
    private string RequireProduct()
    {
        string product = Product.Text.Trim();
        LicenseProduct.Validate(product);
        if (product == LicenseProduct.DefaultId) throw new ArgumentException("请填写实际产品标识，不能使用默认测试标识。");
        return product;
    }

    /// <summary>读取客户发来的申请单：一次填好机器码，带时间恢复请求时一并填入。</summary>
    private void ReadRequest_Click(object sender, RoutedEventArgs e)
    {
        if (_operation != null) return;
        var dialog = new OpenFileDialog
        {
            Title = "选择客户发来的申请单",
            Filter = "授权申请单|*.sfreq|所有文件|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var request = LicenseRequestFile.ReadFile(dialog.FileName);
            Machine.Text = request.MachineCode;
            if (request.RecoveryRequest != null) RecoveryRequest.Text = request.RecoveryRequest;

            string filled = "已从申请单读取机器码" + (request.RecoveryRequest == null ? "" : "和时间恢复请求") + "。";
            if (request.Created != null) filled += "客户生成时间：" + request.Created + "。";
            // 产品不一致是最容易签错的地方：不擅自改产品，只明确提示由人确认。
            if (request.ProductId != null && !string.Equals(request.ProductId, Product.Text.Trim(), StringComparison.Ordinal))
                StatusText.Text = filled + $"\n注意：申请单里的产品标识是「{request.ProductId}」，当前填写的是「{Product.Text.Trim()}」。请确认没有签错产品。";
            else
                StatusText.Text = filled;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ShowError(ex);
        }
    }

    private void ExpiryDays_Changed(object sender, TextChangedEventArgs e)
    {
        // XAML 解析期间 TextChanged 会先于 ExpiryPreview 出现，此时直接跳过。
        if (ExpiryPreview == null || Permanent == null) return;
        UpdateExpiryPreview();
    }

    private void Permanent_Changed(object sender, RoutedEventArgs e)
    {
        if (ExpiryDays == null || ExpiryPreview == null || Trial == null) return;
        bool permanent = Permanent.IsChecked == true;
        ExpiryDays.IsEnabled = !permanent;
        Trial.IsEnabled = !permanent;
        UpdateExpiryPreview();
    }

    private void UpdateExpiryPreview()
    {
        if (ExpiryPreview == null) return;
        if (Permanent?.IsChecked == true) { ExpiryPreview.Text = "永久有效，不设到期时间。"; return; }
        ExpiryPreview.Text = TryExpiry(out DateTime expiry)
            ? "到期：" + expiry.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") + "（本机时间）"
            : "请输入 1–36500 之间的天数。";
    }

    /// <summary>到期时刻取第 N 天的 23:59:59，客户不会在到期当天下午突然被断。</summary>
    private bool TryExpiry(out DateTime expiryUtc)
    {
        expiryUtc = default;
        if (!int.TryParse(ExpiryDays.Text.Trim(), out int days) || days is <= 0 or > 36500) return false;
        DateTime local = DateTime.Today.AddDays(days).AddHours(23).AddMinutes(59).AddSeconds(59);
        expiryUtc = DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();
        return true;
    }

    private LicenseInfo BuildLicense()
    {
        if (string.IsNullOrWhiteSpace(Customer.Text)) throw new ArgumentException("请填写客户名称。");
        DateTime expiry;
        if (Permanent.IsChecked == true) expiry = DateTime.MaxValue;
        else if (!TryExpiry(out expiry)) throw new ArgumentException("授权天数应为 1–36500 之间的整数。");
        var info = new LicenseInfo { ProductId = RequireProduct(), Customer = Customer.Text.Trim(),
            MachineCode = Machine.Text.Trim().ToUpperInvariant(), IssuedAtUtc = DateTime.UtcNow, ExpireAtUtc = expiry };
        foreach (string feature in Features.Text.Split([',', ';', '\n', '\r', '，', '；'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            info.Features.Add(feature);
        foreach (string line in Extra.Text.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
        {
            int equal = line.IndexOf('=');
            if (equal > 0) info.Extra[line[..equal].Trim()] = line[(equal + 1)..].Trim();
        }
        if (Trial.IsChecked == true && !info.IsPermanent) info.Extra["Trial"] = "true";
        if (!string.IsNullOrWhiteSpace(RecoveryRequest.Text)) LicenseRecoveryRequest.AttachTo(info, RecoveryRequest.Text.Trim());
        return info;
    }

    private async void Issue_Click(object sender, RoutedEventArgs e)
    {
        if (_operation != null) return;
        GeneratedFile.Text = string.Empty;
        try
        {
            var identity = RequireIdentity();
            var info = BuildLicense();
            await RunAsync(async _ =>
            {
                string path = await Task.Run(() => IssuerFiles.Record(info, identity.Sign(info), _environment.ArchiveDirectory));
                GeneratedFile.Text = "已生成：" + path;
                File.WriteAllText(Path.Combine(_environment.DirectoryPath, "last-product.txt"), info.ProductId);
                StatusText.Text = "授权已生成并留档，可复制给客户。";
                RefreshRecords();
                Process.Start(new ProcessStartInfo("explorer.exe") { Arguments = "/select,\"" + path + "\"", UseShellExecute = true });
            });
        }
        catch (Exception ex) { ShowError(ex); }
    }
}

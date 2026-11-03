using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Extension.Security.Licensing;

namespace Licensing.Authorization;

/// <summary>授权记录页：装载历史 .lic、筛选、续期回填与打开留档目录。</summary>
public partial class MainWindow
{
    private void RefreshRecords()
    {
        _records = [];
        int rejected = 0;
        if (_identity != null && Directory.Exists(_environment.ArchiveDirectory))
        {
            foreach (string path in Directory.EnumerateFiles(_environment.ArchiveDirectory, "*.lic").OrderByDescending(File.GetLastWriteTimeUtc).Take(5000))
            {
                try
                {
                    var result = LicenseManagerEx.Verify(ReadLicense(path), _identity.PublicKey, checkMachine: false, expectedProductId: LicenseProduct.DefaultId);
                    // 仅用于厂商历史浏览。ProductMismatch 在此表示验签成功但不是保留测试产品，不代表客户可放行。
                    if (result.License != null && result.Status is LicenseStatus.Valid or LicenseStatus.Expired or LicenseStatus.ProductMismatch)
                        _records.Add(new IssuedRecord(path, result.License));
                    else rejected++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { rejected++; }
            }
        }
        ApplySearch();
        if (rejected > 0) StatusText.Text = $"有 {rejected} 条记录无法验证或读取，未加载；原文件保留。";
    }

    private void Search_Changed(object sender, TextChangedEventArgs e) { if (Records != null) ApplySearch(); }

    private void ApplySearch()
    {
        string query = RecordSearch.Text.Trim();
        Records.ItemsSource = _records.Where(record => (record.Customer + " " + record.Product + " " + record.Machine).Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    private void RefreshRecords_Click(object sender, RoutedEventArgs e)
    {
        if (_operation != null) return;
        try { RefreshRecords(); } catch (Exception ex) { ShowError(ex); }
    }

    private void Renew_Click(object sender, RoutedEventArgs e)
    {
        if (_operation != null || Records.SelectedItem is not IssuedRecord record) return;
        Product.Text = record.Product; Customer.Text = record.Customer; Machine.Text = record.Machine;
        Features.Text = string.Join(",", record.Info.Features ?? []);
        Extra.Text = string.Join(Environment.NewLine, (record.Info.Extra ?? []).Where(pair => pair.Key is not "SoftwareFrame.TimeRecovery" and not "Trial").Select(pair => pair.Key + "=" + pair.Value));
        RecoveryRequest.Clear(); Permanent.IsChecked = record.Info.IsPermanent; Trial.IsChecked = false;
        ExpiryDays.Text = "365"; Tabs.SelectedIndex = 0;
        StatusText.Text = "已载入续期信息，请确认新期限；旧授权记录不会覆盖。";
    }

    private void OpenRecords_Click(object sender, RoutedEventArgs e)
    {
        try { Directory.CreateDirectory(_environment.ArchiveDirectory); Process.Start(new ProcessStartInfo(_environment.ArchiveDirectory) { UseShellExecute = true }); }
        catch (Exception ex) { ShowError(ex); }
    }

    private sealed record IssuedRecord(string Path, LicenseInfo Info)
    {
        public string Customer => Info.Customer;
        public string Product => Info.ProductId;
        public string Machine => Info.MachineCode;
        public string Expiry => Info.IsPermanent ? "永久" : Info.ExpireAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        public string Issued => Info.IssuedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        public string Number => Info.Id;
    }
}

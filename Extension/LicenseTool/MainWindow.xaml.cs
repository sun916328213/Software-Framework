using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using Extension.Core.SecurityEx;
using Microsoft.Win32;

namespace LicenseTool
{
    /// <summary>
    /// License 授权工具主窗口（密钥管理 / 签发 / 验证）
    /// </summary>
    public partial class MainWindow : Window
    {
        private string _issuePrivateKey = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
            DpExpire.SelectedDate = DateTime.Today.AddYears(1);
            LblLocalMachine.Text = MachineCodeEx.GetMachineCode();
        }

        private void SetStatus(string text) => StatusText.Text = $"{DateTime.Now:HH:mm:ss}  {text}";

        #region ① 密钥管理

        private void BtnGenKeys_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var (pri, pub) = RsaEx.GenerateKeyPair();
                TxtPrivateKey.Text = pri;
                TxtPublicKey.Text = pub;
                SetStatus("密钥对已生成（2048bit）。");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "生成失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnLoadPri_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "密钥文件|*.key;*.txt;*.pem|所有文件|*.*" };
            if (dlg.ShowDialog() == true) TxtPrivateKey.Text = File.ReadAllText(dlg.FileName).Trim();
        }

        private void BtnLoadPub_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "密钥文件|*.key;*.txt;*.pem|所有文件|*.*" };
            if (dlg.ShowDialog() == true) TxtPublicKey.Text = File.ReadAllText(dlg.FileName).Trim();
        }

        private void BtnSavePri_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtPrivateKey.Text)) { MessageBox.Show("私钥为空。"); return; }
            var dlg = new SaveFileDialog { Filter = "私钥文件|*.key|文本文件|*.txt", FileName = "private.key" };
            if (dlg.ShowDialog() == true)
            {
                File.WriteAllText(dlg.FileName, TxtPrivateKey.Text.Trim(), Encoding.ASCII);
                SetStatus($"私钥已保存：{dlg.FileName}");
            }
        }

        private void BtnSavePub_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtPublicKey.Text)) { MessageBox.Show("公钥为空。"); return; }
            var dlg = new SaveFileDialog { Filter = "公钥文件|*.key|文本文件|*.txt", FileName = "public.key" };
            if (dlg.ShowDialog() == true)
            {
                File.WriteAllText(dlg.FileName, TxtPublicKey.Text.Trim(), Encoding.ASCII);
                SetStatus($"公钥已保存：{dlg.FileName}");
            }
        }

        #endregion

        #region ② 签发 License

        private void BtnFillLocalMachine_Click(object sender, RoutedEventArgs e)
        {
            TxtMachineCode.Text = MachineCodeEx.GetMachineCode();
        }

        private void BtnUseKeyTabPri_Click(object sender, RoutedEventArgs e)
        {
            _issuePrivateKey = TxtPrivateKey.Text.Trim();
            LblPriStatus.Text = string.IsNullOrEmpty(_issuePrivateKey)
                ? "未载入（【密钥管理】页私钥框为空）"
                : $"已载入（{TxtPrivateKey.Text.Trim().Length} 字符）。";
        }

        private LicenseInfo? BuildLicenseInfo(DateTime expireUtc, bool trial)
        {
            if (string.IsNullOrWhiteSpace(TxtCustomer.Text))
            {
                MessageBox.Show("请填写客户名称。");
                return null;
            }
            var info = new LicenseInfo
            {
                Customer = TxtCustomer.Text.Trim(),
                MachineCode = TxtMachineCode.Text.Trim(),
                IssuedAtUtc = DateTime.UtcNow,
                ExpireAtUtc = expireUtc,
            };
            foreach (string f in TxtFeatures.Text
                         .Split(new[] { '\r', '\n', ',', ';', '，', '；' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(s => s.Trim())
                         .Where(s => s.Length > 0))
                info.Features.Add(f);
            foreach (string line in TxtExtra.Text
                         .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int i = line.IndexOf('=');
                if (i > 0) info.Extra[line[..i].Trim()] = line[(i + 1)..].Trim();
            }
            if (trial) info.Extra["Trial"] = "true";
            return info;
        }

        private bool EnsurePrivateKey()
        {
            if (string.IsNullOrEmpty(_issuePrivateKey))
            {
                MessageBox.Show("请先点击「取自【密钥管理】页私钥框」载入私钥。");
                return false;
            }
            return true;
        }

        private DateTime ResolveExpireUtc()
        {
            if (ChkPermanent.IsChecked == true) return DateTime.MaxValue;
            DateTime local = DpExpire.SelectedDate ?? DateTime.Today.AddYears(1);
            // 过期到当天最后一秒（本地）再转 UTC
            return new DateTime(local.Year, local.Month, local.Day, 23, 59, 59, DateTimeKind.Local)
                .ToUniversalTime();
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!EnsurePrivateKey()) return;
                var info = BuildLicenseInfo(ResolveExpireUtc(), trial: false);
                if (info == null) return;
                TxtLicenseOut.Text = LicenseManagerEx.Create(info, _issuePrivateKey);
                SetStatus($"已签发：{info.Customer}，过期 {(info.IsPermanent ? "永久" : info.ExpireAtUtc.ToLocalTime().ToString("yyyy-MM-dd"))}。");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "签发失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCreateTrial_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!EnsurePrivateKey()) return;
                if (!int.TryParse(TxtTrialDays.Text.Trim(), out int days) || days <= 0)
                {
                    MessageBox.Show("试用天数必须是正整数。");
                    return;
                }
                var info = BuildLicenseInfo(DateTime.UtcNow.AddDays(days), trial: true);
                if (info == null) return;
                TxtLicenseOut.Text = LicenseManagerEx.Create(info, _issuePrivateKey);
                SetStatus($"已签发试用版：{info.Customer}，{days} 天。");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "签发失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSaveLic_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtLicenseOut.Text)) BtnCreate_Click(sender, e);
                if (string.IsNullOrWhiteSpace(TxtLicenseOut.Text)) return;
                var dlg = new SaveFileDialog { Filter = "License 文件|*.lic|文本文件|*.txt", FileName = $"{TxtCustomer.Text.Trim()}.lic" };
                if (dlg.ShowDialog() == true)
                {
                    File.WriteAllText(dlg.FileName, TxtLicenseOut.Text.Trim(), Encoding.ASCII);
                    SetStatus($"License 已保存：{dlg.FileName}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region ③ 验证 License

        private void BtnLoadLic_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "License 文件|*.lic;*.txt|所有文件|*.*" };
            if (dlg.ShowDialog() == true) TxtLicIn.Text = File.ReadAllText(dlg.FileName, Encoding.ASCII).Trim();
        }

        private void BtnUseKeyTabPub_Click(object sender, RoutedEventArgs e)
        {
            TxtVerifyPub.Text = TxtPublicKey.Text.Trim();
        }

        private void BtnVerify_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? feature = string.IsNullOrWhiteSpace(TxtRequiredFeature.Text)
                    ? null : TxtRequiredFeature.Text.Trim();
                var r = LicenseManagerEx.Verify(
                    TxtLicIn.Text.Trim(), TxtVerifyPub.Text.Trim(),
                    checkMachine: ChkCheckMachine.IsChecked != false,
                    requiredFeature: feature);

                TxtVerifyResult.Text = $"{r.Status}：{r.Message}";
                TxtVerifyResult.Foreground = r.Ok ? Brushes.DarkGreen : Brushes.DarkRed;

                if (r.License == null)
                {
                    TxtVerifyDetail.Text = "（无 License 明细：签名无效或格式错误）";
                }
                else
                {
                    var lic = r.License;
                    var sb = new StringBuilder();
                    sb.AppendLine($"单号　　：{lic.Id}");
                    sb.AppendLine($"客户　　：{lic.Customer}");
                    sb.AppendLine($"机器码　：{(string.IsNullOrEmpty(lic.MachineCode) ? "（未绑定）" : lic.MachineCode)}");
                    sb.AppendLine($"签发　　：{lic.IssuedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}（本地）");
                    sb.AppendLine($"过期　　：{(lic.IsPermanent ? "永久" : lic.ExpireAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm") + "（本地）")}");
                    sb.AppendLine($"功能点　：{(lic.Features.Count == 0 ? "（全部）" : string.Join(", ", lic.Features))}");
                    sb.AppendLine($"扩展字段：{(lic.Extra.Count == 0 ? "（无）" : string.Join("; ", lic.Extra.Select(kv => $"{kv.Key}={kv.Value}")))}");
                    TxtVerifyDetail.Text = sb.ToString();
                }
                SetStatus($"验证完成：{r.Status}。");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "验证失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}

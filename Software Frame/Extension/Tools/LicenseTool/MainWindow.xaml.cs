// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：MainWindow。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.IO、System.Linq、System.Text、System.Windows、System.Windows.Media。导入不代表所有类型都会在运行时加载。
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using Extension.Security.Cryptography;
using Extension.Security.Licensing;
using Microsoft.Win32;

namespace LicenseTool
{
    /// <summary>
    /// License 授权工具主窗口（密钥管理 / 签发 / 验证）
    /// </summary>
    public partial class MainWindow : Window
    {
        private string _issuePrivateKey = string.Empty;

        /// <summary>
        /// 初始化 MainWindow 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent、AddYears、GetMachineCode；具体条件和先后次序见方法体。
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            DpExpire.SelectedDate = DateTime.Today.AddYears(1);
            LblLocalMachine.Text = MachineCodeEx.GetMachineCode();
        }

        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetStatus。
        /// </summary>
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BtnLoadPri_Click。
        /// 正文中的关键协作调用包括 ShowDialog、Trim、ReadAllText；具体条件和先后次序见方法体。
        /// </summary>
        private void BtnLoadPri_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "密钥文件|*.key;*.txt;*.pem|所有文件|*.*" };
            if (dlg.ShowDialog() == true) TxtPrivateKey.Text = File.ReadAllText(dlg.FileName).Trim();
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BtnLoadPub_Click。
        /// 正文中的关键协作调用包括 ShowDialog、Trim、ReadAllText；具体条件和先后次序见方法体。
        /// </summary>
        private void BtnLoadPub_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "密钥文件|*.key;*.txt;*.pem|所有文件|*.*" };
            if (dlg.ShowDialog() == true) TxtPublicKey.Text = File.ReadAllText(dlg.FileName).Trim();
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BtnSavePri_Click。
        /// 正文中的关键协作调用包括 IsNullOrWhiteSpace、Show、ShowDialog、WriteAllText、Trim；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BtnSavePub_Click。
        /// 正文中的关键协作调用包括 IsNullOrWhiteSpace、Show、ShowDialog、WriteAllText、Trim；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BtnUseKeyTabPri_Click。
        /// 正文中的关键协作调用包括 Trim、IsNullOrEmpty；具体条件和先后次序见方法体。
        /// </summary>
        private void BtnUseKeyTabPri_Click(object sender, RoutedEventArgs e)
        {
            _issuePrivateKey = TxtPrivateKey.Text.Trim();
            LblPriStatus.Text = string.IsNullOrEmpty(_issuePrivateKey)
                ? "未载入（【密钥管理】页私钥框为空）"
                : $"已载入（{TxtPrivateKey.Text.Trim().Length} 字符）。";
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildLicenseInfo。
        /// 正文中的关键协作调用包括 IsNullOrWhiteSpace、Show、Trim、Where、Select；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 EnsurePrivateKey。
        /// 正文中的关键协作调用包括 IsNullOrEmpty、Show；具体条件和先后次序见方法体。
        /// </summary>
        private bool EnsurePrivateKey()
        {
            if (string.IsNullOrEmpty(_issuePrivateKey))
            {
                MessageBox.Show("请先点击「取自【密钥管理】页私钥框」载入私钥。");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ResolveExpireUtc。
        /// 正文中的关键协作调用包括 AddYears、ToUniversalTime；具体条件和先后次序见方法体。
        /// </summary>
        private DateTime ResolveExpireUtc()
        {
            if (ChkPermanent.IsChecked == true) return DateTime.MaxValue;
            DateTime local = DpExpire.SelectedDate ?? DateTime.Today.AddYears(1);
            // 过期到当天最后一秒（本地）再转 UTC
            return new DateTime(local.Year, local.Month, local.Day, 23, 59, 59, DateTimeKind.Local)
                .ToUniversalTime();
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BtnCreate_Click。
        /// 正文中的关键协作调用包括 EnsurePrivateKey、BuildLicenseInfo、ResolveExpireUtc、Create、SetStatus；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BtnCreateTrial_Click。
        /// 正文中的关键协作调用包括 EnsurePrivateKey、TryParse、Trim、Show、BuildLicenseInfo；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BtnSaveLic_Click。
        /// 正文中的关键协作调用包括 IsNullOrWhiteSpace、BtnCreate_Click、Trim、ShowDialog、WriteAllText；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BtnUseKeyTabPub_Click。
        /// 正文中的关键协作调用包括 Trim；具体条件和先后次序见方法体。
        /// </summary>
        private void BtnUseKeyTabPub_Click(object sender, RoutedEventArgs e)
        {
            TxtVerifyPub.Text = TxtPublicKey.Text.Trim();
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BtnVerify_Click。
        /// 正文中的关键协作调用包括 IsNullOrWhiteSpace、Trim、Verify、AppendLine、IsNullOrEmpty；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
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

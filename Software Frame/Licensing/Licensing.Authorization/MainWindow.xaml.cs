using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using System.Xml.Linq;
using Extension.Security.Licensing;
using Microsoft.Win32;

namespace Licensing.Authorization;

public partial class MainWindow : Window
{
    private readonly IssuerEnvironment _environment;
    private IssuerIdentity? _identity;
    private List<IssuedRecord> _records = [];
    private CancellationTokenSource? _operation;
    private bool _closeWhenDone;

    public MainWindow() : this(new IssuerEnvironment())
    {
        Loaded += PromptUnlock;
    }

    private void PromptUnlock(object sender, RoutedEventArgs e)
    {
        Loaded -= PromptUnlock;
        if (_identity != null) return;
        // 窗口按状态自适配：未创建显示“创建签发身份”，已创建显示“解锁”，不再靠置灰表达。
        new IssuerEnvironmentWindow(_environment) { Owner = this }.ShowDialog();
        ReloadEnvironment();
    }

    internal MainWindow(IssuerEnvironment environment)
    {
        _environment = environment;
        InitializeComponent();
        // 默认天数写在 XAML 里；这里补一次预览，避免 TextChanged 早于 ExpiryPreview 就绪而漏算。
        UpdateExpiryPreview();
        string settings = Path.Combine(environment.DirectoryPath, "last-product.txt");
        if (File.Exists(settings) && new FileInfo(settings).Length <= 4096) Product.Text = File.ReadAllText(settings).Trim();
        ReloadEnvironment();
    }

    private void ReloadEnvironment()
    {
        _identity?.Dispose(); _identity = null;
        try
        {
            _identity = _environment.Load();
            string settings = Path.Combine(_environment.DirectoryPath, "last-product.txt");
            if (_identity != null && string.IsNullOrWhiteSpace(Product.Text) && File.Exists(settings) && new FileInfo(settings).Length <= 4096)
                Product.Text = File.ReadAllText(settings).Trim();
            EnvironmentStatus.Text = _identity == null
                ? "尚未创建签发身份：点右上角「签发身份」，设置密码即可创建。"
                : "签发身份已解锁 · " + _identity.Fingerprint[..12] + " · 数据目录：" + _environment.DirectoryPath
                    + "\n" + TrustAnchorStatus(SourceModuleRoot(), _identity.Fingerprint);
            RefreshRecords();
        }
        catch (Exception ex)
        {
            EnvironmentStatus.Text = "签发身份未解锁：点右上角「签发身份」输入密码；损坏时从备份恢复。";
            StatusText.Text = ex.Message;
        }
    }

    private IssuerIdentity RequireIdentity() => _identity ?? throw new InvalidOperationException("请先解锁签发身份（右上角「签发身份」）。");

    /// <summary>
    /// 发布前的信任锚预检：客户包只允许钉仓库里那把公钥，本机身份对不上必然被发布脚本拒绝。
    /// 在界面先讲清楚，免得用户等完几分钟 dotnet publish 才看到真正原因。
    /// </summary>
    internal static string TrustAnchorStatus(string moduleRoot, string identityFingerprint)
    {
        try
        {
            string path = Path.Combine(moduleRoot, "Build", "Trust.props");
            if (!File.Exists(path)) return "仓库信任锚缺失：没有 Build\\Trust.props，这份源码不能生成客户包。";
            var document = XDocument.Load(path);
            string pinned = (((string?)document.Descendants("LicenseExpectedPublicKeyFingerprint").FirstOrDefault()) ?? string.Empty).Trim();
            if (pinned.Length != 64) return "仓库信任锚未钉指纹：Build\\Trust.props 缺少 LicenseExpectedPublicKeyFingerprint。";

            const string variable = "$(MSBuildThisFileDirectory)";
            string declared = (((string?)document.Descendants("LicensePublicKeyFile").FirstOrDefault()) ?? string.Empty).Trim();
            if (declared.StartsWith(variable, StringComparison.Ordinal))
            {
                string keyPath = Path.Combine(moduleRoot, "Build",
                    declared[variable.Length..].Replace('\\', Path.DirectorySeparatorChar));
                if (!File.Exists(keyPath)) return "仓库信任锚缺文件：Build\\Trust.props 指向的公钥不在，请检查 git 状态。";
            }

            return string.Equals(pinned, identityFingerprint, StringComparison.OrdinalIgnoreCase)
                ? $"信任锚一致：本机身份就是仓库钉住的签发公钥（{pinned[..12]}…），可以生成客户包。"
                : $"信任锚不一致：仓库钉 {pinned[..12]}…，本机签发身份 {identityFingerprint[..12]}…\n"
                    + "生成客户包会被拒绝。换签发身份要把新公钥写进 Build\\Keys\\，更新 Build\\Trust.props 并提交，再重新发布。";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
        {
            return "信任锚状态读取失败：" + ex.Message;
        }
    }

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

    private async void Publish_Click(object sender, RoutedEventArgs e)
    {
        if (_operation != null) return;
        try
        {
            var identity = RequireIdentity(); string product = RequireProduct();
            var dialog = new OpenFolderDialog { Title = "选择客户包输出位置" };
            if (dialog.ShowDialog(this) != true) return;
            string output = ResolvePublishDirectory(dialog.FolderName, product);
            await RunAsync(async token =>
            {
                StatusText.Text = "正在生成并审计客户包，请等待（通常 1–3 分钟）；可取消。\n输出目录：" + output;
                await ClientPackagePublisher.PublishAsync(SourceModuleRoot(), identity.PublicKey, product, output, token);
                StatusText.Text = "客户包已生成并通过静态审计。请完整复制目录，并进行目标环境验收。\n" + output;
                Process.Start(new ProcessStartInfo(output) { UseShellExecute = true });
            }, cancellable: true);
        }
        catch (Exception ex) { ShowError(ex, writeLog: true); }
    }

    /// <summary>
    /// 所选目录为空就直接用；已有内容则在下面新建带时间戳的子目录。
    /// 发布脚本拒绝写入非空目录，但让用户自己去建文件夹是没必要的负担，这里代劳且绝不覆盖或删除已有文件。
    /// </summary>
    internal static string ResolvePublishDirectory(string selected, string product)
    {
        if (!Directory.Exists(selected) || !Directory.EnumerateFileSystemEntries(selected).Any()) return selected;
        string stem = (product.Length > 40 ? product[..40] : product) + "_" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string candidate = Path.Combine(selected, stem);
        for (int suffix = 2; Directory.Exists(candidate); suffix++)
            candidate = Path.Combine(selected, stem + "_" + suffix);
        Directory.CreateDirectory(candidate);
        return candidate;
    }

    private void Environment_Click(object sender, RoutedEventArgs e)
    {
        if (_operation != null) return;
        new IssuerEnvironmentWindow(_environment) { Owner = this }.ShowDialog();
        ReloadEnvironment();
    }

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

    private async Task RunAsync(Func<CancellationToken, Task> action, bool cancellable = false)
    {
        using var operation = new CancellationTokenSource(); _operation = operation; CancelOperation.IsEnabled = cancellable;
        try { await action(operation.Token); }
        catch (OperationCanceledException) when (operation.IsCancellationRequested) { StatusText.Text = "操作已取消，未完成的发布目录不可交付。"; }
        finally { _operation = null; CancelOperation.IsEnabled = false; if (_closeWhenDone) Close(); }
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => _operation?.Cancel();
    private void ShowError(Exception ex, bool writeLog = false)
    {
        StatusText.Text = "操作失败：" + ex.Message;
        if (!writeLog) return;
        // 发布链路会带上几十行 dotnet 输出，状态栏放不下；落盘并定位，用户能直接看到真正原因。
        try
        {
            string path = Path.Combine(Path.GetTempPath(), "Licensing.Authorization-失败日志-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");
            File.WriteAllText(path, ex.ToString());
            StatusText.Text += "\n\n完整日志已保存并打开所在文件夹：" + path;
            Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true });
        }
        catch (Exception logFailure) when (logFailure is IOException or UnauthorizedAccessException or Win32Exception)
        {
            StatusText.Text += "\n（日志写入失败：" + logFailure.Message + "）";
        }
    }
    private static string SourceModuleRoot([CallerFilePath] string file = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, ".."));
    protected override void OnClosing(CancelEventArgs e)
    {
        if (_operation != null) { e.Cancel = true; _closeWhenDone = true; _operation.Cancel(); }
        base.OnClosing(e);
    }
    protected override void OnClosed(EventArgs e) { _identity?.Dispose(); _environment.Dispose(); base.OnClosed(e); }

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

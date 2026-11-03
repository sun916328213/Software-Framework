using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Xml;
using System.Xml.Linq;

namespace Licensing.Authorization;

/// <summary>
/// 授权签发工具主窗口。本文件只保留窗口生命周期、签发身份状态与统一的执行/报错通道；
/// 分页行为按 Tab 拆在同名 partial 文件里，行为与拆分前一致。
/// </summary>
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

    private void Environment_Click(object sender, RoutedEventArgs e)
    {
        if (_operation != null) return;
        new IssuerEnvironmentWindow(_environment) { Owner = this }.ShowDialog();
        ReloadEnvironment();
    }

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

    private static string SourceModuleRoot([CallerFilePath] string file = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, ".."));

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

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_operation != null) { e.Cancel = true; _closeWhenDone = true; _operation.Cancel(); }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e) { _identity?.Dispose(); _environment.Dispose(); base.OnClosed(e); }
}

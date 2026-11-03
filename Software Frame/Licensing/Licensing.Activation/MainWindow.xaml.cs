using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Windows;
using Licensing.Native.Client;
using Licensing.Trust;
using Licensing.Wpf;
using Microsoft.Win32;

namespace Licensing.Activation;

public partial class MainWindow : Window
{
    private NativeLicenseSession? _store;
    private string _initializationFailure = string.Empty;
    /// <summary>编译期嵌入的产品标识；开发构建下为空。导出申请单时写进文件，供供应商核对没签错产品。</summary>
    private string? _productId;
    /// <summary>本次会话已生成、尚未被恢复授权消费的 SFR3 请求。导出申请单时一并写入。</summary>
    private string? _pendingRecovery;

    public MainWindow() : this(null, null, readEmbeddedConfiguration: true) { }

    internal MainWindow(string? key, string? productId, bool readEmbeddedConfiguration = false)
    {
        InitializeComponent();
        FooterHint.Text += CoreBuildStamp();
        _productId = productId;
        try
        {
            MachineCode.Text = NativeLicenseSession.GetMachineCode();
            if (readEmbeddedConfiguration)
            {
                // 公钥与产品标识由 Licensing.Trust 统一提供，本程序不再各自嵌入一份。
                key = TrustedIssuer.PublicKey;
                productId = TrustedIssuer.ProductId;
                _productId = productId;
            }
            if (string.IsNullOrWhiteSpace(key))
            {
                ShowUnavailable("这是开发构建：编译时没有嵌入供应商公钥，不能激活客户授权。",
                    "● 开发构建 · 缺少供应商公钥，仅供本机调试");
                return;
            }
            if (string.IsNullOrWhiteSpace(productId))
            {
                ShowUnavailable("这是开发构建：编译时没有嵌入产品标识，不能激活客户授权。",
                    "● 开发构建 · 缺少产品标识，仅供本机调试");
                return;
            }
            _store = new NativeLicenseSession(key, productId);
            Title = $"激活 — {productId}";
            RecoveryButton.IsEnabled = true;
            ClearButton.IsEnabled = true;
            RefreshStatus();
        }
        catch (Exception ex) when (IsExpectedFailure(ex))
        {
            CopyButton.IsEnabled = !string.IsNullOrEmpty(MachineCode.Text);
            ShowUnavailable($"初始化失败：{ex.Message}", "● 本机授权组件不可用");
        }
    }

    /// <summary>顶部摘要与下方原因必须说同一种缺失：笼统写「未嵌入发行身份」会在只缺产品标识时自相矛盾。</summary>
    private void ShowUnavailable(string reason, string summary)
    {
        _initializationFailure = reason +
            "\n\n这是源码目录里的 Debug 构建，不是给客户的发行包；客户机器上运行的包由厂商工具“生成客户工具包”产出，已内置发行身份，不会出现本提示。" +
            "\n本机自测（可选）：在授权工具完成签发环境初始化、填写产品标识，点击“生成客户工具包”产出发行包，运行该目录里的激活工具即为已配置版本。" +
            "\n若提示组件加载失败：客户包必须整目录交付，只复制 EXE 会缺 Licensing.Core/Licensing.Trust 等 DLL，请供应商重新提供完整 Windows x64 客户包。";
        // 主按钮保持可点：变灰会让人以为是程序坏了，点一下能直接看到该怎么做。
        LicenseSummary.Foreground = LicenseStatusBrushes.NotActivated;
        LicenseSummary.Text = summary;
        Status.Text = _initializationFailure;
    }

    private static bool IsExpectedFailure(Exception ex) =>
        ex is IOException or InvalidDataException or UnauthorizedAccessException or SecurityException or CryptographicException
            or FormatException or InvalidOperationException or ArgumentException or Win32Exception
            or ExternalException or PlatformNotSupportedException or DllNotFoundException or EntryPointNotFoundException
            or BadImageFormatException;

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (TrySetClipboard(MachineCode.Text))
        {
            Status.Text = "机器码已复制到剪贴板。";
            return;
        }
        // 复制失败时不要只报错：直接选中，让客户按一次 Ctrl+C 就能完成。
        MachineCode.Focus();
        MachineCode.SelectAll();
        Status.Text = "剪贴板被其他程序占用（远程桌面、剪贴板工具常见）。机器码已选中，按 Ctrl+C 复制即可；"
            + "或者点「导出申请单」存成文件发给供应商，不需要剪贴板。";
    }

    /// <summary>
    /// 把机器码（和已生成的时间恢复请求）存成 .sfreq 交给供应商。
    /// 隔离网络、远程桌面、只允许文件传输的场合，这是唯一可靠的交接方式。
    /// </summary>
    private void Export_Click(object sender, RoutedEventArgs e)
    {
        string machine = MachineCode.Text.Trim();
        if (machine.Length == 0)
        {
            Status.Text = "没有读到本机机器码，无法导出申请单。请先点「刷新状态」重试。";
            return;
        }
        var dialog = new SaveFileDialog
        {
            Title = "保存申请单（发给供应商）",
            Filter = "授权申请单|*.sfreq",
            FileName = LicenseRequestFile.SuggestedFileName(machine)
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            LicenseRequestFile.Write(dialog.FileName, machine, _productId, _pendingRecovery);
            Status.Text = (_pendingRecovery == null
                    ? "申请单已保存，请把它发给供应商换取授权文件。"
                    : "申请单已保存（已包含时间恢复请求），请发给供应商。")
                + "\n" + dialog.FileName;
        }
        catch (Exception ex) when (IsExpectedFailure(ex)) { Status.Text = $"保存申请单失败：{ex.Message}"; }
    }

    /// <summary>输入框有内容按粘贴的授权激活；留空则打开文件选择。一个按钮覆盖两种交付方式。</summary>
    private void Activate_Click(object sender, RoutedEventArgs e)
    {
        if (_store == null)
        {
            Status.Text = _initializationFailure;
            MessageBox.Show(this, _initializationFailure, "这是开发构建", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (string.IsNullOrWhiteSpace(LicenseInput.Text)) { ActivateFromFile(); return; }
        try
        {
            var result = _store.ImportText(LicenseInput.Text);
            ShowResult(result);
            // 激活成功后清空输入框，避免授权文本长期留在界面上。
            if (result.Ok) { LicenseInput.Clear(); _pendingRecovery = null; }
        }
        catch (Exception ex) when (IsExpectedFailure(ex)) { Status.Text = $"激活失败：{ex.Message}"; }
    }

    private void ActivateFromFile()
    {
        var dialog = new OpenFileDialog { Filter = "授权文件|*.lic", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var result = _store!.ImportFile(dialog.FileName);
            ShowResult(result);
            // 恢复授权导入成功后，本机待恢复请求已被一次性消费，不要再写进后续申请单。
            if (result.Ok) _pendingRecovery = null;
        }
        catch (Exception ex) when (IsExpectedFailure(ex)) { Status.Text = $"激活失败：{ex.Message}"; }
    }

    /// <summary>只删本机授权文件。时间状态保留，避免“清除”变成把系统时间调回去的后门。</summary>
    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (_store == null) { Status.Text = _initializationFailure; return; }
        if (MessageBox.Show(this,
                "将删除本机授权文件，之后需要重新导入供应商提供的授权才能使用。\n\n" +
                "· 不会退还授权，也不会解除机器绑定\n" +
                "· 时间记录会保留，不能借此把系统时间调回去继续使用\n\n确定清除本机授权？",
                "确认清除", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        try
        {
            bool removed = _store.ClearLocalLicense();
            RefreshStatus();
            Status.Text = (removed ? "已清除本机授权。" : "本机没有可清除的授权。") +
                "\n如需继续使用，请向供应商索取授权后重新激活。";
        }
        catch (Exception ex) when (IsExpectedFailure(ex)) { Status.Text = $"清除失败：{ex.Message}"; }
    }

    private void Recovery_Click(object sender, RoutedEventArgs e)
    {
        if (_store == null) return;
        try
        {
            string request = _store.CreateTimeRecoveryRequest();
            // 记下来：客户接着点「导出申请单」时会自动带上，不用再手工搬运这一长串。
            _pendingRecovery = request;
            Status.Text = $"请先校正系统时间，将以下请求发送给供应商。供应商审核并签发恢复授权后，点「激活」重新导入。\n再次生成会使之前的请求失效。\n\n{request}";
            Status.Text += TrySetClipboard(request)
                ? "\n\n已复制到剪贴板。也可以点「导出申请单」，把机器码和这条请求一起存成文件发出。"
                : "\n\n剪贴板暂不可用，请点「导出申请单」把机器码和这条请求一起存成文件。";
        }
        catch (Exception ex) when (IsExpectedFailure(ex)) { Status.Text = $"生成恢复请求失败：{ex.Message}"; }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshStatus();

    private void RefreshStatus()
    {
        if (_store == null) return;
        try
        {
            ShowResult(_store.Load());
        }
        catch (Exception ex) when (IsExpectedFailure(ex)) { Status.Text = $"读取失败：{ex.Message}"; }
    }

    protected override void OnClosed(EventArgs e)
    {
        _store?.Dispose();
        base.OnClosed(e);
    }

    private void ShowResult(NativeVerifyResult result)
    {
        UpdateSummary(result);
        var info = result.License;
        Status.Text = result.Message;
        if (info != null)
            Status.Text += $"\n客户：{info.Customer}\n产品：{info.ProductId}\n授权编号：{info.Id}\n有效期：{(info.IsPermanent ? "永久" : info.ExpireAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"))}";
        if (result.Ok && _store != null)
            Status.Text += $"\n保存位置（当前 Windows 用户）：{_store.LicensePath}";
    }

    /// <summary>常驻摘要：打开界面就能看到是否有效、何时到期、还剩多少天，不用去读下面的日志。</summary>
    private void UpdateSummary(NativeVerifyResult result)
    {
        var banner = LicenseBanner.For(result);
        LicenseSummary.Foreground = LicenseStatusBrushes.Of(banner.State);
        LicenseSummary.Text = banner.Text;
    }

    /// <summary>客户包由 dotnet publish 现编，界面直接标出验签组件的构建时间，省得靠文件属性猜自己跑的是哪一份。</summary>
    private static string CoreBuildStamp()
    {
        string path = typeof(Extension.Security.Licensing.LicenseInfo).Assembly.Location;
        return path.Length == 0 || !File.Exists(path)
            ? string.Empty
            : $"\n验签组件 Licensing.Core.dll 构建于 {new FileInfo(path).LastWriteTime:yyyy-MM-dd HH:mm}。";
    }

    // 剪贴板可能被其他进程短暂占用，重试几次比直接报错更符合预期。
    private static bool TrySetClipboard(string text)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try { Clipboard.SetText(text); return true; }
            catch (ExternalException) { Thread.Sleep(60); }
        }
        return false;
    }
}

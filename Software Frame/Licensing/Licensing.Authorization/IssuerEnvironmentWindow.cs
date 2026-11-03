using System.ComponentModel;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace Licensing.Authorization;

/// <summary>
/// 签发身份管理窗口。按“未初始化 / 已锁定 / 已解锁”三种状态重建界面，
/// 每个状态只出现当前可用的操作，避免一排按钮靠置灰表达状态。
/// </summary>
internal sealed class IssuerEnvironmentWindow : Window
{
    private readonly IssuerEnvironment _environment;
    private readonly TextBlock _status = new() { Name = "IdentityStatus", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 14) };
    private readonly PasswordBox _password = new() { Name = "IdentityPassword", Margin = new Thickness(0, 4, 0, 8) };
    private readonly PasswordBox _backupPassword = new() { Name = "BackupPassword", Margin = new Thickness(0, 4, 0, 8) };
    private readonly PasswordBox _restorePassword = new() { Name = "RestorePassword", Margin = new Thickness(0, 4, 0, 8) };
    private readonly PasswordBox _confirmation = new() { Margin = new Thickness(0, 4, 0, 8) };
    private readonly StackPanel _body = new();
    private bool _busy, _closeWhenDone;
    // 冻结后才能跨 STA 线程共享：测试会在另一个 STA 线程直接构造本窗口，未冻结的静态画刷会在那里抛异常。
    private static readonly Brush ErrorBrush = CreateErrorBrush();
    private static Brush CreateErrorBrush()
    {
        var brush = new SolidColorBrush(Color.FromRgb(0xB7, 0x1C, 0x1C));
        brush.Freeze();
        return brush;
    }

    internal IssuerEnvironmentWindow(IssuerEnvironment environment)
    {
        _environment = environment;
        Title = "签发身份";
        Width = 620; MinWidth = 520;
        // 状态重建后内容高度会变，固定 Height 会把「从加密备份恢复」挤到看不见的地方；交给内容自适应，只设上限。
        SizeToContent = SizeToContent.Height; MaxHeight = 820;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(_status);
        panel.Children.Add(_body);
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Rebuild();
    }

    private void Rebuild()
    {
        _body.Children.Clear();
        _password.Clear();
        _backupPassword.Clear();
        _restorePassword.Clear();
        _confirmation.Clear();
        if (!_environment.IsInitialized) BuildInitialize();
        else if (!_environment.IsUnlocked) BuildUnlock();
        else BuildUnlocked();
        Loaded -= FocusPassword;
        Loaded += FocusPassword;
    }

    private void FocusPassword(object sender, RoutedEventArgs e) => _password.Focus();

    private void BuildInitialize()
    {
        Inform("这台机器还没有签发身份。设置密码后会生成新的 RSA 3072 密钥对，用来给客户签发授权。");
        _body.Children.Add(Label("密码（至少 12 字符，请单独保管；遗忘无法找回）"));
        _body.Children.Add(_password);
        _body.Children.Add(Label("确认密码"));
        _body.Children.Add(_confirmation);
        var create = PrimaryButton("创建签发身份", "InitializeButton");
        create.Click += Initialize_Click;
        _body.Children.Add(create);
        _body.Children.Add(Note("这台机器以前签发过授权？不要新建身份，改用下面的恢复或迁移，否则已发出的授权会全部失效。"));
        RestoreBlock();
        if (_environment.HasLegacyLocalIdentity)
            _body.Children.Add(SecondaryButton("迁移原电脑的本机身份（仅一次）", "MigrateButton", MigrateLocal_Click));
    }

    private void BuildUnlock()
    {
        Inform("签发身份已存在，尚未解锁。密码不会保存到磁盘。");
        _body.Children.Add(Label("解锁密码"));
        _body.Children.Add(_password);
        var unlock = PrimaryButton("解锁", "UnlockIdentity");
        unlock.Click += Unlock_Click;
        _body.Children.Add(unlock);
        _body.Children.Add(Note("忘记密码只能从 .sfib 备份恢复，没有后门。"));
        RestoreBlock();
    }

    private void BuildUnlocked()
    {
        using var identity = _environment.Load();
        Inform("签发身份已解锁，本次会话可以继续签发。关闭工具会清除会话密钥。");
        _body.Children.Add(KeyValue("指纹", identity?.Fingerprint[..16] ?? "—"));
        _body.Children.Add(KeyValue("数据目录", _environment.DirectoryPath));
        _body.Children.Add(Note("换电脑请复制整个工具文件夹（含 IssuerData），输入同一密码即可，无需原 Windows 账户。不要把工具或身份目录交给客户。"));
        _body.Children.Add(Label("新备份密码（至少 12 字符，可与解锁密码不同）"));
        _body.Children.Add(_backupPassword);
        _body.Children.Add(Label("确认备份密码"));
        _body.Children.Add(_confirmation);
        var backup = PrimaryButton("创建加密备份", "BackupButton");
        backup.Click += Backup_Click;
        _body.Children.Add(backup);
        RestoreBlock();
    }

    /// <summary>
    /// 恢复用独立的「备份密码」框：解锁密码对应身份目录里的凭据，备份密码对应这份 .sfib 自己。
    /// 两者挤在同一个框里，恢复时填解锁密码只会得到"密码错误"，看起来就像按钮坏了。
    /// </summary>
    private void RestoreBlock()
    {
        _body.Children.Add(Label("备份密码（填所选 .sfib 自己的密码，不是上面的解锁密码）"));
        _body.Children.Add(_restorePassword);
        _body.Children.Add(SecondaryButton("从加密备份恢复", "RestoreButton", Restore_Click));
        _body.Children.Add(Note("备份与本机不是同一把身份时，会先摊开两把指纹让你确认再替换；替换后本机身份签发的授权全部失效。"));
    }

    private static TextBlock Label(string text) =>
        new() { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };

    private static TextBlock Note(string text) =>
        new() { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = SystemColors.GrayTextBrush, Margin = new Thickness(0, 10, 0, 4) };

    private static TextBlock KeyValue(string key, string value) =>
        new() { Text = key + "：" + value, TextWrapping = TextWrapping.Wrap, FontFamily = new System.Windows.Media.FontFamily("Consolas"), Margin = new Thickness(0, 2, 0, 2) };

    private static Button PrimaryButton(string text, string name) =>
        new() { Name = name, Content = text, Padding = new Thickness(18, 6, 18, 6), Margin = new Thickness(0, 10, 0, 4), MinWidth = 160, HorizontalAlignment = HorizontalAlignment.Left };

    private static Button SecondaryButton(string text, string name, RoutedEventHandler handler)
    {
        var button = new Button { Name = name, Content = text, Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 2, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
        button.Click += handler;
        return button;
    }

    private async void Initialize_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || MessageBox.Show(this, "只在从未签发过授权时创建。已有授权请改用恢复或迁移，否则新客户包与旧授权不匹配。确定创建新的签发身份？",
            "确认创建", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        string? password = ConfirmedPassword();
        if (password == null) return;
        await RunAsync(() => { using var identity = _environment.Initialize(password); });
    }

    private async void Unlock_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        string password = _password.Password;
        await RunAsync(() => { using var identity = _environment.Load(password) ?? throw new InvalidOperationException("尚未创建签发身份。"); });
        // 解锁成功即关闭，让主窗口立刻刷新；失败时留在窗口里继续试。
        if (_environment.IsUnlocked) Close();
    }

    private async void MigrateLocal_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        string? password = ConfirmedPassword();
        if (password == null) return;
        await RunAsync(() => { using var identity = _environment.MigrateLocal(password); });
    }

    private async void Backup_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (_backupPassword.Password.Length < 12) { Fail("新建备份的密码至少 12 字符，请填在「新备份密码」里。", _backupPassword); return; }
        if (_backupPassword.Password != _confirmation.Password) { Fail("两次备份密码不一致。", _confirmation); return; }
        string password = _backupPassword.Password;
        var dialog = new SaveFileDialog { Title = "保存加密备份（不要放入客户包）", Filter = "签发身份备份|*.sfib", FileName = "签发身份备份_" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".sfib" };
        if (dialog.ShowDialog(this) != true) return;
        await RunAsync(() => _environment.Backup(dialog.FileName, password));
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        string password = _restorePassword.Password;
        if (password.Length < 12) { Fail("请先在「备份密码」里填写这份 .sfib 自己的密码（至少 12 字符）；上面的解锁密码解不开备份。", _restorePassword); return; }
        var dialog = new OpenFileDialog { Title = "选择签发身份备份", Filter = "签发身份备份|*.sfib", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;

        // 核对备份要跑一次 PBKDF2，期间同样算忙：否则用户可以连点，弹出两个确认框后再叠一次恢复。
        _busy = true;
        Inform("正在核对备份指纹，请等待。");
        string backupFingerprint;
        try { backupFingerprint = await Task.Run(() => _environment.PeekBackupFingerprint(dialog.FileName, password)); }
        // 密码不对和身份不同是两回事，分开说，否则用户只会反复试同一个错密码。
        catch (CryptographicException) { Fail("备份密码不对，或备份文件已损坏；本机身份未改动。", _restorePassword); return; }
        catch (Exception ex) { Fail("读取备份失败：" + ex.Message); return; }
        finally { _busy = false; if (_closeWhenDone) Close(); }

        string? local = _environment.LocalFingerprint;
        bool replace = local != null && !string.Equals(local, backupFingerprint, StringComparison.Ordinal);
        if (replace && !ConfirmReplace(local!, backupFingerprint)) { Inform("已取消，本机签发身份未改动。"); return; }
        await RunAsync(() => { using var identity = _environment.Restore(dialog.FileName, password, replace); });
    }

    /// <summary>
    /// 跨身份恢复要人确认：换身份等于让手上所有已签发的授权作废，还要重发客户包。
    /// 两把指纹都摊开显示，避免凭"要不要覆盖"四个字点头。默认按钮是否。
    /// </summary>
    private bool ConfirmReplace(string local, string backup) =>
        MessageBox.Show(this,
            "所选备份与本机不是同一把签发身份。"
            + "\n\n本机指纹：" + local
            + "\n备份指纹：" + backup
            + "\n\n恢复会用备份替换本机身份，之后："
            + "\n· 本机身份已签发的所有授权立即失效，客户必须换新授权；"
            + "\n· 仓库信任锚 Build\\Trust.props 与 Build\\Keys\\ 要换成这份新公钥并重新发布客户包。"
            + "\n\n确定替换？", "确认替换签发身份", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;

    private string? ConfirmedPassword()
    {
        if (_password.Password.Length < 12) { Fail("请设置至少 12 字符的密码，并单独保管。", _password); return null; }
        if (_password.Password != _confirmation.Password) { Fail("两次密码不一致。", _confirmation); return null; }
        return _password.Password;
    }

    private async Task RunAsync(Action action)
    {
        _busy = true;
        Inform("正在处理，请等待；不要中断文件写入。");
        try
        {
            await Task.Run(action);
            Rebuild();
            Inform("操作成功。可以关闭此窗口开始签发；跨电脑请复制整个工具目录。");
        }
        catch (CryptographicException) { Fail("密码错误或身份文件已损坏；文件未替换，请检查密码或恢复备份。"); }
        catch (Exception ex) { Fail("操作失败：" + ex.Message); }
        finally { _password.Clear(); _backupPassword.Clear(); _restorePassword.Clear(); _confirmation.Clear(); _busy = false; if (_closeWhenDone) Close(); }
    }

    /// <summary>状态行同时承担提示与报错：正常用默认颜色，报错标红并把光标落到该填的框，免得看起来像按钮没反应。</summary>
    private void Inform(string text)
    {
        _status.Text = text;
        _status.Foreground = SystemColors.ControlTextBrush;
    }

    private void Fail(string text, PasswordBox? target = null)
    {
        _status.Text = text;
        _status.Foreground = ErrorBrush;
        target?.Focus();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_busy) { _closeWhenDone = true; e.Cancel = true; }
        base.OnClosing(e);
    }
}

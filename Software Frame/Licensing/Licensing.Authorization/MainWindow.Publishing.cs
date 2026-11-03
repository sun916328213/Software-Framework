using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace Licensing.Authorization;

/// <summary>客户工具包页：用当前签发身份发布并审计可交付给客户的整目录。</summary>
public partial class MainWindow
{
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
}

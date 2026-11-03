using System.Windows.Media;
using Licensing.Native.Client;

namespace Licensing.Wpf;

/// <summary>
/// 授权状态到颜色的唯一映射。画刷一律冻结：静态未冻结的画刷有 Dispatcher 亲和性，
/// 换一条 STA 线程赋值就抛「与父 Freezable 不同的线程」。
/// </summary>
public static class LicenseStatusBrushes
{
    public static readonly Brush Valid = Frozen(0x10, 0x7C, 0x10);
    public static readonly Brush Denied = Frozen(0xC4, 0x2B, 0x1C);
    public static readonly Brush NotActivated = Frozen(0x60, 0x60, 0x60);

    public static Brush Of(LicenseBannerState state) => state switch
    {
        LicenseBannerState.Valid => Valid,
        LicenseBannerState.NotActivated => NotActivated,
        _ => Denied
    };

    private static SolidColorBrush Frozen(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
}

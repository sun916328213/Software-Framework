using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using System;
using System.Windows;
using System.Windows.Media;

namespace WPFControl.Helper
{
    /// <summary>
    /// 初始化控制类，用于配置MaterialDesignThemes颜色主题
    /// </summary>
    [Obsolete("与 BundledTheme 混用会被忽略，请改用 WPFCustomControls.Services.AppThemeService（三属性自由切换，深浅+主色+辅色）。")]
    public static class ThemeColorHelper
    {
 
        /// <summary>
        /// 应用自定义颜色主题
        /// </summary>
        /// <param name="primaryColor">主色调</param>
        /// <param name="secondaryColor">辅助色调</param>
        public static void SetThemeColor(Color primaryColor, Color secondaryColor)
        {
            try
            {
                var paletteHelper = new PaletteHelper();
                var theme = paletteHelper.GetTheme();
                
                // 设置主色调
                theme.SetPrimaryColor(primaryColor);
                theme.SetSecondaryColor(secondaryColor);
                
                paletteHelper.SetTheme(theme);
            }
            catch (Exception ex)
            {
                // 如果出现异常，输出到控制台（在实际应用中可能需要更好的错误处理）
                System.Diagnostics.Debug.WriteLine($"Error applying custom theme: {ex.Message}");
            }
        }

        public static void SetThemeColor(string primaryColor, string secondaryColor)
        {
            try
            {
                var paletteHelper = new PaletteHelper();
                var theme = paletteHelper.GetTheme();

                // 设置主色调
                theme.SetPrimaryColor((Color)ColorConverter.ConvertFromString(primaryColor));
                theme.SetSecondaryColor((Color)ColorConverter.ConvertFromString(secondaryColor));

                paletteHelper.SetTheme(theme);
            }
            catch (Exception ex)
            {
                // 如果出现异常，输出到控制台（在实际应用中可能需要更好的错误处理）
                System.Diagnostics.Debug.WriteLine($"Error applying custom theme: {ex.Message}");
            }
        }
 
    }
}
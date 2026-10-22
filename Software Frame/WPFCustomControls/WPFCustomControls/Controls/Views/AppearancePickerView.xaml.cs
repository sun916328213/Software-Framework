using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Extension.UI.Theme;

namespace WPFCustomControls.Controls;

/// <summary>紧凑外观入口，延迟复用 ThemePickerView；宿主传入自己的 ThemeService，不覆盖 DataContext。</summary>
public partial class AppearancePickerView : UserControl
{
    private ThemePickerView? _picker;
    public static readonly DependencyProperty ThemeProperty = DependencyProperty.Register(
        nameof(Theme), typeof(ThemeService), typeof(AppearancePickerView), new PropertyMetadata(null, ThemeChanged));
    public ThemeService? Theme { get => (ThemeService?)GetValue(ThemeProperty); set => SetValue(ThemeProperty, value); }
    public AppearancePickerView()
    {
        InitializeComponent();
        Unloaded += (_, _) => { AppearancePopup.IsOpen = false; _picker?.ViewModel.SuspendThemeNotifications(); };
    }
    private void ToggleAppearance(object sender, RoutedEventArgs e) => AppearancePopup.IsOpen = !AppearancePopup.IsOpen;
    private void PopupOpened(object? sender, EventArgs e)
    {
        if (_picker == null)
        {
            _picker = new ThemePickerView();
            _picker.SetBinding(ThemePickerView.ThemeProperty, new Binding(nameof(Theme)) { Source = this });
            _picker.SetResourceReference(ThemePickerView.FieldBackgroundProperty, "MaterialDesign.Brush.Card.Background");
            _picker.SetResourceReference(ThemePickerView.FieldForegroundProperty, "MaterialDesign.Brush.Foreground");
            PickerHost.Content = _picker;
        }
        _picker.ViewModel.ResumeThemeNotifications();
    }
    private void PopupClosed(object? sender, EventArgs e) => _picker?.ViewModel.SuspendThemeNotifications();
    private static void ThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (AppearancePickerView)d;
        // 显式同步避免属性绑定的通知顺序令收起状态重新订阅主题。
        if (view._picker == null) return;
        view._picker.SetCurrentValue(ThemePickerView.ThemeProperty, e.NewValue);
        if (!view.AppearancePopup.IsOpen) view._picker.ViewModel.SuspendThemeNotifications();
    }
}

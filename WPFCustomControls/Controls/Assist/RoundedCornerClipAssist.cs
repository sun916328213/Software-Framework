using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WPFCustomControls.Controls
{
    /// <summary>
    /// 圆角裁剪附加属性助手。
    /// WPF 的 Border 不像 WinForms/Linux 窗口那样会裁剪子元素，
    /// 超出圆角区域的内容仍然会显示。本助手在尺寸变化时动态更新
    /// Border 的 Clip，将内容裁剪到圆角矩形范围内，隐藏溢出部分，
    /// 并与 BorderThickness/Padding 计算后的内容区域精确对齐。
    /// </summary>
    public static class RoundedCornerClipAssist
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(RoundedCornerClipAssist),
                new PropertyMetadata(false, OnEnabledChanged));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.RegisterAttached(
                "CornerRadius",
                typeof(CornerRadius),
                typeof(RoundedCornerClipAssist),
                new FrameworkPropertyMetadata(
                    new CornerRadius(0),
                    FrameworkPropertyMetadataOptions.AffectsArrange,
                    OnCornerRadiusChanged));

        public static bool GetIsEnabled(DependencyObject element) =>
            (bool)element.GetValue(IsEnabledProperty);

        public static void SetIsEnabled(DependencyObject element, bool value) =>
            element.SetValue(IsEnabledProperty, value);

        public static CornerRadius GetCornerRadius(DependencyObject element) =>
            (CornerRadius)element.GetValue(CornerRadiusProperty);

        public static void SetCornerRadius(DependencyObject element, CornerRadius value) =>
            element.SetValue(CornerRadiusProperty, value);

        private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FrameworkElement element)
                return;

            if ((bool)e.NewValue)
            {
                element.SizeChanged += OnElementSizeChanged;
                element.LayoutUpdated += OnElementLayoutUpdated;
                ApplyClip(element);
            }
            else
            {
                element.SizeChanged -= OnElementSizeChanged;
                element.LayoutUpdated -= OnElementLayoutUpdated;
                element.Clip = null;
            }
        }

        private static void OnCornerRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element && GetIsEnabled(element))
                ApplyClip(element);
        }

        private static void OnElementSizeChanged(object sender, SizeChangedEventArgs e) =>
            ApplyClip((FrameworkElement)sender);

        private static void OnElementLayoutUpdated(object sender, EventArgs e)
        {
            // 布局完成后再裁剪一次，保证与最终尺寸一致
            if (sender is FrameworkElement element && GetIsEnabled(element))
                ApplyClip(element);
        }

        private static void ApplyClip(FrameworkElement element)
        {
            if (element is not Border border || !GetIsEnabled(border))
                return;

            var width = Math.Max(0, border.ActualWidth);
            var height = Math.Max(0, border.ActualHeight);

            if (width <= 0 || height <= 0)
                return;

            // 内容实际绘制区域 = 去除 BorderThickness 与 Padding 后的内部矩形，
            // 子元素正是在该区域布局。按此区域裁剪才能与圆角边框精确对齐，
            // 避免出现需要手动 -1 像素位移的偏差。
            var t = border.BorderThickness;
            var p = border.Padding;
            var left = t.Left + p.Left;
            var top = t.Top + p.Top;
            var right = t.Right + p.Right;
            var bottom = t.Bottom + p.Bottom;

            var rect = new Rect(
                left + 0.5,          // 0.5 偏移抵消 1px 抗锯齿边界
                top + 0.5,
                Math.Max(0, width - left - right - 1),
                Math.Max(0, height - top - bottom - 1));

            // 取圆角半径：优先使用附加属性指定的值；未显式指定时
            // 跟随 Border 自身的 CornerRadius（含局部覆盖），保证始终与边框圆角一致。
            var c = GetCornerRadius(border);
            double radius;
            if (c.TopLeft == 0 && c.TopRight == 0 && c.BottomLeft == 0 && c.BottomRight == 0)
            {
                var native = border.CornerRadius;
                radius = Math.Max(
                    Math.Max(native.TopLeft, native.TopRight),
                    Math.Max(native.BottomLeft, native.BottomRight));
            }
            else
            {
                radius = Math.Max(
                    Math.Max(c.TopLeft, c.TopRight),
                    Math.Max(c.BottomLeft, c.BottomRight));
            }
            radius = Math.Max(0, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2));

            border.Clip = new RectangleGeometry(rect, radius, radius);
        }
    }
}
// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：RoundedCornerClipAssist。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、System、System.Windows、System.Windows.Controls、System.Windows.Media。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
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
    /// Border 的子元素裁剪到内圆角矩形范围内，隐藏溢出部分。
    /// Border 自身的背景和边框不参与裁剪，避免白色边缘被误裁掉。
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

        private static readonly DependencyProperty ClippedChildProperty =
            DependencyProperty.RegisterAttached(
                "ClippedChild",
                typeof(UIElement),
                typeof(RoundedCornerClipAssist));

        private static readonly DependencyProperty OriginalChildClipProperty =
            DependencyProperty.RegisterAttached(
                "OriginalChildClip",
                typeof(Geometry),
                typeof(RoundedCornerClipAssist));

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetIsEnabled。
        /// 正文中的关键协作调用包括 GetValue；具体条件和先后次序见方法体。
        /// </summary>
        public static bool GetIsEnabled(DependencyObject element) =>
            (bool)element.GetValue(IsEnabledProperty);

        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetIsEnabled。
        /// 正文中的关键协作调用包括 SetValue；具体条件和先后次序见方法体。
        /// </summary>
        public static void SetIsEnabled(DependencyObject element, bool value) =>
            element.SetValue(IsEnabledProperty, value);

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetCornerRadius。
        /// 正文中的关键协作调用包括 GetValue；具体条件和先后次序见方法体。
        /// </summary>
        public static CornerRadius GetCornerRadius(DependencyObject element) =>
            (CornerRadius)element.GetValue(CornerRadiusProperty);

        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetCornerRadius。
        /// 正文中的关键协作调用包括 SetValue；具体条件和先后次序见方法体。
        /// </summary>
        public static void SetCornerRadius(DependencyObject element, CornerRadius value) =>
            element.SetValue(CornerRadiusProperty, value);

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnEnabledChanged。
        /// 正文中的关键协作调用包括 ApplyClip、RestoreChildClip；具体条件和先后次序见方法体。
        /// </summary>
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
                if (element is Border border)
                    RestoreChildClip(border);
            }
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnCornerRadiusChanged。
        /// 正文中的关键协作调用包括 GetIsEnabled、ApplyClip；具体条件和先后次序见方法体。
        /// </summary>
        private static void OnCornerRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element && GetIsEnabled(element))
                ApplyClip(element);
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnElementSizeChanged。
        /// 正文中的关键协作调用包括 ApplyClip；具体条件和先后次序见方法体。
        /// </summary>
        private static void OnElementSizeChanged(object sender, SizeChangedEventArgs e) =>
            ApplyClip((FrameworkElement)sender);

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnElementLayoutUpdated。
        /// 正文中的关键协作调用包括 GetIsEnabled、ApplyClip；具体条件和先后次序见方法体。
        /// </summary>
        private static void OnElementLayoutUpdated(object? sender, EventArgs e)
        {
            // 布局完成后再裁剪一次，保证与最终尺寸一致
            if (sender is FrameworkElement element && GetIsEnabled(element))
                ApplyClip(element);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ApplyClip。
        /// 正文中的关键协作调用包括 GetIsEnabled、RestoreChildClip、TrackChild、Max、GetCornerRadius；具体条件和先后次序见方法体。
        /// </summary>
        private static void ApplyClip(FrameworkElement element)
        {
            if (element is not Border border || !GetIsEnabled(border))
                return;

            if (border.Child is not UIElement child)
            {
                RestoreChildClip(border);
                return;
            }

            TrackChild(border, child);

            var width = Math.Max(0, child.RenderSize.Width);
            var height = Math.Max(0, child.RenderSize.Height);

            if (width <= 0 || height <= 0)
                return;

            // 取圆角半径：优先使用附加属性指定的值；未显式指定时
            // 跟随 Border 自身的 CornerRadius（含局部覆盖），保证始终与边框圆角一致。
            var c = GetCornerRadius(border);
            if (c.TopLeft == 0 && c.TopRight == 0 && c.BottomLeft == 0 && c.BottomRight == 0)
                c = border.CornerRadius;

            var childOrigin = child.TranslatePoint(new Point(), border);
            var thickness = border.BorderThickness;
            var clipRect = new Rect(
                thickness.Left - childOrigin.X,
                thickness.Top - childOrigin.Y,
                Math.Max(0, border.ActualWidth - thickness.Left - thickness.Right),
                Math.Max(0, border.ActualHeight - thickness.Top - thickness.Bottom));
            var innerRadius = new CornerRadius(
                Math.Max(0, c.TopLeft - Math.Max(thickness.Left, thickness.Top)),
                Math.Max(0, c.TopRight - Math.Max(thickness.Right, thickness.Top)),
                Math.Max(0, c.BottomRight - Math.Max(thickness.Right, thickness.Bottom)),
                Math.Max(0, c.BottomLeft - Math.Max(thickness.Left, thickness.Bottom)));

            child.Clip = CreateRoundedRectangleGeometry(clipRect, innerRadius);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 TrackChild。
        /// 正文中的关键协作调用包括 ReferenceEquals、GetValue、RestoreChildClip、SetValue；具体条件和先后次序见方法体。
        /// </summary>
        private static void TrackChild(Border border, UIElement child)
        {
            if (ReferenceEquals(border.GetValue(ClippedChildProperty), child))
                return;

            RestoreChildClip(border);
            border.SetValue(ClippedChildProperty, child);
            border.SetValue(OriginalChildClipProperty, child.Clip);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 RestoreChildClip。
        /// 正文中的关键协作调用包括 GetValue、ClearValue；具体条件和先后次序见方法体。
        /// </summary>
        private static void RestoreChildClip(Border border)
        {
            if (border.GetValue(ClippedChildProperty) is UIElement child)
                child.Clip = (Geometry?)border.GetValue(OriginalChildClipProperty);

            border.ClearValue(ClippedChildProperty);
            border.ClearValue(OriginalChildClipProperty);
        }

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 CreateRoundedRectangleGeometry。
        /// 正文中的关键协作调用包括 Max、LimitScale、Open、BeginFigure、LineTo；具体条件和先后次序见方法体。
        /// </summary>
        private static Geometry CreateRoundedRectangleGeometry(Rect bounds, CornerRadius cornerRadius)
        {
            var width = bounds.Width;
            var height = bounds.Height;
            var topLeft = Math.Max(0, cornerRadius.TopLeft);
            var topRight = Math.Max(0, cornerRadius.TopRight);
            var bottomRight = Math.Max(0, cornerRadius.BottomRight);
            var bottomLeft = Math.Max(0, cornerRadius.BottomLeft);

            // WPF Border 会按可用空间同比缩小相交的圆角；裁剪几何采用同样规则。
            var scale = 1d;
            scale = LimitScale(scale, width, topLeft + topRight);
            scale = LimitScale(scale, width, bottomLeft + bottomRight);
            scale = LimitScale(scale, height, topLeft + bottomLeft);
            scale = LimitScale(scale, height, topRight + bottomRight);

            topLeft *= scale;
            topRight *= scale;
            bottomRight *= scale;
            bottomLeft *= scale;

            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(
                    new Point(bounds.Left + topLeft, bounds.Top),
                    isFilled: true,
                    isClosed: true);
                context.LineTo(new Point(bounds.Right - topRight, bounds.Top), true, false);
                AddArc(context, new Point(bounds.Right, bounds.Top + topRight), topRight);
                context.LineTo(new Point(bounds.Right, bounds.Bottom - bottomRight), true, false);
                AddArc(context, new Point(bounds.Right - bottomRight, bounds.Bottom), bottomRight);
                context.LineTo(new Point(bounds.Left + bottomLeft, bounds.Bottom), true, false);
                AddArc(context, new Point(bounds.Left, bounds.Bottom - bottomLeft), bottomLeft);
                context.LineTo(new Point(bounds.Left, bounds.Top + topLeft), true, false);
                AddArc(context, new Point(bounds.Left + topLeft, bounds.Top), topLeft);
            }

            geometry.Freeze();
            return geometry;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 LimitScale。
        /// 正文中的关键协作调用包括 Min；具体条件和先后次序见方法体。
        /// </summary>
        private static double LimitScale(double current, double available, double requested) =>
            requested > available && requested > 0
                ? Math.Min(current, available / requested)
                : current;

        /// <summary>
        /// 执行添加操作；对应入口为 AddArc。
        /// 正文中的关键协作调用包括 LineTo、ArcTo；具体条件和先后次序见方法体。
        /// </summary>
        private static void AddArc(StreamGeometryContext context, Point endPoint, double radius)
        {
            if (radius <= 0)
            {
                context.LineTo(endPoint, true, false);
                return;
            }

            context.ArcTo(
                endPoint,
                new Size(radius, radius),
                rotationAngle: 0,
                isLargeArc: false,
                SweepDirection.Clockwise,
                isStroked: true,
                isSmoothJoin: false);
        }
    }
}

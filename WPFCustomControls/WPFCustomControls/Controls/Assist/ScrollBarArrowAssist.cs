using System.Windows;
using System.Windows.Media;

namespace WPFCustomControls.Controls
{
    /// <summary>
    /// 滚动条两端三角按钮的附加属性助手（配合 <c>Resources/Resources.xaml</c> 里的
    /// <c>ScrollBar_Style</c> 使用）。
    ///
    /// <para>
    /// 背景：MD 自带模板里的「箭头」是**带竖杆的箭头**（↑ 形），本仓要的是**纯实心三角**（▲▼），
    /// 所以 <c>ScrollBar_Style</c> 自己出了一套 ControlTemplate。三角的尺寸 / 颜色这些东西
    /// 原本是写死在模板里的，抽到这里之后就能按需覆盖。
    /// </para>
    ///
    /// <para>
    /// <b>怎么设</b>：三个位置都可以，优先级从高到低 ——
    /// </para>
    /// <list type="number">
    /// <item>直接设在某个 ScrollBar 实例上（局部值最高）。</item>
    /// <item>设在隐式 ScrollBar 样式里（本仓最常用）：
    /// <code>
    /// &lt;Style BasedOn="{StaticResource ScrollBar_Style}" TargetType="ScrollBar"&gt;
    ///     &lt;Setter Property="assist:ScrollBarArrowAssist.Size" Value="14" /&gt;
    /// &lt;/Style&gt;
    /// </code>
    /// </item>
    /// <item>设在任意祖先元素上（如 ScrollViewer / 页面根 Grid）—— 这些属性都开了
    /// <see cref="FrameworkPropertyMetadataOptions.Inherits"/>，会往下继承到 ScrollBar 和三角本身。</item>
    /// </list>
    ///
    /// <para>
    /// <b>想彻底不显示三角</b>：不要用这里的属性，用 MD 自己的
    /// <c>materialDesign:ScrollBarAssist.ButtonsVisibility="Collapsed"</c>（模板已按它绑定 Visibility）。
    /// </para>
    ///
    /// <para>
    /// <b>关于两个 Brush 属性的 null 语义</b>：默认值都是 <c>null</c>，表示「跟随主题」——
    /// 三角取 <c>MaterialDesign.Brush.Foreground</c>，悬停高亮取
    /// <c>MaterialDesign.Brush.ScrollBar.ActiveBackground</c>。
    /// 一旦你显式给了画刷，就完全按你的来（不会和主题画刷叠加）。
    /// </para>
    /// </summary>
    public static class ScrollBarArrowAssist
    {
        /// <summary>
        /// 三角的绘制尺寸（外接正方形边长），默认 10。
        ///
        /// <para>
        /// 三角几何是 16 宽 / 12 高的等腰三角形，按 <c>Stretch=Uniform</c> 等比缩进 Size×Size 的方框，
        /// 所以**视觉宽度 = Size，视觉高度 ≈ 0.75 × Size**。
        /// 竖直滚动条受行高上限（<c>SystemParameters.VerticalScrollBarButtonHeightKey</c>，默认 17）约束，
        /// 设太大不会溢出，只是留白变少。
        /// </para>
        /// </summary>
        public static readonly DependencyProperty SizeProperty =
            DependencyProperty.RegisterAttached(
                "Size",
                typeof(double),
                typeof(ScrollBarArrowAssist),
                new FrameworkPropertyMetadata(
                    10d,
                    FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure));

        /// <summary>
        /// 三角自身的外边距，默认 <c>0</c>。用来微调三角与轨道 / 滚动条边缘的间距，
        /// 典型用法是 <c>Margin="0,3,0,1"</c> 把上三角往下压一点。
        /// </summary>
        public static readonly DependencyProperty MarginProperty =
            DependencyProperty.RegisterAttached(
                "Margin",
                typeof(Thickness),
                typeof(ScrollBarArrowAssist),
                new FrameworkPropertyMetadata(
                    default(Thickness),
                    FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure));

        /// <summary>
        /// 三角的填充画刷。默认 <c>null</c> = 跟随主题前景色（<c>MaterialDesign.Brush.Foreground</c>）。
        /// </summary>
        public static readonly DependencyProperty BrushProperty =
            DependencyProperty.RegisterAttached(
                "Brush",
                typeof(Brush),
                typeof(ScrollBarArrowAssist),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// 鼠标悬停 / 按下时三角按钮底部的圆角高亮块颜色。
        /// 默认 <c>null</c> = 跟随主题（<c>MaterialDesign.Brush.ScrollBar.ActiveBackground</c>）。
        /// 想要「悬停完全不变色」就设成 <c>Transparent</c>。
        /// </summary>
        public static readonly DependencyProperty HighlightBrushProperty =
            DependencyProperty.RegisterAttached(
                "HighlightBrush",
                typeof(Brush),
                typeof(ScrollBarArrowAssist),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// 悬停 / 按下时高亮块的圆角，默认 <c>0</c>（直角）。给个 4 左右就是圆角药丸感。
        /// </summary>
        public static readonly DependencyProperty HighlightCornerRadiusProperty =
            DependencyProperty.RegisterAttached(
                "HighlightCornerRadius",
                typeof(CornerRadius),
                typeof(ScrollBarArrowAssist),
                new FrameworkPropertyMetadata(
                    default(CornerRadius),
                    FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

        // ── Size ──────────────────────────────────────────────────
        public static double GetSize(DependencyObject element) =>
            (double)element.GetValue(SizeProperty);

        public static void SetSize(DependencyObject element, double value) =>
            element.SetValue(SizeProperty, value);

        // ── Margin ────────────────────────────────────────────────
        public static Thickness GetMargin(DependencyObject element) =>
            (Thickness)element.GetValue(MarginProperty);

        public static void SetMargin(DependencyObject element, Thickness value) =>
            element.SetValue(MarginProperty, value);

        // ── Brush ─────────────────────────────────────────────────
        public static Brush? GetBrush(DependencyObject element) =>
            (Brush?)element.GetValue(BrushProperty);

        public static void SetBrush(DependencyObject element, Brush? value) =>
            element.SetValue(BrushProperty, value);

        // ── HighlightBrush ────────────────────────────────────────
        public static Brush? GetHighlightBrush(DependencyObject element) =>
            (Brush?)element.GetValue(HighlightBrushProperty);

        public static void SetHighlightBrush(DependencyObject element, Brush? value) =>
            element.SetValue(HighlightBrushProperty, value);

        // ── HighlightCornerRadius ─────────────────────────────────
        public static CornerRadius GetHighlightCornerRadius(DependencyObject element) =>
            (CornerRadius)element.GetValue(HighlightCornerRadiusProperty);

        public static void SetHighlightCornerRadius(DependencyObject element, CornerRadius value) =>
            element.SetValue(HighlightCornerRadiusProperty, value);
    }
}

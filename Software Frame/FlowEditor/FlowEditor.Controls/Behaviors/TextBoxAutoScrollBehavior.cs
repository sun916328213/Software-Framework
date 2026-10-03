// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：TextBoxAutoScrollBehavior。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、System.Windows、System.Windows.Controls。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using System.Windows;
using System.Windows.Controls;

namespace FlowEditor.Controls.Behaviors;

/// <summary>为只追加内容的文本框提供可复用的自动滚动到底部行为。</summary>
public static class TextBoxAutoScrollBehavior
{
    /// <summary>标识 <see cref="GetIsEnabled"/> 和 <see cref="SetIsEnabled"/> 使用的附加属性。</summary>
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(TextBoxAutoScrollBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    /// <summary>获取文本框是否启用了自动滚动。</summary>
    /// <param name="element">目标文本框。</param>
    /// <returns>启用时为 <see langword="true"/>。</returns>
    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    /// <summary>设置文本框是否启用自动滚动。</summary>
    /// <param name="element">目标文本框。</param>
    /// <param name="value">是否启用。</param>
    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    /// <summary>在附加属性变化时注册或注销文本变化处理。</summary>
    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not TextBox textBox) return;

        textBox.TextChanged -= OnTextChanged;
        if (e.NewValue is true)
            textBox.TextChanged += OnTextChanged;
    }

    /// <summary>在文本更新后滚动到最新内容。</summary>
    private static void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox { Text.Length: > 0 } textBox)
            textBox.ScrollToEnd();
    }
}

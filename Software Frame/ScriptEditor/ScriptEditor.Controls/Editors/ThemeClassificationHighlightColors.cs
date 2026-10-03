// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：ThemeClassificationHighlightColors、DarkColors。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Windows.Media、ICSharpCode.AvalonEdit.Highlighting、RoslynPad.Editor。导入不代表所有类型都会在运行时加载。
using System;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;
using RoslynPad.Editor;

namespace ScriptEditor.Controls.Editors;

/// <summary>Chooses a readable syntax palette without changing the Roslyn document or classification state.</summary>
/// <remarks>
/// ThemeClassificationHighlightColors 的类型定义。ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
/// 继承或实现的契约：IClassificationHighlightColors。具体约束按这些契约及各成员实现解释。
/// </remarks>
internal sealed class ThemeClassificationHighlightColors(Func<bool> isDark) : IClassificationHighlightColors
{
    private readonly ClassificationHighlightColors _light = new();
    private readonly ClassificationHighlightColors _dark = new DarkColors();
    public HighlightingColor DefaultBrush => (isDark() ? _dark : _light).DefaultBrush;
    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetBrush。
    /// 正文中的关键协作调用包括 GetBrush、isDark；具体条件和先后次序见方法体。
    /// </summary>
    public HighlightingColor GetBrush(string classificationTypeName) =>
        (isDark() ? _dark : _light).GetBrush(classificationTypeName);

    /// <summary>
    /// DarkColors 的类型定义。ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
    /// 继承或实现的契约：ClassificationHighlightColors。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class DarkColors : ClassificationHighlightColors
    {
        /// <summary>
        /// 初始化 DarkColors 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Foreground、FromRgb；具体条件和先后次序见方法体。
        /// </summary>
        public DarkColors()
        {
            DefaultBrush = Foreground(0xD4, 0xD4, 0xD4);
            TypeBrush = Foreground(0x4E, 0xC9, 0xB0);
            MethodBrush = Foreground(0xDC, 0xDC, 0xAA);
            ParameterBrush = Foreground(0x9C, 0xDC, 0xFE);
            CommentBrush = Foreground(0x8F, 0xBC, 0x8F);
            XmlCommentBrush = Foreground(0x8F, 0xBC, 0x8F);
            KeywordBrush = Foreground(0x56, 0x9C, 0xD6);
            PreprocessorKeywordBrush = Foreground(0xC5, 0x86, 0xC0);
            StringBrush = Foreground(0xCE, 0x91, 0x78);
            BraceMatchingBrush = Foreground(0xD4, 0xD4, 0xD4);
            BraceMatchingBrush.Background = new SimpleHighlightingBrush(Color.FromRgb(0x53, 0x63, 0x53));
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Foreground。
        /// 正文中的关键协作调用包括 FromRgb；具体条件和先后次序见方法体。
        /// </summary>
        private static HighlightingColor Foreground(byte r, byte g, byte b) =>
            new() { Foreground = new SimpleHighlightingBrush(Color.FromRgb(r, g, b)) };
    }
}

// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：RoiEditorView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Controls、System.Windows、System.Windows.Controls、Vision.Controls.ViewModels。导入不代表所有类型都会在运行时加载。
using Vision.Controls;
using System.Windows;
using System.Windows.Controls;
using Vision.Controls.ViewModels;

namespace Vision.Controls.View
{
    /// <summary>
    /// RoiEditorView.xaml 的交互逻辑（P8 新控件；添加按钮用默认几何调 VM，余下绑命令）。
    /// </summary>
    public partial class RoiEditorView : UserControl
    {
        /// <summary>绑定的编辑器 VM。</summary>
        public static readonly DependencyProperty EditorProperty =
            DependencyProperty.Register(nameof(Editor), typeof(RoiEditorViewModel), typeof(RoiEditorView), new PropertyMetadata(null));

        /// <summary>绑定的编辑器 VM。</summary>
        public RoiEditorViewModel? Editor
        {
            get => (RoiEditorViewModel?)GetValue(EditorProperty);
            set => SetValue(EditorProperty, value);
        }

        /// <summary>构造。</summary>
        public RoiEditorView()
        {
            InitializeComponent();
        }

    }
}

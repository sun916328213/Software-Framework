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

        private void OnAddRectangleClicked(object sender, RoutedEventArgs e)
        {
            Editor?.AddRectangle(32, 32, 100, 60);
        }

        private void OnAddCircleClicked(object sender, RoutedEventArgs e)
        {
            Editor?.AddCircle(32, 32, 60);
        }
    }
}

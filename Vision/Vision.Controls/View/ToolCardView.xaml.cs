using System.Windows;
using System.Windows.Controls;
using Vision.Controls.ViewModels;

namespace Vision.Controls.View
{
    /// <summary>
    /// ToolCardView.xaml 的交互逻辑（P8 新控件；只做 DP 透传，逻辑全在 VM）。
    /// </summary>
    public partial class ToolCardView : UserControl
    {
        /// <summary>绑定的卡片 VM。</summary>
        public static readonly DependencyProperty CardProperty =
            DependencyProperty.Register(nameof(Card), typeof(ToolCardViewModel), typeof(ToolCardView), new PropertyMetadata(null));

        /// <summary>绑定的卡片 VM。</summary>
        public ToolCardViewModel? Card
        {
            get => (ToolCardViewModel?)GetValue(CardProperty);
            set => SetValue(CardProperty, value);
        }

        /// <summary>构造。</summary>
        public ToolCardView()
        {
            InitializeComponent();
        }
    }
}

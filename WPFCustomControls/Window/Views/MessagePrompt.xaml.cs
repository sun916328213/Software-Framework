using System.Windows;
using System.Windows.Media;

namespace WPFCustomControls.Window.Views
{
    public partial class MessagePrompt : System.Windows.Window
    {
        public static new readonly DependencyProperty TitleProperty;
        public static readonly DependencyProperty MessageProperty;
        public static readonly DependencyProperty Visibility_CancelProperty;
        public static readonly DependencyProperty MessageForegroundProperty;
        public static readonly DependencyProperty TitleForegroundProperty;

        public new string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }
        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }
        public Visibility Visibility_Cancel
        {
            get => (Visibility)GetValue(Visibility_CancelProperty);
            set => SetValue(Visibility_CancelProperty, value);
        }
        public Brush MessageForeground
        {
            get => (Brush)GetValue(MessageForegroundProperty);
            set => SetValue(MessageForegroundProperty, value);
        }
        public Brush TitleForeground
        {
            get => (Brush)GetValue(TitleForegroundProperty);
            set => SetValue(TitleForegroundProperty, value);
        }

        public MessagePrompt() => InitializeComponent();

        public MessagePrompt(string pTitle, string pMessage, Visibility pHideCancel = Visibility.Visible)
        {
            InitializeComponent();
            Title = pTitle;
            Message = pMessage;
            Visibility_Cancel = pHideCancel;
        }

        static MessagePrompt()
        {
            TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(MessagePrompt), new PropertyMetadata("提示"));
            MessageProperty = DependencyProperty.Register("Message", typeof(string), typeof(MessagePrompt), new PropertyMetadata(""));
            Visibility_CancelProperty = DependencyProperty.Register("Visibility_Cancel", typeof(Visibility), typeof(MessagePrompt), new PropertyMetadata());
            MessageForegroundProperty = DependencyProperty.Register("MessageForeground", typeof(Brush), typeof(MessagePrompt), new PropertyMetadata(Brushes.Black));
            TitleForegroundProperty = DependencyProperty.Register("TitleForeground", typeof(Brush), typeof(MessagePrompt), new PropertyMetadata(Brushes.Black));
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

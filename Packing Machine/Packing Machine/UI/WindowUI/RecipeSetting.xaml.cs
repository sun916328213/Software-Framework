using System.Windows;

namespace Packing_Machine.UI.WindowUI
{
    /// <summary>
    /// RecipeSetting.xaml 的交互逻辑
    /// </summary>
    public partial class RecipeSetting : Window
    {
      
        public RecipeSetting()
        {
            InitializeComponent();
         
        }

    

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}

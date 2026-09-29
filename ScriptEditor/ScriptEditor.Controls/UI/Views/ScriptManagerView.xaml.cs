using System.Windows;
using System.Windows.Controls;
using ScriptEditor.ViewModels;

namespace ScriptEditor.Views
{
    public partial class ScriptManagerView : UserControl
    {
        public ScriptManagerView()
        {
            InitializeComponent();

            // VM 通过 Prism ViewModelLocator 在构造时注入 DataContext；
            // 事件订阅改由 Attach/Detach 管理（见 ScriptManagerViewModel），
            // 避免单例 Service 的 Model 持有废弃 VM 强引用导致内存泄漏。
            Loaded += OnViewLoaded;
            Unloaded += OnViewUnloaded;
        }

        private void OnViewLoaded(object sender, RoutedEventArgs e)
        {
            (DataContext as ScriptManagerViewModel)?.Attach();
        }

        private void OnViewUnloaded(object sender, RoutedEventArgs e)
        {
            // 延迟到 ApplicationIdle 确认：TabControl 切页也会触发 Unloaded，
            // 届时若视图已重新载入（IsLoaded）则跳过，仅最终卸载时才 Detach。
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!IsLoaded)
                    (DataContext as ScriptManagerViewModel)?.Detach();
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
    }
}

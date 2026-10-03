// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：ScriptManagerView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：ScriptEditor.Controls、System.Windows、System.Windows.Controls、ScriptEditor.Controls.ViewModels。导入不代表所有类型都会在运行时加载。
using ScriptEditor.Controls;
using System.Windows;
using System.Windows.Controls;
using ScriptEditor.Controls.ViewModels;

namespace ScriptEditor.Controls.Views
{
    /// <summary>
    /// ScriptManagerView 的类型定义。ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
    /// 继承或实现的契约：UserControl。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public partial class ScriptManagerView : UserControl
    {
        private ScriptManagerViewModel? _attachedViewModel;
        /// <summary>
        /// 初始化 ScriptManagerView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent、AttachCurrentViewModel；具体条件和先后次序见方法体。
        /// </summary>
        public ScriptManagerView()
        {
            InitializeComponent();

            // VM 通过 Prism ViewModelLocator 在构造时注入 DataContext；
            // 事件订阅改由 Attach/Detach 管理（见 ScriptManagerViewModel），
            // 避免单例 Service 的 Model 持有废弃 VM 强引用导致内存泄漏。
            Loaded += OnViewLoaded;
            Unloaded += OnViewUnloaded;
            DataContextChanged += (_, _) => { if (IsLoaded) AttachCurrentViewModel(); };
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnViewLoaded。
        /// 正文中的关键协作调用包括 AttachCurrentViewModel；具体条件和先后次序见方法体。
        /// </summary>
        private void OnViewLoaded(object sender, RoutedEventArgs e)
        {
            AttachCurrentViewModel();
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnViewUnloaded。
        /// </summary>
        private void OnViewUnloaded(object sender, RoutedEventArgs e)
        {
            // 只解除页面通知；服务和脚本会话继续存在。再次 Loaded 时 Attach 会补发状态。
            _attachedViewModel?.Detach();
            _attachedViewModel = null;
        }
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 AttachCurrentViewModel。
        /// 正文中的关键协作调用包括 ReferenceEquals；具体条件和先后次序见方法体。
        /// </summary>
        private void AttachCurrentViewModel()
        {
            var current = DataContext as ScriptManagerViewModel;
            if (ReferenceEquals(current, _attachedViewModel)) return;
            _attachedViewModel?.Detach();
            _attachedViewModel = current;
            current?.Attach();
        }
    }
}

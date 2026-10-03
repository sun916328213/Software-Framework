// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：RecipeSetting。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Windows。导入不代表所有类型都会在运行时加载。
using System.Windows;

namespace MachineApp.UI.WindowUI
{
    /// <summary>
    /// RecipeSetting.xaml 的交互逻辑
    /// </summary>
    public partial class RecipeSetting : Window
    {
      
        /// <summary>
        /// 初始化 RecipeSetting 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public RecipeSetting()
        {
            InitializeComponent();
         
        }

    

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Button_Click。
        /// </summary>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Button_Click_1。
        /// </summary>
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}

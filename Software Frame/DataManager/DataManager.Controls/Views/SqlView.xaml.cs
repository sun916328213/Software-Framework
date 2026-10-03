// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：SqlView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Controls、DataManager.Core、System.Windows.Controls。导入不代表所有类型都会在运行时加载。
using DataManager.Controls;
using DataManager.Core;
using System.Windows.Controls;

namespace DataManager.Controls.Views
{
    /// <summary>
    /// SQL 测试页视图：纯 XAML（列定义、增删回调、选中同步全在 SqlView.xaml 声明），无 code-behind 逻辑。
    /// </summary>
    public partial class SqlView : UserControl
    {
        /// <summary>
        /// 初始化 SqlView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public SqlView()
        {
            InitializeComponent();
        }
    }
}

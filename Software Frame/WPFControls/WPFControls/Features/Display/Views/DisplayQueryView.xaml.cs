// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：DisplayQueryView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、WPFControls.Controls.Views、CommunityToolkit.Mvvm.Input、Extension.Imaging.Halcon、Extension.Imaging.OpenCv、System.Collections.Generic。导入不代表所有类型都会在运行时加载。
using WPFControls;
using WPFControls.Controls.Views;
using CommunityToolkit.Mvvm.Input;
using Extension.Imaging.Halcon;
using Extension.Imaging.OpenCv;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace WPFControls.ProgramControl.Views.Display
{
    /// <summary>
    /// DisplayQueryView.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayQueryView : UserControl
    {
        public static readonly DependencyProperty ItemsSourceProperty;
        public static readonly DependencyProperty SelectIndexProperty;
        public static readonly DependencyProperty SelectedItemProperty; // 添加SelectedItem属性
 

        public RelayCommand FirstCommand { get; }
        public RelayCommand EndCommand { get; }
        public RelayCommand NextCommand { get; }
        public RelayCommand LastCommand { get; }

        /// <summary>
        /// 初始化 DisplayQueryView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent、Any、Count；具体条件和先后次序见方法体。
        /// </summary>
        public DisplayQueryView()
        {
            InitializeComponent();
            FirstCommand = new RelayCommand(() => 
            {
                SelectIndex = 0;
            });
            EndCommand = new RelayCommand(() => { 
                if (ItemsSource != null && ItemsSource.Any())
                    SelectIndex = ItemsSource.Count() - 1; 
            });
            NextCommand = new RelayCommand(() => { 
                if (ItemsSource != null && SelectIndex < ItemsSource.Count() - 1)
                    SelectIndex++; 
            });
            LastCommand = new RelayCommand(() => { 
                if (ItemsSource != null && SelectIndex > 0)
                    SelectIndex--; 
            });
        }

        /// <summary>
        /// 初始化 DisplayQueryView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Register；具体条件和先后次序见方法体。
        /// </summary>
        static DisplayQueryView()
        {
            ItemsSourceProperty = DependencyProperty.Register("ItemsSource", typeof(IEnumerable<object>), typeof(DisplayQueryView), new FrameworkPropertyMetadata(null,OnItemsSourceChanged));
            SelectIndexProperty = DependencyProperty.Register("SelectIndex", typeof(int), typeof(DisplayQueryView), new FrameworkPropertyMetadata(-1, OnSelectIndexChanged)); // 添加默认值和回调
            SelectedItemProperty = DependencyProperty.Register("SelectedItem", typeof(object), typeof(DisplayQueryView), new FrameworkPropertyMetadata(null)); // 注册SelectedItem属性
         }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnItemsSourceChanged。
        /// 正文中的关键协作调用包括 Count；具体条件和先后次序见方法体。
        /// </summary>
        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as DisplayQueryView;
            if (control.ItemsSource.Count() > 0)
            {
                control.SelectIndex = -1;
                control.SelectIndex = 0;
            }
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnSelectIndexChanged。
        /// 正文中的关键协作调用包括 ToList、ToString、OpenImage；具体条件和先后次序见方法体。
        /// </summary>
        private static void OnSelectIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as DisplayQueryView;
            if (control != null && control.ItemsSource != null)
            {
                var items = control.ItemsSource.ToList();
                var index = control.SelectIndex;
                
                if (index >= 0 && index < items.Count)
                {
                    // 获取选中的项
                    string selectedItem = items[index].ToString();

                    control.lbl_SelectPath.Content = selectedItem;
                    control.lbl_Process.Content = (index+1).ToString()+"/"+ items.Count.ToString();
                    // 调用Halcon ImageHelper打开图片
                    control. SelectedItem =    HImageEx.OpenImage(selectedItem);
                }
            }
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("图片集合"), DisplayName("图片集合")]
        public IEnumerable<object> ItemsSource 
        {
            get { return (IEnumerable<object>)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("选中Index"), DisplayName("选中Index")]
        public int SelectIndex 
        {
            get { return (int)GetValue(SelectIndexProperty); }
            set { SetValue(SelectIndexProperty, value); }
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("选中项"), DisplayName("选中项")]
        public object SelectedItem 
        {
            get { return GetValue(SelectedItemProperty); }
            set { SetValue(SelectedItemProperty, value); }
        }

       
    }
}
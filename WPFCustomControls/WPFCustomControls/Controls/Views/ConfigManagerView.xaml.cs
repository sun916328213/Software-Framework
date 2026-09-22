using CommunityToolkit.Mvvm.Input;
using Extension.Core.Conversion;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WPFCustomControls.Window.Views;

namespace WPFCustomControls.Controls
{
    /// <summary>
    /// ConfigManagerView.xaml 的交互逻辑
    /// 通用配置管理视图：内嵌 DataGridView，支持增加/删除/保存/解锁/选择与 Json 持久化。
     /// </summary>
    public partial class ConfigManagerView : UserControl
    {
        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register("IsReadOnly", typeof(bool), typeof(ConfigManagerView), new PropertyMetadata(true));

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register("ItemsSource", typeof(System.Collections.IEnumerable), typeof(ConfigManagerView), new PropertyMetadata(null));

        public static readonly DependencyProperty SelectItemProperty =
            DependencyProperty.Register("SelectItem", typeof(object), typeof(ConfigManagerView), new PropertyMetadata(null));

        public static readonly DependencyProperty FilePathProperty =
            DependencyProperty.Register("FilePath", typeof(string), typeof(ConfigManagerView), new PropertyMetadata(null));

        public static readonly DependencyProperty FileNameProperty =
            DependencyProperty.Register("FileName", typeof(string), typeof(ConfigManagerView), new PropertyMetadata(null));

        public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register("Title", typeof(string), typeof(ConfigManagerView), new PropertyMetadata(null));

        public static readonly DependencyProperty SelectVisibilityProperty =
       DependencyProperty.Register("SelectVisibility", typeof(Visibility), typeof(ConfigManagerView), new PropertyMetadata(Visibility.Visible));

        public static readonly DependencyProperty AddVisibilityProperty =
        DependencyProperty.Register("AddVisibility", typeof(Visibility), typeof(ConfigManagerView), new PropertyMetadata(Visibility.Visible));

        public static readonly DependencyProperty DeleteVisibilityProperty =
         DependencyProperty.Register("DeleteVisibility", typeof(Visibility), typeof(ConfigManagerView), new PropertyMetadata(Visibility.Visible));



        // 新增事件回调依赖属性-------------------------------------------------------------------------------------
        public static readonly DependencyProperty SelectedCallbackProperty =
            DependencyProperty.Register("SelectedCallback", typeof(Action), typeof(ConfigManagerView), new PropertyMetadata(null));

        public static readonly DependencyProperty AddCallbackProperty =
            DependencyProperty.Register("AddCallback", typeof(Action), typeof(ConfigManagerView), new PropertyMetadata(null));

        public static readonly DependencyProperty DeletedCallbackProperty =
            DependencyProperty.Register("DeletedCallback", typeof(Action), typeof(ConfigManagerView), new PropertyMetadata(null));

        public static readonly DependencyProperty SaveCallbackProperty =
          DependencyProperty.Register("SaveCallback", typeof(Action), typeof(ConfigManagerView), new PropertyMetadata(null));

        public static readonly DependencyProperty RowHeightProperty =
        DependencyProperty.Register("RowHeight", typeof(double), typeof(ConfigManagerView), new PropertyMetadata(double.NaN));


        public static readonly DependencyProperty DataGridColumnsProperty =
          DependencyProperty.Register(
              nameof(DataGridColumns),
              typeof(ObservableCollection<DataGridColumn>),
              typeof(ConfigManagerView),
              new PropertyMetadata(null));

        /// <summary>
        /// 公开 DataGrid 列集合，外部可直接赋值
        /// </summary>
        [Bindable(true)]
        [Category("自定义属性"), Description("DataGrid 列集合"), DisplayName("DataGrid 列集合")]
        public ObservableCollection<DataGridColumn> DataGridColumns
        {
            get { return (ObservableCollection<DataGridColumn>)GetValue(DataGridColumnsProperty); }
            set { SetValue(DataGridColumnsProperty, value); }
        }

        /// <summary>
        /// 是否只读
        /// </summary>
        [Bindable(true)]
        [Category("自定义属性"), Description("DataGrid行高"), DisplayName("DataGrid行高")]
        public double RowHeight
        {
            get { return (double)GetValue(RowHeightProperty); }
            set { SetValue(RowHeightProperty, value); }
        }

        /// <summary>
        /// 是否只读
        /// </summary>
        [Bindable(true)]
        [Category("自定义属性"), Description("是否只读"), DisplayName("是否只读")]
        public bool IsReadOnly
        {
            get { return (bool)GetValue(IsReadOnlyProperty); }
            set { SetValue(IsReadOnlyProperty, value); }
        }

        /// <summary>
        /// 数据源
        /// </summary>
        [Bindable(true)]
        [Category("自定义属性"), Description("数据源"), DisplayName("数据源")]
        public System.Collections.IEnumerable ItemsSource
        {
            get { return (System.Collections.IEnumerable)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        /// <summary>
        /// 选中项 主要给外部控件使用
        /// </summary>
        [Bindable(true)]
        [Category("自定义属性"), Description("选中项 主要给外部控件使用"), DisplayName("选中项 主要给外部控件使用")]
        public object SelectItem
        {
            get { return (object)GetValue(SelectItemProperty); }
            set { SetValue(SelectItemProperty, value); }
        }

        /// <summary>
        /// 文件路径
        /// </summary>
        [Bindable(true)]
        [Category("自定义属性"), Description("文件路径"), DisplayName("文件路径")]
        public string FilePath
        {
            get { return (string)GetValue(FilePathProperty); }
            set { SetValue(FilePathProperty, value); }
        }

        /// <summary>
        /// 文件名称
        /// </summary>
        [Bindable(true)]
        [Category("自定义属性"), Description("文件名称"), DisplayName("文件名称")]
        public string FileName
        {
            get { return (string)GetValue(FileNameProperty); }
            set { SetValue(FileNameProperty, value); }
        }

        /// <summary>
        /// 标题
        /// </summary>
        [Bindable(true)]
        [Category("自定义属性"), Description("标题"), DisplayName("标题")]
        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        /// <summary>
        /// 选择按钮显示状态
        /// </summary>
        [Bindable(true)]
        [Category("自定义属性"), Description("选择按钮显示状态"), DisplayName("选择按钮显示状态")]
        public Visibility SelectVisibility
        {
            get { return (Visibility)GetValue(SelectVisibilityProperty); }
            set { SetValue(SelectVisibilityProperty, value); }
        }

        /// <summary>
        /// 增按钮显示状态
        /// </summary>
        [Bindable(true)]
        [Category("自定义属性"), Description("增按钮显示状态"), DisplayName("增按钮显示状态")]
        public Visibility AddVisibility
        {
            get { return (Visibility)GetValue(AddVisibilityProperty); }
            set { SetValue(AddVisibilityProperty, value); }
        }

        /// <summary>
        /// 删除按钮显示状态
        /// </summary>
        [Bindable(true)]
        [Category("自定义属性"), Description("删除按钮显示状态"), DisplayName("删除按钮显示状态")]
        public Visibility DeleteVisibility
        {
            get { return (Visibility)GetValue(DeleteVisibilityProperty); }
            set { SetValue(DeleteVisibilityProperty, value); }
        }

        /// <summary>
        /// 选中项回调
        /// </summary>
        [Bindable(true)]
        [Category("自定义属性"), Description("选中项回调"), DisplayName("选中项回调")]
        public Action SelectedCallback
        {
            get { return (Action)GetValue(SelectedCallbackProperty); }
            set { SetValue(SelectedCallbackProperty, value); }
        }

        /// <summary>
        /// 添加项回调
        /// </summary>
        [Bindable(true)]
        [Category("自定义属性"), Description("添加项回调"), DisplayName("添加项回调")]
        public Action AddCallback
        {
            get { return (Action)GetValue(AddCallbackProperty); }
            set { SetValue(AddCallbackProperty, value); }
        }

        /// <summary>
        /// 删除项回调
        /// </summary>
        [Bindable(true)]
        [Category("自定义属性"), Description("删除项回调"), DisplayName("删除项回调")]
        public Action DeletedCallback
        {
            get { return (Action)GetValue(DeletedCallbackProperty); }
            set { SetValue(DeletedCallbackProperty, value); }
        }

        /// <summary>
        /// 保存回调
        /// </summary>
        [Bindable(true)]
        [Category("自定义属性"), Description("保存回调"), DisplayName("保存回调")]
        public Action SaveCallback
        {
            get { return (Action)GetValue(SaveCallbackProperty); }
            set { SetValue(SaveCallbackProperty, value); }
        }


        // 内部命令
        public RelayCommand SelectCommand { get; }
        public RelayCommand LockCommand { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand AddCommand { get; }
        public RelayCommand DeleteCommand { get; }

        public ConfigManagerView()
        {
            InitializeComponent();
            DataGridColumns = new ObservableCollection<System.Windows.Controls.DataGridColumn>();
            // 初始化内部命令
            SelectCommand = new RelayCommand(SelecItem);
            LockCommand = new RelayCommand(Lock);
            SaveCommand = new RelayCommand(SaveData);
            AddCommand = new RelayCommand(AddItem);
            DeleteCommand = new RelayCommand(DeleteItem);

            Loaded += (s, e) =>
            {
                if (DataGridColumns != null && DataGridColumns.Count > 0)
                {
                    // ⚠️ DataGridView（= 裸 DataGrid）的 AutoGenerateColumns 默认是 **true**。
                    // 不关掉的话：我们传的中文列加进去之后，DataGrid 还会照 ItemsSource 的元素类型
                    // **再自动生成一套**英文列（Name / FilePath / Separator / SaveDays / LogType）挂在后面。
                    // 症状（LogView 实测）：总列数 5 → **10**；星号列被挤成 ActualWidth=**20px**
                    // （名称/日志目录/分隔符/保存天数 全废），表头出现英文属性名，横向滚动条长得离谱。
                    // ⇒ 有自定义列时必须关掉自动生成。**没传列的宿主（全仓 9 处）保持 true**，行为不变。
                    DataGridView.AutoGenerateColumns = false;
                    DataGridView.Columns.Clear();
                    foreach (var col in DataGridColumns)
                    {
                        DataGridView.Columns.Add(col);
                    }
                }
            };
        }



        public void SelecItem()
        {
            if (DataGridView.SelectedItem != null)
            {
                SelectItem = DataGridView.SelectedItem;
                SelectedCallback?.Invoke();
            }
            else
            {
                Prompt();
            }
        }

        public void Lock()
        {
            if (IsReadOnly == false)
            {
                IsReadOnly = true;
                btn_Set.Content = "解锁";
            }
            else
            {
                MessagePrompt messagePrompt = new MessagePrompt("提示", "请确认是否解锁");
                var ret = messagePrompt.ShowDialog();
                if (ret == false) return;
                IsReadOnly = false;
                btn_Set.Content = "上锁";
            }
        }

        // 保存数据
        private void SaveData()
        {
            if (string.IsNullOrEmpty(FilePath) || string.IsNullOrEmpty(FileName))
            {
                MessageBox.Show("请检查文件路径(FilePath)和文件名称(FileName)","提示" );
                return;
            }
            if (ItemsSource != null  )
            {
                try
                {
                    // 确保目录存在
                    var directory = System.IO.Path.GetDirectoryName(FilePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // 保存数据
                    JsonEx.Save(ItemsSource, FilePath, FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存数据时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                SaveCallback?.Invoke();
            }
        }


        // 添加项
        private void AddItem()
        {
            // 尝试直接添加项到ItemsSource集合
            if (TryAddItemToCollection())
            {
                return;
            }

            // 如果无法直接创建项，则通过回调让外部创建
            AddCallback?.Invoke();
        }

        // 尝试向集合中添加新项
        private bool TryAddItemToCollection()
        {
            if (ItemsSource is IList list)
            {
                // 获取集合元素类型
                Type itemType = GetListItemType(list);
                if (itemType != null)
                {
                    try
                    {
                        // 创建新项并添加到集合中
                        var newItem = Activator.CreateInstance(itemType);
                        list.Add(newItem);
                        AddCallback?.Invoke();
                        return true;
                    }
                    catch (Exception)
                    {
                        // 如果创建实例失败，则返回false，使用回调方式
                    }
                }
            }

            return false;
        }

        // 获取IList集合中元素的类型
        private Type GetListItemType(IList list)
        {
            // 检查是否是ObservableCollection<T>或其它泛型IList<T>实现
            var listType = list.GetType();

            // 获取泛型接口
            foreach (var interfaceType in listType.GetInterfaces())
            {
                if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IList<>))
                {
                    return interfaceType.GetGenericArguments()[0];
                }
            }

            // 如果是ObservableCollection<T>的直接实例
            if (listType.IsGenericType && listType.GetGenericTypeDefinition() == typeof(ObservableCollection<>))
            {
                return listType.GetGenericArguments()[0];
            }

            return null;
        }


        // 删除项
        private void DeleteItem()
        {
            if (DataGridView.SelectedItem != null)
            {
                var itemToDelete = DataGridView.SelectedItem;


                // 如果ItemsSource是ObservableCollection，我们可以直接从中移除
                if (ItemsSource is IList list)
                {
                    list.Remove(itemToDelete);
                }

                DeletedCallback?.Invoke();
            }
            else
            {
                Prompt();
            }
        }


        private void Prompt()
        {
            MessagePrompt messagePrompt = new MessagePrompt();
            messagePrompt.Title = "提示";
            messagePrompt.Message = "请先选择后再进行操作!!";
            messagePrompt.Visibility_Cancel = Visibility.Hidden;
            messagePrompt.ShowDialog();
        }
    }
}

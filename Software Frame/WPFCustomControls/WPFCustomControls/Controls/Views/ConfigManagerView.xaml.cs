// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：ConfigManagerView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、CommunityToolkit.Mvvm.Input、Extension.Core.Conversion、System.Collections、System.Collections.ObjectModel、System.ComponentModel。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using CommunityToolkit.Mvvm.Input;
using Extension.Core.Conversion;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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


        // 内部命令默认实现（T10）
        //
        // 5 个按钮的 Command 是**可被外部覆盖的依赖属性**（见下方 seam 区）。
        // 依赖属性元数据默认是 null，由 CoerceValueCallback 折成这里的默认实现
        // ⇒「没人设置 = 内置行为」，15 个既有宿主零改动。
        private readonly RelayCommand _defaultSelectCommand;
        private readonly RelayCommand _defaultLockCommand;
        private readonly RelayCommand _defaultSaveCommand;
        private readonly RelayCommand _defaultAddCommand;
        private readonly RelayCommand _defaultDeleteCommand;

        // 外部 Command seam（T10）-------------------------------------------------------------------
        //
        // 与本仓库既有约定一致（QueryView / AddUserView / DeletUserView / LoginView 都是
        // 「ICommand 依赖属性 + 宿主注入」）。区别在于本控件 5 个按钮**自带默认行为**，
        // 所以这里不是「宿主必须提供」，而是「宿主可选覆盖」：
        //   未设置 ⇒ 依赖属性由构造函数写成内置 RelayCommand（改造前行为，逐字保留）；
        //   已设置 ⇒ 走宿主提供的命令（可用 CanExecute 控制按钮可用性）。
        //
        // 「没有命令」只有一种含义：依赖属性为 null（显式设 null 或 ClearValue 都算），
        // 此时按钮禁用。**不额外把 null 折回内置** —— 那会让「显式 null」与「ClearValue」
        // 两条本该等价的路径得到不同结果（coerce 只在「设过值」那条路上生效），
        // 留下一个很难查的不一致。
        //
        // ⚠️ 覆盖只替换**命令体**，不接管 *Callback 回调：宿主若覆盖 SaveCommand，
        // 内置的 JsonEx.Save 与 SaveCallback 都不会再执行，需自行处理落盘与通知。
        // 反过来，未覆盖时 SaveCallback 等回调语义与改造前完全一致。
        //
        // ⚠️ 内置默认必须写成依赖属性的**本地值**（见构造函数），不能只写成 CLR getter 的
        // ?? 回退：XAML 的 {Binding Path=SaveCommand} 经 DependencyPropertyDescriptor
        // 直接读**依赖属性本身**（GetValue），**绕过 CLR getter**。只写在 getter 里，
        // 会出现「view.SaveCommand 读出内置命令、而按钮绑到 null 并静默禁用」的诡异状态。

        public static readonly DependencyProperty SelectCommandProperty =
            DependencyProperty.Register("SelectCommand", typeof(ICommand), typeof(ConfigManagerView), new PropertyMetadata(null));

        public static readonly DependencyProperty LockCommandProperty =
            DependencyProperty.Register("LockCommand", typeof(ICommand), typeof(ConfigManagerView), new PropertyMetadata(null));

        public static readonly DependencyProperty SaveCommandProperty =
            DependencyProperty.Register("SaveCommand", typeof(ICommand), typeof(ConfigManagerView), new PropertyMetadata(null));

        public static readonly DependencyProperty AddCommandProperty =
            DependencyProperty.Register("AddCommand", typeof(ICommand), typeof(ConfigManagerView), new PropertyMetadata(null));

        public static readonly DependencyProperty DeleteCommandProperty =
            DependencyProperty.Register("DeleteCommand", typeof(ICommand), typeof(ConfigManagerView), new PropertyMetadata(null));

        /// <summary>选择指令。未设置时走内置 <see cref="SelecItem"/>。</summary>
        [Bindable(true)]
        [Category("自定义属性"), Description("选择指令（留空使用内置行为）"), DisplayName("选择指令")]
        public ICommand SelectCommand
        {
            get { return (ICommand)GetValue(SelectCommandProperty); }
            set { SetValue(SelectCommandProperty, value); }
        }

        /// <summary>上锁/解锁指令。未设置时走内置 <see cref="Lock"/>。</summary>
        [Bindable(true)]
        [Category("自定义属性"), Description("上锁/解锁指令（留空使用内置行为）"), DisplayName("上锁/解锁指令")]
        public ICommand LockCommand
        {
            get { return (ICommand)GetValue(LockCommandProperty); }
            set { SetValue(LockCommandProperty, value); }
        }

        /// <summary>保存指令。未设置时走内置 <see cref="SaveData"/>。</summary>
        [Bindable(true)]
        [Category("自定义属性"), Description("保存指令（留空使用内置行为）"), DisplayName("保存指令")]
        public ICommand SaveCommand
        {
            get { return (ICommand)GetValue(SaveCommandProperty); }
            set { SetValue(SaveCommandProperty, value); }
        }

        /// <summary>增加指令。未设置时走内置 <see cref="AddItem"/>。</summary>
        [Bindable(true)]
        [Category("自定义属性"), Description("增加指令（留空使用内置行为）"), DisplayName("增加指令")]
        public ICommand AddCommand
        {
            get { return (ICommand)GetValue(AddCommandProperty); }
            set { SetValue(AddCommandProperty, value); }
        }

        /// <summary>删除指令。未设置时走内置 <see cref="DeleteItem"/>。</summary>
        [Bindable(true)]
        [Category("自定义属性"), Description("删除指令（留空使用内置行为）"), DisplayName("删除指令")]
        public ICommand DeleteCommand
        {
            get { return (ICommand)GetValue(DeleteCommandProperty); }
            set { SetValue(DeleteCommandProperty, value); }
        }

        /// <summary>
        /// 初始化 ConfigManagerView 实例及其协作对象。
        ///
        /// <para>
        /// <b>顺序有约束</b>：内置命令必须早于 <see cref="InitializeComponent"/> 写进依赖属性。
        /// XAML 里 5 个按钮的 <c>Command</c> 绑定在加载时就会求值一次；那一刻若依赖属性还是
        /// null，按钮会停在「无命令」状态（表现为禁用），此后也不会自动重新求值 ——
        /// 因为依赖属性的值并没有再变过。
        /// </para>
        /// </summary>
        public ConfigManagerView()
        {
            // T10：内置命令先于 InitializeComponent（理由见上方 <summary>）。
            // 必须写成依赖属性的**本地值**，不能只放在 CLR getter 的 ?? 回退里 ——
            // XAML 绑定读的是依赖属性本身，看不到 getter。
            _defaultSelectCommand = new RelayCommand(SelecItem);
            _defaultLockCommand = new RelayCommand(Lock);
            _defaultSaveCommand = new RelayCommand(SaveData);
            _defaultAddCommand = new RelayCommand(AddItem);
            _defaultDeleteCommand = new RelayCommand(DeleteItem);
            SelectCommand = _defaultSelectCommand;
            LockCommand = _defaultLockCommand;
            SaveCommand = _defaultSaveCommand;
            AddCommand = _defaultAddCommand;
            DeleteCommand = _defaultDeleteCommand;

            InitializeComponent();
            DataGridColumns = new ObservableCollection<System.Windows.Controls.DataGridColumn>();

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



        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 SelecItem。
        /// 正文中的关键协作调用包括 Prompt；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Lock。
        /// 正文中的关键协作调用包括 ShowDialog；具体条件和先后次序见方法体。
        /// </summary>
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


        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Prompt。
        /// 正文中的关键协作调用包括 ShowDialog；具体条件和先后次序见方法体。
        /// </summary>
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

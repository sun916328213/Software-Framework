using DPLibrary_Base_F48;
using DPLibrary_Base_F48.Vision;
using DPLibrary_Halcon_F48;
using DPLibrary_Halcon_F48.Base;
using HalconDotNet;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace Ctr_WPF_F48.Ctr_Halcon.View
{
    /// <summary>
    /// UserControl1.xaml 的交互逻辑
    /// </summary>
    public partial class Ctr_HWindow_DP : UserControl, IDisposable
    {
        /// <summary>
        /// Halcon 显示窗口（来自内嵌的 HSmartWindowView，含右键菜单）
        /// </summary>
        public HSmartWindowControlWPF HWindow1 { get; private set; }

        public Ctr_HWindow_DP()
        {
            InitializeComponent();

            HWindow1 = HSWindowView.HSmart;
            HWindow1.HMouseDown += HWindow1_HMouseDown;
            HWindow1.HMouseUp += HWindow1_HMouseUp;

            foreach (E_ROIType item in Enum.GetValues(typeof(E_ROIType)))
            {
                if (item == E_ROIType.Null)
                {
                    continue;
                }
                MenuItem menuItem = new MenuItem();
                menuItem.Header = item.ToString();
                menuItem.Click += ROIMenuItem_Click;
                switch (item)
                {
                    case E_ROIType.Rectangle:
                        menuItem.Icon = new PackIcon { Kind = PackIconKind.CardOutline };
                        break;
                    case E_ROIType.Rectangle2:
                        menuItem.Icon = new PackIcon { Kind = PackIconKind.CardsOutline };
                        break;
                    case E_ROIType.Circle:
                        menuItem.Icon = new PackIcon { Kind = PackIconKind.CircleOutline };
                        break;
                    case E_ROIType.Ellipsoid:
                        menuItem.Icon = new PackIcon { Kind = PackIconKind.EllipseOutline };
                        break;
                    case E_ROIType.Polygon:
                        menuItem.Icon = new PackIcon { Kind = PackIconKind.VectorPolyline };
                        break;
                    default:
                        break;
                }
                mu_Control.Items.Add(menuItem);
            }

            foreach (E_XLDType item in Enum.GetValues(typeof(E_XLDType)))
            {
                if (item == E_XLDType.Null)
                {
                    continue;
                }
                MenuItem menuItem = new MenuItem();
                menuItem.Header = item.ToString();
                menuItem.Click += MeasureMenuItem_Click;
                switch (item)
                {
                    case E_XLDType.Line:
                        menuItem.Icon = new PackIcon { Kind = PackIconKind.Minus };
                        break;
                    case E_XLDType.SectorCircle:
                        menuItem.Icon = new PackIcon { Kind = PackIconKind.VectorCircleVariant };
                        break;
                    case E_XLDType.SectorEllipsoid:
                        menuItem.Icon = new PackIcon { Kind = PackIconKind.VectorEllipse };
                        break;
                    default:
                        break;
                }
                mu_Measure.Items.Add(menuItem);
            }

            this.IsVisibleChanged += Ctr_HWindow_DP_IsVisibleChanged;

            mRefreshLoop = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(1000 / FPS) //默认60fps，大概17ms刷新一次
            };
            mRefreshLoop.Tick += _refreshLoopTick;
            mRefreshLoop.Start();
            Loaded += Ctr_HWindow_DP_Loaded;
        }

        private void _refreshLoopTick(object sender, EventArgs e)
        {
            if (!mPending) return;
            mPending = false;
            ResetDisp();
        }

        /// <summary>
        /// 第一次显示工具后 如果工具存在数据则进行显示
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Ctr_HWindow_DP_Loaded(object sender, RoutedEventArgs e)
        {
            ResetDisp();
        }

        /// <summary>
        /// 用于固定刷新的定时器
        /// </summary>
        private readonly DispatcherTimer mRefreshLoop;
        /// <summary>
        /// 标记脏数据，用于显示时的定时器刷新
        /// </summary>
        private volatile bool mPending;
        /// <summary>
        /// 图像锁
        /// </summary>
        private object mImageLock = new object();
        private void Ctr_HWindow_DP_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                mRefreshLoop.Start();
            }
            else
            {
                mRefreshLoop.Stop();
            }
        }

        private void ROIMenuItem_Click(object sender, RoutedEventArgs e)
        {
            EnsureDispROIDatas();
            CreateROIType = (E_ROIType)Enum.Parse(typeof(E_ROIType), ((MenuItem)sender).Header.ToString());
            IsDrawingROI = true;
        }

        public void BeginDrawROI(E_ROIType roiType)
        {
            EnsureDispROIDatas();
            SelectedEditableRoi = null;
            CreateROIType = roiType;
            IsDrawingROI = true;
            SetCurrentValue(ShowRoiLayerProperty, true);
            SetCurrentValue(DisplayLayersProperty, DisplayLayers | DisplayLayers.ROI);
        }

        private void MeasureMenuItem_Click(object sender, RoutedEventArgs e)
        {
            EnsureDispMeasurePathDatas();
            CreateMeasureType = (E_XLDType)Enum.Parse(typeof(E_XLDType), ((MenuItem)sender).Header.ToString());
            IsDrawingMeasure = true;
            // 打开卡尺绘制层，否则 MeasurePaths 被绘制但不可见
            SetCurrentValue(DisplayLayersProperty, DisplayLayers | DisplayLayers.MeasurePath);
        }

        #region  依赖属性( 刷新频率 - int : FPS)

        /// <summary>
        /// 刷新频率附加
        /// </summary>
        public static readonly DependencyProperty FPSProperty =
              DependencyProperty.Register(
                  name: "FPS", // 属性名称
                  propertyType: typeof(int), // 属性类型
                  ownerType: typeof(Ctr_HWindow_DP), // 所属控件类型
                  new PropertyMetadata(
                      60, // 默认值
                      propertyChangedCallback: OnFPSChanged // 属性变化回调
                  )
              );

        /// <summary>
        /// 刷新频率源
        /// </summary>
        public int FPS
        {
            get => (int)GetValue(FPSProperty);
            set => SetValue(FPSProperty, value);
        }

        /// <summary>
        /// 刷新频率变化时的处理
        /// </summary>
        private static void OnFPSChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Ctr_HWindow_DP control)
            {
                int val = (int)e.NewValue;
                if (val <= 0) val = 1;
                control.mRefreshLoop.Interval = TimeSpan.FromMilliseconds(1000.0 / val);
            }
        }

        #endregion

        #region  依赖属性( 图像源 - HObject : ImageSource)

        /// <summary>
        /// 图像源附加
        /// </summary>
        public static readonly DependencyProperty ImageSourceProperty =
              DependencyProperty.Register(
                  name: "ImageSource", // 属性名称
                  propertyType: typeof(HObject), // 属性类型
                  ownerType: typeof(Ctr_HWindow_DP), // 所属控件类型
                  new FrameworkPropertyMetadata(
                      null, // 默认值
                      FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                      OnImageSourceChanged // 属性变化回调
                  )
              );

        /// <summary>
        /// 图像源源
        /// </summary>
        public HObject ImageSource
        {
            get => (HObject)GetValue(ImageSourceProperty);
            set => SetValue(ImageSourceProperty, value);
        }

        /// <summary>
        /// 图像源变化时的处理
        /// </summary>
        private static void OnImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Ctr_HWindow_DP control)
            {
                //if (control.mIsLoadFromFile && e.OldValue != null)
                //{
                //    lock (control.mImageLock)
                //    {
                //        HObject old = e.OldValue as HObject;//非托管，手动释放
                //        old.Dispose();
                //        old = null;
                //        control.mIsLoadFromFile = false;
                //    }
                //}
                if (!ReferenceEquals(e.OldValue, e.NewValue))
                {
                    control._lastValidImage?.Dispose();
                    control._lastValidImage = null;
                    control.mCurrentDrawing = null;
                    control.IsDrawingROI = false;
                    control.IsDrawingMeasure = false;
                }
                control.isFirstLoad = true;
                // 这里可以添加属性变化后的逻辑（如加载图像）
                control.mPending = true;
            }
        }

        #endregion

        #region  依赖属性( 用于显示的文本信息 - ObservableCollection<HDispMessageData> : DispMessageDatas)

        /// <summary>
        /// 用于显示的文本信息附加
        /// </summary>
        public static readonly DependencyProperty DispMessageDatasProperty =
              DependencyProperty.Register(
                  name: "DispMessageDatas", // 属性名称
                  propertyType: typeof(BulkObservableCollection<I_DispMessage>), // 属性类型
                  ownerType: typeof(Ctr_HWindow_DP), // 所属控件类型
                  new FrameworkPropertyMetadata(
                      null,// 默认值
                      FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                      OnDispMessageDatasChanged
                      )
              );

        /// <summary>
        /// 用于显示的文本信息源
        /// </summary>
        public BulkObservableCollection<I_DispMessage> DispMessageDatas
        {
            get => (BulkObservableCollection<I_DispMessage>)GetValue(DispMessageDatasProperty);
            set => SetValue(DispMessageDatasProperty, value);
        }

        private static void OnDispMessageDatasChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Ctr_HWindow_DP control)
            {
                if (e.OldValue is BulkObservableCollection<I_DispMessage> oldCollection)
                {
                    oldCollection.CollectionChanged -= control.OnDispMessageDatasCollectionChanged;

                }

                if (e.NewValue is BulkObservableCollection<I_DispMessage> newCollection)
                {
                    newCollection.CollectionChanged += control.OnDispMessageDatasCollectionChanged;
                }
            }
        }

        private void OnDispMessageDatasCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // 每当集合 Add/Remove/Reset 都会触发}
            mPending = true;
        }

        #endregion

        #region  依赖属性( 区域列表 - HDispRegionDataCollection : DispRegionDatas)

        /// <summary>
        /// 区域列表附加
        /// </summary>
        public static readonly DependencyProperty DispRegionDatasProperty =
              DependencyProperty.Register(
                  name: "DispRegionDatas", // 属性名称
                  propertyType: typeof(BulkObservableCollection<HROI>), // 属性类型
                  ownerType: typeof(Ctr_HWindow_DP), // 所属控件类型
                  new FrameworkPropertyMetadata(
                      null, // 默认值
                      FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                      OnDispRegionDatasChanged // 属性变化回调
                  )
              );

        /// <summary>
        /// 区域列表源
        /// </summary>
        public BulkObservableCollection<HROI> DispRegionDatas
        {
            get => (BulkObservableCollection<HROI>)GetValue(DispRegionDatasProperty);
            set => SetValue(DispRegionDatasProperty, value);
        }

        /// <summary>
        /// 区域列表变化时的处理
        /// </summary>
        private static void OnDispRegionDatasChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Ctr_HWindow_DP control)
            {
                if (e.OldValue is BulkObservableCollection<HROI> oldCollection)
                {
                    oldCollection.CollectionChanged -= control.OnDispRegionDatasCollectionChanged;
                }

                if (e.NewValue is BulkObservableCollection<HROI> newCollection)
                {
                    newCollection.CollectionChanged += control.OnDispRegionDatasCollectionChanged;
                }
            }
        }

        private void OnDispRegionDatasCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // 每当集合 Add/Remove/Reset 都会触发
            mPending = true;
        }

        #endregion

        #region  依赖属性( XLD列表 - BulkObservableCollection<HDispXLDData> : DispXLDDatas)

        /// <summary>
        /// XLD列表附加
        /// </summary>
        public static readonly DependencyProperty DispXLDDatasProperty =
              DependencyProperty.Register(
                  name: "DispXLDDatas", // 属性名称
                  propertyType: typeof(BulkObservableCollection<I_HXLD>), // 属性类型
                  ownerType: typeof(Ctr_HWindow_DP), // 所属控件类型
                  new FrameworkPropertyMetadata(
                      null, // 默认值
                      FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                      OnDispXLDDatasChanged // 属性变化回调
                  )
              );

        /// <summary>
        /// XLD列表源
        /// </summary>
        public BulkObservableCollection<I_HXLD> DispXLDDatas
        {
            get => (BulkObservableCollection<I_HXLD>)GetValue(DispXLDDatasProperty);
            set => SetValue(DispXLDDatasProperty, value);
        }

        /// <summary>
        /// XLD列表变化时的处理
        /// </summary>
        private static void OnDispXLDDatasChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Ctr_HWindow_DP control)
            {
                if (e.OldValue is BulkObservableCollection<I_HXLD> oldCollection)
                {
                    oldCollection.CollectionChanged -= control.OnDispXLDDatasCollectionChanged;
                }

                if (e.NewValue is BulkObservableCollection<I_HXLD> newCollection)
                {
                    newCollection.CollectionChanged += control.OnDispXLDDatasCollectionChanged;
                }

            }
        }

        private void OnDispXLDDatasCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // 每当集合 Add/Remove/Reset 都会触发
            mPending = true;
        }

        #endregion

        #region  依赖属性( ROI区域列表 - BulkObservableCollection<I_ROI> : DispROIDatas)

        /// <summary>
        /// ROI区域列表附加
        /// </summary>
        public static readonly DependencyProperty DispROIDatasProperty =
              DependencyProperty.Register(
                  name: "DispROIDatas", // 属性名称
                  propertyType: typeof(BulkObservableCollection<HROI>), // 属性类型
                  ownerType: typeof(Ctr_HWindow_DP), // 所属控件类型
                  new FrameworkPropertyMetadata(
                      null, // 默认值
                      FrameworkPropertyMetadataOptions.None,
                      OnDispROIDatasChanged // 属性变化回调
                  )
              );

        /// <summary>
        /// ROI区域列表源
        /// </summary>
        public BulkObservableCollection<HROI> DispROIDatas
        {
            get => (BulkObservableCollection<HROI>)GetValue(DispROIDatasProperty);
            set => SetValue(DispROIDatasProperty, value);
        }

        /// <summary>
        /// ROI区域列表变化时的处理
        /// </summary>
        private static void OnDispROIDatasChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Ctr_HWindow_DP control)
            {
                if (e.OldValue is BulkObservableCollection<HROI> oldCollection)
                {
                    oldCollection.CollectionChanged -= control.OnDispROIDatasCollectionChanged;
                    foreach (var item in oldCollection)
                    {
                        // 移除事件绑定，防止内存泄漏
                        item.SelectedChanged -= control.CurrentSelectChanged;
                    }
                }

                if (e.NewValue is BulkObservableCollection<HROI> newCollection)
                {
                    newCollection.CollectionChanged += control.OnDispROIDatasCollectionChanged;
                    foreach (var item in newCollection)
                    {
                        item.SelectedChanged += control.CurrentSelectChanged;
                    }
                }

                control.mCurrentDrawing = null;
                control.IsDrawingROI = false;
                control.ApplyEditableRoiState();
                control.mPending = true;
            }
        }

        private void OnDispROIDatasCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems == null) break;
                    foreach (HROI roi in e.NewItems)
                    {
                        if (roi == null) continue;
                        // 新增ROI注册事件
                        roi.SelectedChanged += CurrentSelectChanged;
                        roi.IsHit = ReferenceEquals(roi, SelectedEditableRoi);
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems == null) break;
                    foreach (HROI roi in e.OldItems)
                    {
                        if (roi == null) continue;
                        // 移除事件绑定，防止内存泄漏
                        roi.SelectedChanged -= CurrentSelectChanged;
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    // 集合整体清空时，解除所有绑定
                    if (sender is BulkObservableCollection<HROI> roiCollection)
                    {
                        foreach (var roi in roiCollection)
                        {
                            if (roi == null) continue;
                            roi.SelectedChanged -= CurrentSelectChanged;
                        }
                    }
                    break;
            }

            ApplyEditableRoiState();
            // 每当集合 Add/Remove/Reset 都会触发
            mPending = true;
        }

        #endregion

        #region  依赖属性( 卡尺路径轮廓 - BulkObservableCollection<HXLD2> : DispMeasurePathDatas)

        /// <summary>
        /// 卡尺路径轮廓附加
        /// </summary>
        public static readonly DependencyProperty DispMeasurePathDatasProperty =
              DependencyProperty.Register(
                  name: "DispMeasurePathDatas", // 属性名称
                  propertyType: typeof(BulkObservableCollection<HXLD2>), // 属性类型
                  ownerType: typeof(Ctr_HWindow_DP), // 所属控件类型
                  new FrameworkPropertyMetadata(
                      null, // 默认值
                      FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                      OnDispMeasurePathDatasChanged // 属性变化回调
                  )
              );

        /// <summary>
        /// 卡尺路径轮廓源
        /// </summary>
        public BulkObservableCollection<HXLD2> DispMeasurePathDatas
        {
            get => (BulkObservableCollection<HXLD2>)GetValue(DispMeasurePathDatasProperty);
            set => SetValue(DispMeasurePathDatasProperty, value);
        }

        /// <summary>
        /// 卡尺路径轮廓变化时的处理
        /// </summary>
        private static void OnDispMeasurePathDatasChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Ctr_HWindow_DP control)
            {
                if (e.OldValue is BulkObservableCollection<HXLD2> oldCollection)
                {
                    oldCollection.CollectionChanged -= control.OnDispMeasurePathDatasCollectionChanged;
                    foreach (var item in oldCollection)
                    {
                        // 移除事件绑定，防止内存泄漏
                        item.SelectedChanged -= control.CurrentSelectChanged;
                    }
                }

                if (e.NewValue is BulkObservableCollection<HXLD2> newCollection)
                {
                    newCollection.CollectionChanged += control.OnDispMeasurePathDatasCollectionChanged;
                    foreach (var item in newCollection)
                    {
                        item.SelectedChanged += control.CurrentSelectChanged;
                    }
                }

            }
        }
        private void OnDispMeasurePathDatasCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems == null) break;
                    foreach (HXLD2 xld in e.NewItems)
                    {
                        if (xld == null) continue;
                        // 注册事件
                        xld.SelectedChanged += CurrentSelectChanged;
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems == null) break;
                    foreach (HXLD2 xld in e.OldItems)
                    {
                        if (xld == null) continue;
                        // 移除事件绑定，防止内存泄漏
                        xld.SelectedChanged -= CurrentSelectChanged;
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    // 集合整体清空时，解除所有绑定
                    if (sender is BulkObservableCollection<HXLD2> xldCollection)
                    {
                        foreach (HXLD2 xld in xldCollection)
                        {
                            if (xld == null) continue;
                            xld.SelectedChanged -= CurrentSelectChanged;
                        }
                    }
                    break;
            }

            // 每当集合 Add/Remove/Reset 都会触发
            mPending = true;
        }

        #endregion

        #region  依赖属性( ROI显示层 - bool : ShowRoiLayer)

        public static readonly DependencyProperty ShowRoiLayerProperty =
              DependencyProperty.Register(
                  name: "ShowRoiLayer",
                  propertyType: typeof(bool),
                  ownerType: typeof(Ctr_HWindow_DP),
                  new FrameworkPropertyMetadata(false, OnShowRoiLayerChanged));

        public bool ShowRoiLayer
        {
            get => (bool)GetValue(ShowRoiLayerProperty);
            set => SetValue(ShowRoiLayerProperty, value);
        }

        private static void OnShowRoiLayerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Ctr_HWindow_DP control)
            {
                control.ApplyEditableRoiState();
                control.ResetDisp();
            }
        }

        #endregion

        #region  依赖属性( 当前可编辑ROI - HROI : SelectedEditableRoi)

        public static readonly DependencyProperty SelectedEditableRoiProperty =
              DependencyProperty.Register(
                  name: "SelectedEditableRoi",
                  propertyType: typeof(HROI),
                  ownerType: typeof(Ctr_HWindow_DP),
                  new FrameworkPropertyMetadata(null, OnSelectedEditableRoiChanged));

        public HROI SelectedEditableRoi
        {
            get => (HROI)GetValue(SelectedEditableRoiProperty);
            set => SetValue(SelectedEditableRoiProperty, value);
        }

        private static void OnSelectedEditableRoiChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Ctr_HWindow_DP control)
            {
                control.ApplyEditableRoiState();
                control.ResetDisp();
            }
        }

        #endregion

        #region  依赖属性( 是否展示菜单栏 - Visibility : IsShowMenu)

        /// <summary>
        /// 是否展示菜单栏附加
        /// </summary>
        public static readonly DependencyProperty IsShowMenuProperty =
              DependencyProperty.Register(
                  name: "IsShowMenu", // 属性名称
                  propertyType: typeof(Visibility), // 属性类型
                  ownerType: typeof(Ctr_HWindow_DP), // 所属控件类型
                  new FrameworkPropertyMetadata(
                      Visibility.Visible, // 默认值
                      FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                      OnIsShowMenuChanged // 属性变化回调
                  )
              );

        /// <summary>
        /// 是否展示菜单栏源
        /// </summary>
        public Visibility IsShowMenu
        {
            get => (Visibility)GetValue(IsShowMenuProperty);
            set => SetValue(IsShowMenuProperty, value);
        }

        /// <summary>
        /// 是否展示菜单栏变化时的处理
        /// </summary>
        private static void OnIsShowMenuChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Ctr_HWindow_DP control)
            {
                Visibility val = (Visibility)e.NewValue;
                control.menu.Visibility = val;
            }
        }

        #endregion

        #region  依赖属性( 是否展示功能菜单 - Visibility : IsShowFunctionMenu)

        public static readonly DependencyProperty IsShowFunctionMenuProperty =
              DependencyProperty.Register(
                  name: "IsShowFunctionMenu",
                  propertyType: typeof(Visibility),
                  ownerType: typeof(Ctr_HWindow_DP),
                  new FrameworkPropertyMetadata(Visibility.Visible));

        public Visibility IsShowFunctionMenu
        {
            get => (Visibility)GetValue(IsShowFunctionMenuProperty);
            set => SetValue(IsShowFunctionMenuProperty, value);
        }

        #endregion

        #region  依赖属性( 用于配置当前显示的东西 - DisplayLayers : DisplayLayers)

        /// <summary>
        /// 用于配置当前显示的东西附加
        /// </summary>
        public static readonly DependencyProperty DisplayLayersProperty =
              DependencyProperty.Register(
                  name: "DisplayLayers", // 属性名称
                  propertyType: typeof(DisplayLayers), // 属性类型
                  ownerType: typeof(Ctr_HWindow_DP), // 所属控件类型
                  new FrameworkPropertyMetadata(
                      DisplayLayers.Image, // 默认值(全显示)
                      FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                      OnDisplayLayersChanged // 属性变化回调
                  )
              );

        /// <summary>
        /// 用于配置当前显示的东西源
        /// </summary>
        public DisplayLayers DisplayLayers
        {
            get => (DisplayLayers)GetValue(DisplayLayersProperty);
            set => SetValue(DisplayLayersProperty, value);
        }

        /// <summary>
        /// 用于配置当前显示的东西变化时的处理
        /// </summary>
        private static void OnDisplayLayersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Ctr_HWindow_DP control)
            {
                //DisplayLayers val = (DisplayLayers)e.NewValue;
                // 这里可以添加属性变化后的逻辑
                control.ResetDisp();
            }
        }

        #endregion

        //实际图像坐标（0，0） - （mImageWidth，mImageHeight）
        double mImageWidth = 0;
        double mImageHeight = 0;

        /// <summary>
        /// 是否处于绘制ROI阶段
        /// </summary>
        public bool IsDrawingROI = false;

        /// <summary>
        /// 绘制的ROI类型
        /// </summary>
        public E_ROIType CreateROIType = E_ROIType.Null;

        /// <summary>
        /// 是否处于绘制卡尺阶段
        /// </summary>
        public bool IsDrawingMeasure = false;

        /// <summary>
        /// 绘制的卡尺类型
        /// </summary>
        public E_XLDType CreateMeasureType = E_XLDType.Null;

        /// <summary>
        /// 当前选中的ROI
        /// </summary>
        I_IsHit mCurrentDrawing = null;
        void CurrentSelectChanged(I_IsHit isHit, bool val)
        {
            if (isHit is HROI roi && !ReferenceEquals(roi, SelectedEditableRoi))
            {
                isHit.IsHit = false;
                mPending = true;
                return;
            }

            if (val)
            {
                if (mCurrentDrawing != null && mCurrentDrawing != isHit)
                {
                    mCurrentDrawing.IsHit = false;
                }
                mCurrentDrawing = isHit;
                mPending = true;
            }
            else if (ReferenceEquals(mCurrentDrawing, isHit))
            {
                mCurrentDrawing = null;
                mPending = true;
            }
        }

        private void ApplyEditableRoiState()
        {
            if (DispROIDatas == null)
                return;

            foreach (var roi in DispROIDatas)
            {
                if (roi == null)
                    continue;

                roi.IsHit = ShowRoiLayer && ReferenceEquals(roi, SelectedEditableRoi);
            }

            mCurrentDrawing = ShowRoiLayer ? SelectedEditableRoi : null;
        }

        /// <summary>
        /// 加载图像
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void LoadImage(object sender, RoutedEventArgs e)
        {
            string FileString = string.Empty;
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            dialog.Multiselect = false;//该值确定是否可以选择多个文件
            dialog.Title = "请选择文件夹";
            dialog.Filter = "图像(*.jfif*.tif*.tiff*.gif*.bmp*.jpg*.jpeg*.jp2*.png*.pcx*.pgm*.ppm*.pbm*.xwd*.ima*.hobj)|*.jfif;*.tif;*.tiff;*.gif;*.bmp;*.jpg;*.jpeg;*.jp2;*.png;*.pcx;*.pgm;*.ppm;*.pbm;*.xwd;*.ima;*.hobj |" +
                            "所有文件(*.*)|*.*";

            if (dialog.ShowDialog() == true)
            {
                FileString = dialog.FileName;
                try
                {
                    HObject image;
                    HOperatorSet.ReadImage(out image, FileString);
                    ImageSource = image;
                    isFirstLoad = true;
                    mPending = true;
                }
                catch (HalconDotNet.HOperatorException ex)
                {
                    MessageBox.Show($"无法加载图像文件：\n{FileString}\n\n错误信息：{ex.Message}\n\n可能原因：\n1. 文件格式与扩展名不匹配\n2. 文件已损坏\n3. 不支持的图像格式",
                        "图像加载失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"加载图像时发生未知错误：\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 保存图像
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveImage(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            dialog.Title = "请选择文件夹";
            dialog.Filter = "BMP 图像 (*.bmp)|*.bmp|JPEG 图像 (*.jpg)|*.jpg|PNG 图像 (*.png)|*.png";
            if (dialog.ShowDialog() == true)
            {
                string filePath = dialog.FileName;
                string extension = System.IO.Path.GetExtension(filePath).ToLower().TrimStart('.');
                using var image = CreateSaveImageSnapshot();
                HOperatorSet.WriteImage(image, extension, 0, filePath);
            }
        }

        private HObject CreateSaveImageSnapshot()
        {
            if (TryCloneInitializedImage(ImageSource, out var image))
                return image;

            if (TryCloneInitializedImage(_lastValidImage, out image))
                return image;

            var width = mImageWidth > 0 ? (int)Math.Round(mImageWidth) : Math.Max(1, (int)Math.Round(HWindow1?.ActualWidth ?? 640));
            var height = mImageHeight > 0 ? (int)Math.Round(mImageHeight) : Math.Max(1, (int)Math.Round(HWindow1?.ActualHeight ?? 480));
            HOperatorSet.GenImageConst(out image, "byte", width, height);
            return image;
        }

        private static bool TryCloneInitializedImage(HObject source, out HObject image)
        {
            image = null;
            if (source == null || !source.IsInitialized())
                return false;

            try
            {
                image = source.Clone();
                return image != null && image.IsInitialized();
            }
            catch
            {
                image?.Dispose();
                image = null;
                return false;
            }
        }

        private void ImageFirstLoad()
        {
            if (HWindow1.HalconWindow == null || !isFirstLoad)
            {
                return;
            }
            if (ImageSource != null)
            {
                HTuple w = null, h = null, pointer = null, type = null;
                try
                {
                    HOperatorSet.GetImagePointer1(ImageSource, out pointer, out type, out w, out h);

                    mImageHeight = h;
                    mImageWidth = w;

                    // 在窗体 Resize 或加载时调用
                    AutoSizeImage();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ImageFirstLoad] 获取图像尺寸失败: {ex.Message}");
                    // 尺寸获取失败时不设为 false，下次 ResetDisp 会重试
                    return;
                }
                finally
                {
                    w?.Dispose();
                    h?.Dispose();
                    pointer?.Dispose();
                    type?.Dispose();
                }
                isFirstLoad = false;
            }
        }

        bool isFirstLoad = true;

        /// <summary>
        /// 自适应控件大小
        /// </summary>
        /// <param name="hImage"></param>
        /// <exception cref="Exception"></exception>
        /// <summary>
        /// 选中ROI改变事件
        /// </summary>
        /// <param name="hROI"></param>
        private void AutoSizeImage()
        {
            //获取长宽比例
            double Width = HWindow1.ActualWidth;
            double Height = HWindow1.ActualHeight;
            // 布局尚未完成（尺寸为 0），跳过，避免除零产生 NaN 后 SetPart 抛异常
            if (Width <= 0 || Height <= 0 || double.IsNaN(Width) || double.IsNaN(Height) ||
                double.IsInfinity(Width) || double.IsInfinity(Height))
            {
                return;
            }
            double wRatio = mImageWidth / Width;
            double hRatio = mImageHeight / Height;
            //设置起始点与结束点
            HTuple col1, row1, col2, row2;
            if (wRatio >= hRatio)
            {
                col1 = 0;
                row1 = (-1.0) * ((Height * wRatio) - mImageHeight) / 2.0;
                col2 = col1 + mImageWidth;
                row2 = row1 + Height * wRatio;
            }
            else
            {
                col1 = (-1.0) * ((Width * hRatio) - mImageWidth) / 2.0;
                row1 = 0;
                col2 = col1 + Width * hRatio;
                row2 = mImageHeight;
            }
            HWindow1.HalconWindow.SetPart(row1, col1, row2, col2); // 自适应
            isFirstLoad = false;
        }

        /// <summary>
        /// 重加载显示数据
        /// </summary>
        private void ResetDisp()
        {
            if (HWindow1?.HalconWindow == null)
            {
                return;
            }

            var window = HWindow1.HalconWindow;
            var buffered = false;
            try
            {
                if (ImageSource != null)
                {
                    ImageFirstLoad();
                    window.SetWindowParam("flush", "false");
                    buffered = true;

                    window.ClearWindow();

                    if (IsDrawingROI)//处于绘制模式中
                    {
                        DispImage();
                        DispROI();
                    }
                    else
                    {
                        if (DisplayLayers.HasFlag(DisplayLayers.Image))
                        {
                            DispImage();
                        }
                        if (DisplayLayers.HasFlag(DisplayLayers.Region))
                        {
                            DispRegion();
                        }
                        if (DisplayLayers.HasFlag(DisplayLayers.XLD))
                        {
                            DispXLD();
                        }
                        if (DisplayLayers.HasFlag(DisplayLayers.ROI))
                        {
                            DispROI();
                        }
                        else
                        {
                            DeDispROI();
                        }
                        if (DisplayLayers.HasFlag(DisplayLayers.MeasurePath))
                        {
                            DispMeasure();
                        }
                        else
                        {
                            DeDispMeasure();
                        }
                        if (DisplayLayers.HasFlag(DisplayLayers.Message))
                        {
                            DispMessages();
                        }
                    }

                    window.FlushBuffer();
                }
                else
                {
                    // 没有图像时清空窗口，避免残留旧图像
                    window.ClearWindow();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResetDisp] 异常: {ex.Message}");
                // 后台工具可能正在替换/释放结果对象，本帧不提交并在下一帧重试。
                mPending = true;
            }
            finally
            {
                if (buffered)
                {
                    try { window.SetWindowParam("flush", "true"); }
                    catch { }
                }
            }
        }
        /// <summary>
        /// 显示图像
        /// </summary>
        private void DispImage()
        {
            // 检查 IsInitialized()。
            // 如果不查 IsInitialized，DispObj 就会报 #4056 错误。
            if (ImageSource != null && ImageSource.IsInitialized())
            {
                try
                {
                    if (HWindow1 != null && HWindow1.HalconWindow != null)
                    {
                        HWindow1.HalconWindow.DispObj(ImageSource);
                        // 显示成功后缓存一份独立快照，避免原 HObject 后续失效。
                        if (TryCloneInitializedImage(ImageSource, out var lastValidImage))
                        {
                            _lastValidImage?.Dispose();
                            _lastValidImage = lastValidImage;
                        }
                    }
                }
                catch (HalconDotNet.HOperatorException ex)
                {
                    // 如果 handle 已失效（多线程共享工具导致），保留上一帧有效图像
                    if (ex.GetErrorCode() == 4056)
                    {
                        // 尝试显示上一帧有效图像
                        if (_lastValidImage != null && _lastValidImage.IsInitialized())
                        {
                            try { HWindow1?.HalconWindow?.DispObj(_lastValidImage); }
                            catch { }
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[DispImage] 错误码={ex.GetErrorCode()}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>上一帧成功显示的图像缓存，用于 DispObj 失败时回退</summary>
        private HObject _lastValidImage = null;

        /// <summary>
        /// 显示数据
        /// </summary>
        private void DispMessages()
        {
            if (DispMessageDatas != null)
            {
                foreach (var item in DispMessageDatas)
                {
                    item.Draw(HWindow1.HalconWindow);
                }
            }
        }

        private void DispRegion()
        {
            if (DispRegionDatas != null)
            {
                foreach (var item in DispRegionDatas)
                {
                    item.Draw(HWindow1.HalconWindow);
                }
            }
        }

        private void DispROI()
        {
            var win = HWindow1?.HalconWindow;
            if (win != null && DispROIDatas != null && ShowRoiLayer)
            {
                foreach (var item in DispROIDatas)
                {
                    item?.Draw(win);
                }
            }
            else
            {
                DeDispROI();
            }
        }

        private void DeDispROI()
        {
            var win = HWindow1?.HalconWindow;
            if (win != null && DispROIDatas != null)
            {
                foreach (var item in DispROIDatas)
                {
                    item?.DeDraw(win);
                }
            }
        }

        private void DispMeasure()
        {
            var win = HWindow1?.HalconWindow;
            if (win != null && DispMeasurePathDatas != null)
            {
                foreach (var item in DispMeasurePathDatas)
                {
                    item?.Draw(win);
                }
            }
        }

        private void DeDispMeasure()
        {
            var win = HWindow1?.HalconWindow;
            if (win != null && DispMeasurePathDatas != null)
            {
                foreach (var item in DispMeasurePathDatas)
                {
                    item?.DeDraw(win);
                }
            }
        }

        private void DispXLD()
        {
            if (DispXLDDatas != null)
            {
                foreach (var item in DispXLDDatas)
                {
                    item.Draw(HWindow1.HalconWindow);
                }
            }
        }

        private void HWindow1_HMouseDown(object sender, HSmartWindowControlWPF.HMouseEventArgsWPF e)
        {
            if (e == null)
            {
                return;
            }
            if (e.Button == System.Windows.Input.MouseButton.Left)//左键
            {
                if (ImageSource != null)
                {
                    if (IsDrawingROI)
                    {
                        EnsureDispROIDatas();
                        HWindow1.HKeepAspectRatio = false;
                        HWindow1.HMoveContent = false;

                        //获取当前点坐标

                        HROI createdRoi = null;
                        switch (CreateROIType)
                        {
                            case E_ROIType.Rectangle://默认生成一个100 * 100 的正方形
                                createdRoi = new HROI_Rectangle(e.Row - 50, e.Column - 50, e.Row + 50, e.Column + 50);
                                break;
                            case E_ROIType.Rectangle2:
                                createdRoi = new HROI_Rectangle2(e.Column, e.Row, 0, 50, 50) { DispColor = E_VisionColor.Blue };
                                break;
                            case E_ROIType.Circle:
                                createdRoi = new HROI_Circle(e.Column, e.Row, 50) { DispColor = E_VisionColor.Blue };
                                break;
                            case E_ROIType.Ellipsoid:
                                createdRoi = new HROI_Ellipsoid(e.Column, e.Row, 0, 80, 50) { DispColor = E_VisionColor.Blue };
                                break;
                            case E_ROIType.Polygon:
                                createdRoi = new HROI_Polyline(new List<Point>() { new Point(e.Column - 50, e.Row - 50), new Point(e.Column + 50, e.Row + 50) }) { DispColor = E_VisionColor.Blue };
                                break;
                            default:
                                break;
                        }
                        if (createdRoi != null)
                        {
                            createdRoi.IsActive = true;
                            DispROIDatas.Add(createdRoi);
                            SelectedEditableRoi = createdRoi;
                            SetCurrentValue(ShowRoiLayerProperty, true);
                            createdRoi.IsHit = true;
                            mCurrentDrawing = createdRoi;
                        }
                        IsDrawingROI = false;
                        HWindow1.HKeepAspectRatio = true;
                        HWindow1.HMoveContent = true;
                    }
                    else if (IsDrawingMeasure)
                    {
                        EnsureDispMeasurePathDatas();
                        HWindow1.HKeepAspectRatio = false;
                        HWindow1.HMoveContent = false;

                        switch (CreateMeasureType)
                        {
                            case E_XLDType.Line:
                                DispMeasurePathDatas.Add(new HXLD_Line(e.Column, e.Row - 50, e.Column, e.Row + 50));
                                break;
                            case E_XLDType.SectorCircle:
                                DispMeasurePathDatas.Add(new HXLD_SectorCircle(e.Column, e.Row, 50, 0, 3.14));
                                break;
                            case E_XLDType.SectorEllipsoid:
                                break;
                            default:
                                break;
                        }
                        IsDrawingMeasure = false;
                        HWindow1.HKeepAspectRatio = true;
                        HWindow1.HMoveContent = true;
                    }
                }
            }
            else if (e.Button == System.Windows.Input.MouseButton.Right)//右键
            {
                if (mCurrentDrawing is HROI_Polyline)//多点
                {
                    mCurrentDrawing.IsHit = false;//停止绘制多线段
                    mPending = true;
                    mCurrentDrawing = null;
                    CreateROIType = E_ROIType.Null;
                }
            }
        }

        private void EnsureDispROIDatas()
        {
            if (DispROIDatas == null)
                SetCurrentValue(DispROIDatasProperty, new BulkObservableCollection<HROI>());
        }

        private void EnsureDispMeasurePathDatas()
        {
            if (DispMeasurePathDatas == null)
                SetCurrentValue(DispMeasurePathDatasProperty, new BulkObservableCollection<HXLD2>());
        }

        private void HWindow1_HMouseUp(object sender, HSmartWindowControlWPF.HMouseEventArgsWPF e)
        {
            IsDrawingROI = false;
            HWindow1.HKeepAspectRatio = true;
            HWindow1.HMoveContent = true;
        }

        public void Dispose()
        {
            mRefreshLoop.Stop();
            mRefreshLoop.Tick -= _refreshLoopTick;

            IsVisibleChanged -= Ctr_HWindow_DP_IsVisibleChanged;
            Loaded -= Ctr_HWindow_DP_Loaded;

            DeDispROI();
            DataContext = null;

            BindingOperations.ClearAllBindings(this);

            // 置空各集合以触发 DP 回调退订 CollectionChanged 与项级事件，防止控件被集合锚定
            DispMessageDatas = null;
            DispRegionDatas = null;
            DispXLDDatas = null;
            SetCurrentValue(DispROIDatasProperty, null);
            DispMeasurePathDatas = null;

            _lastValidImage?.Dispose();
            _lastValidImage = null;

            if (HWindow1?.HalconWindow != null)
            {
                HOperatorSet.CloseWindow(HWindow1.HalconWindow);
            }
            HWindow1?.Dispose();
            HWindow1 = null;
        }
    }
}

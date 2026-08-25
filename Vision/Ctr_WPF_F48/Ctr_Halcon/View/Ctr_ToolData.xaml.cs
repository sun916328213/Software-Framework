using DPLibrary_Base_F48.Tools;
using DPLibrary_Halcon_F48.Tools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Ctr_WPF_F48.Ctr_Halcon.View
{
    /// <summary>
    /// Ctr_ToolData.xaml 的交互逻辑
    /// </summary>
    public partial class Ctr_ToolData : UserControl , INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public Ctr_ToolData()
        {
            InitializeComponent();
        }

        #region  依赖属性( 读码器实例 - HTool : Reader)

        /// <summary>
        /// 工具实例附加
        /// </summary>
        public static readonly DependencyProperty HToolProperty =
              DependencyProperty.Register(
                  name: "HTool", // 属性名称
                  propertyType: typeof(HVisionTool), // 属性类型
                  ownerType: typeof(Ctr_ToolData), // 所属控件类型
                  new FrameworkPropertyMetadata(
                      null, // 默认值
                      FrameworkPropertyMetadataOptions.None,
                      OnHToolChanged // 属性变化回调
                  )
              );

        /// <summary>
        /// 工具实例源
        /// </summary>
        public HVisionTool HTool
        {
            get => (HVisionTool)GetValue(HToolProperty);
            set => SetValue(HToolProperty, value);
        }

        /// <summary>
        /// 工具实例变化时的处理
        /// </summary>
        private static void OnHToolChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Ctr_ToolData control)
            {
                HVisionTool val = (HVisionTool)e.NewValue;
                // 先移除旧的事件订阅（如果有的话）
                if (e.OldValue is HVisionTool oldVal)
                {
                    oldVal.PropertyChanged -= control.OnHToolPropertyChanged;
                    if (oldVal.SearchRegions != null)
                        oldVal.SearchRegions.CollectionChanged -= control.OnSearchRegionsChanged;
                    if (oldVal.MeasurePaths != null)
                        oldVal.MeasurePaths.CollectionChanged -= control.OnXLDChanged;
                    oldVal.RequestShowView -= control.ShowView;
                }

                // 订阅新值的事件
                if (val != null)
                {
                    val.PropertyChanged += control.OnHToolPropertyChanged;
                    if (val.SearchRegions != null)
                        val.SearchRegions.CollectionChanged += control.OnSearchRegionsChanged;
                    if (val.MeasurePaths != null)
                        val.MeasurePaths.CollectionChanged += control.OnXLDChanged;
                    val.RequestShowView += control.ShowView;

                    // 立即触发一次更新以反映初始状态
                    control.OnPropertyChanged(nameof(ROI_Count));
                    control.OnPropertyChanged(nameof(XLD_Count));
                }
            }
        }

        private void OnHToolPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is HVisionTool reader)
            {
                switch (e.PropertyName)
                {
                    case nameof(HVisionTool.SearchRegions):
                        // 移除旧的监听再添加新的
                        if (reader.SearchRegions != null)
                        {
                            reader.SearchRegions.CollectionChanged -= OnSearchRegionsChanged;
                            reader.SearchRegions.CollectionChanged += OnSearchRegionsChanged;
                        }
                        break;
                    case nameof(HVisionTool.MeasurePaths):
                        // 移除旧的监听再添加新的
                        if (reader.MeasurePaths != null)
                        {
                            reader.MeasurePaths.CollectionChanged -= OnXLDChanged;
                            reader.MeasurePaths.CollectionChanged += OnXLDChanged;
                        }
                        break;
                }
            }
        }

        private void OnSearchRegionsChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(ROI_Count));
        }

        private void OnXLDChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(XLD_Count));
        }

        public bool ROI_Count
        {
            get
            {
                return HTool?.SearchRegions?.Count > 0;
            }
        }

        public bool XLD_Count
        {
            get
            {
                return HTool?.MeasurePaths?.Count > 0;
            }
        }

        private void ShowView(ITool tool)
        {
            switch (tool.ToolType)
            {
                case E_ToolType.HTool_BarCode:
                case E_ToolType.HTool_2DCode:
                case E_ToolType.HTool_TemplateMatch:
                case E_ToolType.HTool_TemplateControl_SHP:
                case E_ToolType.HTool_TemplateControl_NCC:
                    Ctr_HVisionTool_DP ctr = new Ctr_HVisionTool_DP();
                    ctr.HTool = (HVisionTool)tool;
                    ctr.ShowAsWindow();
                    ctr.Dispose();
                    break;
                case E_ToolType.HTool_NPoints:
                    Ctr_ToolOnlyData_DP ctr_only = new Ctr_ToolOnlyData_DP();
                    ctr_only.Tool = tool;
                    ctr_only.ShowAsWindow();
                    ctr_only.Dispose();
                    break;
                default:
                    break;
            }
        }
        #endregion

        #region  依赖属性( 是否显示操作列表 - bool : IsShowParam)

        /// <summary>
        /// 是否显示操作列表附加
        /// </summary>
        public static readonly DependencyProperty IsShowParamProperty =
              DependencyProperty.Register(
                  name: "IsShowParam", // 属性名称
                  propertyType: typeof(bool), // 属性类型
                  ownerType: typeof(Ctr_ToolData), // 所属控件类型
                  new FrameworkPropertyMetadata(
                      true, // 默认值
                      FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                      OnIsShowParamChanged // 属性变化回调
                  )
              );

        /// <summary>
        /// 是否显示操作列表源
        /// </summary>
        public bool IsShowParam
        {
            get => (bool)GetValue(IsShowParamProperty);
            set => SetValue(IsShowParamProperty, value);
        }

        /// <summary>
        /// 是否显示操作列表变化时的处理
        /// </summary>
        private static void OnIsShowParamChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Ctr_ToolData control)
            {
                bool val = (bool)e.NewValue;
                // 这里可以添加属性变化后的逻辑
            }
        }

        #endregion
        private void Run_Click(object sender, RoutedEventArgs e)
        {
            HTool?.Run();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (HTool != null)
            {
                ToolMge.Instance.Save(HTool);
            }
            else
            {
                MessageBox.Show("请先加载实例");
            }
        }
    }
}

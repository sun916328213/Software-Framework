// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：PrintInfo、PrintModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel、Extension.Core.Models、Extension.Imaging.Models、Prism.Ioc、System.Collections.Generic。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using Extension.Core.Models;
using Extension.Imaging.Models;
using Prism.Ioc;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace WPFControls.Controls.Models
{
    /// <summary>
    /// 打印信息
    /// </summary>
    public class PrintInfo
    {
        /// <summary>
        /// 标签路径
        /// </summary>
        public string LabelPath { get; set; }
        /// <summary>
        /// 标签名称
        /// </summary>
        public string LabelName { get; set; }
        /// <summary>
        /// 打印名称
        /// </summary>
        public string PrintName { get; set; }
        /// <summary>
        /// 标签字段信息
        /// </summary>
        public List<KeyValue> LabelFieldInfo { get; set; }
    }

    /// <summary>
    /// PrintModel 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// 继承或实现的契约：ObservableObject。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class PrintModel : ObservableObject
    {
        ObservableCollection<string> _printNames = new ObservableCollection<string>();

        /// <summary>
        /// 打印机名称集合
        /// </summary>
        public ObservableCollection<string> PrintNames
        {
            get => _printNames;
            set => SetProperty(ref _printNames, value);
        }


        private string _labelFormat;
        /// <summary>
        /// 标签路径
        /// </summary>
        public string LabelFormat
        {
            get => _labelFormat;
            set => SetProperty(ref _labelFormat, value);
        }

        private ImageSource labelImage;

        /// <summary>
        /// 标签图像
        /// </summary>
        public ImageSource LabelImage
        {
            get => labelImage;
            set => SetProperty(ref labelImage, value);
        }

        private ObservableCollection<KeyValue > _labelField = new ObservableCollection<KeyValue>();

        /// <summary>
        /// 标签Key
        /// </summary>
        public ObservableCollection<KeyValue> LabelField
        {
            get => _labelField;
            set => SetProperty(ref _labelField, value);
        }


        private PrintInfo _printInfo = new PrintInfo();
        /// <summary>
        /// 打印信息
        /// </summary>
        public PrintInfo PrintInfo
        {
            get => _printInfo;
            set => SetProperty(ref _printInfo, value);
        }
      
    }
}
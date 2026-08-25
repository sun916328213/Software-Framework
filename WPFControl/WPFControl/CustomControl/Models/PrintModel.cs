using CommunityToolkit.Mvvm.ComponentModel;
using Device.PrintSoftware;
using Extension.Struct;
using Prism.Ioc;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media;
using WPFControl.ProgramControl.Models;

namespace WPFControl.CustomControl.Models
{
    /// <summary>
    /// 打印信息
    /// </summary>
    public class PrintInfo
    {
        /// <summary>
        /// 标签路径
        /// </summary>
        internal string LabelPath { get; set; }
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
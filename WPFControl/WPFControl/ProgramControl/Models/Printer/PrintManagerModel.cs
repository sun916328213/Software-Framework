using CommunityToolkit.Mvvm.ComponentModel;
using Device.PrintSoftware;
using Extension.Core.ConvertEx;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WPFControl.CustomControl.Models;

namespace WPFControl.ProgramControl.Models
{
 

    /// <summary>
    /// 打印信息管理
    /// </summary>
    public class PrintInfoManager
    {
        //标签名称
       public string LabelName=> PrintInfo.LabelName;

        /// <summary>
        /// 标签字段信息
        /// </summary>
        public string LabelFieldInfo => PrintInfo.LabelFieldInfo.ToJson();

        /// <summary>
        /// 贴标对象类型
        /// </summary>
        internal string ObjectType { get; set; }
 
        /// <summary>
        /// 标签位置
        /// </summary>
        internal int LabelPos { get; set; }
        /// <summary>
        /// 打印信息
        /// </summary>
        internal PrintInfo PrintInfo { get; set; }
    }

    public class PrintManagerModel : ObservableObject
    {
        private PrintInfoManager selectItem = new PrintInfoManager();

        /// <summary>
        /// 选中的打印项目
        /// </summary>
        public PrintInfoManager SelectItem
        {
            get => selectItem;
            set => SetProperty(ref selectItem, value);
        }


        private ObservableCollection<PrintInfoManager> printCollection = new ObservableCollection<PrintInfoManager>();

        /// <summary>
        /// 打印列表集合
        /// </summary>
        public ObservableCollection<PrintInfoManager> PrintInfos
        {
            get => printCollection;
            set => SetProperty(ref printCollection, value);
        }

        private int selectPrintIndex;

        /// <summary>
        /// 选中的打印Index
        /// </summary>
        public int SelectPrintIndex
        {
            get => selectPrintIndex;
            set => SetProperty(ref selectPrintIndex, value);
        }
    }
}

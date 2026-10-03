// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：PrintInfoManager、PrintManagerModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel、Extension.Core.Conversion、System.Collections.Generic、System.Collections.ObjectModel、System.Linq。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using Extension.Core.Conversion;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WPFControls.Controls.Models;

namespace WPFControls.ProgramControl.Models
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

    /// <summary>
    /// PrintManagerModel 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// 继承或实现的契约：ObservableObject。具体约束按这些契约及各成员实现解释。
    /// </summary>
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

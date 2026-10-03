// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：ISQLDataBase、DataQueryModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel、Mapster、System、System.Collections.ObjectModel、System.ComponentModel.DataAnnotations。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using Mapster;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WPFControls.ProgramControl.Models
{
    /// <summary>
    /// SQL数据接口
    /// </summary>
    public interface ISQLDataBase
    {
        public string ID { get; set; }
 
        public DateTime Time { get; set; }
    }

    /// <summary>
    /// DataQueryModel 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// 继承或实现的契约：ObservableObject。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class DataQueryModel<T,TDTO>:ObservableObject where T: ISQLDataBase
    {
        ObservableCollection<T> iDatas = new ObservableCollection<T>();
        /// <summary>
        /// SQL数据集合
        /// </summary>
        internal ObservableCollection<T> Datas
        {
            get => iDatas;
            set
            {
                SetProperty(ref iDatas, value);
                if (iDatas != null && iDatas.Count > 0)
                    DatasDTO = iDatas.Adapt<ObservableCollection<TDTO>>();
                else
                    DatasDTO = null;
            }
        }


        ObservableCollection<TDTO> iDatasDTO=new ObservableCollection<TDTO>();
        /// <summary>
        /// SQL数据集合 用于显示
        /// </summary>
        public ObservableCollection<TDTO> DatasDTO
        {
            get => iDatasDTO;
            set => SetProperty(ref iDatasDTO, value);
        }


        DateTime dateTime_Start = DateTime.Now, dateTime_End = DateTime.Now;
        /// <summary>
        /// 查询开始时间
        /// </summary>
        public DateTime DateTime_Start
        {
            get => dateTime_Start;
            set => SetProperty(ref dateTime_Start, value);
        }

        /// <summary>
        /// 查询结束时间
        /// </summary>
        public DateTime DateTime_End
        {
            get => dateTime_End;
            set => SetProperty(ref dateTime_End, value);
        }

        string iID;
        /// <summary>
        /// 筛选ID
        /// </summary>
        public string ID
        {
            get => iID;
            set => SetProperty(ref iID, value);
        }
    }
}

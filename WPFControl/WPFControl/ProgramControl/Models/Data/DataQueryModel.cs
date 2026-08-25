using CommunityToolkit.Mvvm.ComponentModel;
using Mapster;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

namespace WPFControl.ProgramControl.Models
{
    /// <summary>
    /// SQL数据接口
    /// </summary>
    public interface ISQLDataBase
    {
        public string ID { get; set; }

        public DateTime Time { get; set; }
    }

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

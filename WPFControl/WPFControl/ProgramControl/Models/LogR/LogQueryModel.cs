using CommunityToolkit.Mvvm.ComponentModel;
using Extension.Struct;
using Extension.Core.EnumEx;
using Mapster;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WPFControl.ProgramControl.Models
{
    /// <summary>
    /// 日志查询数据模型
    /// 注意：LogQueryModel不应该继承SLogInfo，因为它们是不同层次的概念：
    /// 1. SLogInfo是结构体，代表单条日志信息
    /// 2. LogQueryModel是类，代表日志集合的容器
    /// 3. 它们之间是组合关系(has-a)而非继承关系(is-a)
    /// </summary>
    internal class LogQueryModel:ObservableObject
    {
        /// <summary>
        /// Log数据集合
        /// </summary>
        internal List<LogInfo> LogInfos { get; set; } = new List<LogInfo>();

        ObservableCollection<LogInfo> logDatas = new ObservableCollection<LogInfo>();
        /// <summary>
        /// Log显示数据集合
        /// </summary>
        public ObservableCollection<LogInfo> LogDatas
        {
            get => logDatas;
            set
            {
                SetProperty(ref logDatas, value);
                LogInfoDTO = logDatas.Adapt<ObservableCollection<LogInfoDTO>>();
            }
        }
 
         ObservableCollection<LogInfoDTO> iLogInfoDTO= new ObservableCollection<LogInfoDTO>();
        /// <summary>
        /// Log显示数据集合 已转换格式
        /// </summary>
        public ObservableCollection<LogInfoDTO> LogInfoDTO
        {
            get => iLogInfoDTO;
            set => SetProperty(ref iLogInfoDTO, value);
        }

        DateTime dateTime_Start= DateTime.Now, dateTime_End= DateTime.Now;
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

        /// <summary>
        /// Select选择集合
        /// </summary>
        public IEnumerable SelectItemSource => EnumEx.GetName<ELogLevel>();


        ELogLevel selectItem;
        /// <summary>
        /// Select选择项
        /// </summary>
        public ELogLevel SelectItem
        {
            get => selectItem;
            set => SetProperty(ref selectItem, value);
        }
    }
}

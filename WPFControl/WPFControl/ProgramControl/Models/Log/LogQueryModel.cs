using CommunityToolkit.Mvvm.ComponentModel;
using DataManager.Log.Definition;
using Extension.Core.EnumEx;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WPFControl.ProgramControl.Models
{
    /// <summary>
    /// 日志查询数据模型
    /// 注意：LogQueryModel不应该继承LogInfo，因为它们是不同层次的概念：
    /// 1. LogInfo是结构体，代表单条日志信息
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
        /// <remarks>
        /// 2026-09-16：DataGrid 直接绑定本集合，不再经 LogInfoDTO 转换。
        /// 原 DTO 仅用于展示，而筛选/统计/导出全都基于 LogInfo，
        /// 且自动生成列对 ELogLevel 走 ToString()，显示结果与原 DTO 一致。
        /// </remarks>
        public ObservableCollection<LogInfo> LogDatas
        {
            get => logDatas;
            set => SetProperty(ref logDatas, value);
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


        ELogLevel? selectItem;
        /// <summary>
        /// Select选择项
        /// </summary>
        public ELogLevel? SelectItem
        {
            get => selectItem;
            set => SetProperty(ref selectItem, value);
        }

        int currentPage = 1;
        /// <summary>
        /// 当前页
        /// </summary>
        public int CurrentPage
        {
            get => currentPage;
            set => SetProperty(ref currentPage, value);
        }

        int totalCount;
        /// <summary>
        /// 总记录数
        /// </summary>
        public int TotalCount
        {
            get => totalCount;
            set => SetProperty(ref totalCount, value);
        }

        /// <summary>
        /// 每页条数
        /// </summary>
        public int PageSize { get; set; } = 20;

        string filterItem;
        /// <summary>
        /// 筛查字段(对应DataGrid表头)
        /// </summary>
        public string FilterItem
        {
            get => filterItem;
            set => SetProperty(ref filterItem, value);
        }

        string filterText;
        /// <summary>
        /// 筛查值
        /// </summary>
        public string FilterText
        {
            get => filterText;
            set => SetProperty(ref filterText, value);
        }
    }
}

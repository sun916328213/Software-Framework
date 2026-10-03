// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：CSQLDataDTO。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Mapster、MachineApp.Business.Station、System、System.ComponentModel.DataAnnotations.Schema、WPFControls.ProgramControl.Models、Extension.Core.Conversion。导入不代表所有类型都会在运行时加载。
using Mapster;
using MachineApp.Business.Station;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using WPFControls.ProgramControl.Models;
using Extension.Core.Conversion;
using System.ComponentModel.DataAnnotations;

using MachineApp.Infra.Persistence.Entities;
namespace MachineApp.UI.Model
{
    /// <summary>
    /// CSQLDataDTO 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// </summary>
    public class CSQLDataDTO 
    {
        public string ID { get; set; }

        public DateTime Time { get; set; }
        /// <summary>
        /// RFID数据
        /// </summary>
        public string RFID { get; set; }
        /// <summary>
        /// FOSBID
        /// </summary>
        public string FOSBID { get; set; }

       /// <summary>
       /// 干燥剂数量
       /// </summary>
        public int DesiccantNum { get; set; }
        /// <summary>
        /// 湿度卡数量
        /// </summary>
        public int HumidityCardNum { get; set; }

        /// <summary>
        /// 标签信息
        /// </summary>
        public string  LableInfo { get; set; }
        /// <summary>
        /// 标签复检信息
        /// </summary>
        public string LableCheckInfo { get; set; }
 
       /// <summary>
       /// 热封结果
       /// </summary>
        public string HeatSealResult { get; set; }
        /// <summary>
        /// 热封NG索引
        /// </summary>
        public string HeatSealIndex { get; set; }  
    
        /// <summary>
        /// AL宽度
        /// </summary>
        public string ALWidth { get; set; }
        /// <summary>
        /// AL角度
        /// </summary>
        public string ALAngle { get; set; }

 
        /// <summary>
        /// 将CSQLData转换为CSQLDataDTO
        /// </summary>
        /// <param name="data">源数据</param>
        /// <returns>DTO对象</returns>
        public static CSQLDataDTO FromCSQLData(CSQLData data)
        {
            if (data == null) return null;
            return data.Adapt<CSQLDataDTO>();
        }
    }

}

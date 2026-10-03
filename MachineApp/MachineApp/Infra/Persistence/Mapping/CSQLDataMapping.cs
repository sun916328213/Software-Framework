// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：CSQLDataMapping。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Mapster、MachineApp.Business.Station、System、System.ComponentModel.DataAnnotations.Schema、WPFControls.ProgramControl.Models、Extension.Core.Conversion。导入不代表所有类型都会在运行时加载。
using Mapster;
using MachineApp.Business.Station;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using WPFControls.ProgramControl.Models;
using Extension.Core.Conversion;
using System.ComponentModel.DataAnnotations;

using MachineApp.Infra.Persistence.Entities;
using MachineApp.UI.Model;
namespace MachineApp.Infra.Persistence.Mapping
{
    /// <summary>
    /// CSQLData相关映射配置
    /// </summary>
    public static class CSQLDataMapping
    {
        /// <summary>
        /// 配置CSQLData和CSQLDataDTO之间的映射关系
        /// </summary>
        /// <param name="config">映射配置对象</param>
        public static void Configure(TypeAdapterConfig config)
        {
            // 配置从CSQLData到CSQLDataDTO的映射
            config.ForType<CSQLData, CSQLDataDTO>()

                   // ReadID映射
                .Map(dest => dest.RFID, src => src.StationInfo.Load.RFIDInfo)
                .Map(dest => dest.FOSBID, src => src.StationInfo.ReadID.FOSBID)
                .Map(dest => dest.DesiccantNum, src => src.StationInfo.Accessory.Num_Desiccant)
                .Map(dest => dest.HumidityCardNum, src => src.StationInfo.Accessory.Num_HumidityIndicator)

                .Map(dest => dest.LableInfo, src => string.Join(",", src.StationInfo.Label.LabelInfos))
                .Map(dest => dest.LableCheckInfo, src => string.Join(",", src.StationInfo.Label.LabelInfo_Check))
                // HeatSealInfo映射
                .Map(dest => dest.HeatSealResult, src => src.StationInfo.HeatSeal.CheckResult.ToString())
                .Map(dest => dest.HeatSealIndex , src =>string.Join("," ,src.StationInfo.HeatSeal.Index))

                .Map(dest => dest.ALWidth, src => src.StationInfo.ALDetect.Width.ToString())
                .Map(dest => dest.ALAngle, src => src.StationInfo.ALDetect.Angle.ToString())
                     ;
        }
    }
}

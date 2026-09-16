using Mapster;
using Packing_Machine.Business.Station;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using WPFControl.ProgramControl.Models;
using Extension.Core.ConvertEx;
using System.ComponentModel.DataAnnotations;

namespace Packing_Machine.UI.Model
{
    public class CStation
    {
        public LoadInfo Load { get; set; } = new LoadInfo();
        public FOSBInfo ReadID { get; set; } = new FOSBInfo();
        public AccessoryInfo Accessory { get; set; } = new AccessoryInfo();
        public LabelInfo Label { get; set; } = new LabelInfo();
        public HeatSealInfo HeatSeal { get; set; } = new HeatSealInfo();
        public ALInfo ALDetect { get; set; } = new ALInfo();
        
    }

    public class CSQLData : ISQLDataBase
    {
        public string ID { get; set; }
        [Key]
        public DateTime Time { get; set; }

        [NotMapped]
        public CStation StationInfo { get; set; } = new CStation();

        public string Station_Json
        {
            get => StationInfo.ToJson();
            set => StationInfo = value.FromJson<CStation>();
        }
       
       
    }

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

// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：CStation、CSQLData。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Mapster、MachineApp.Business.Station、System、System.ComponentModel.DataAnnotations.Schema、WPFControls.ProgramControl.Models、Extension.Core.Conversion。导入不代表所有类型都会在运行时加载。
using Mapster;
using MachineApp.Business.Station;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using WPFControls.ProgramControl.Models;
using Extension.Core.Conversion;
using System.ComponentModel.DataAnnotations;

namespace MachineApp.Infra.Persistence.Entities
{
    /// <summary>
    /// CStation 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
    /// </summary>
    public class CStation
    {
        public LoadInfo Load { get; set; } = new LoadInfo();
        public FOSBInfo ReadID { get; set; } = new FOSBInfo();
        public AccessoryInfo Accessory { get; set; } = new AccessoryInfo();
        public LabelInfo Label { get; set; } = new LabelInfo();
        public HeatSealInfo HeatSeal { get; set; } = new HeatSealInfo();
        public ALInfo ALDetect { get; set; } = new ALInfo();
        
    }

    /// <summary>
    /// CSQLData 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
    /// 继承或实现的契约：ISQLDataBase。具体约束按这些契约及各成员实现解释。
    /// </summary>
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

}

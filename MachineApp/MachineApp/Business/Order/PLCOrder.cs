// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：PLCSystem、PLCSensor、PLCProcess。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.Linq、System.Text、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MachineApp.Business.Order
{
 
    /// <summary>
    /// PLCSystem 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// </summary>
    public static class PLCSystem
    {
        public const string PLC_SysTrigger  = "PLC_SysTrigger";
 
        public const string PLC_DeviceStatus = "PLC_DeviceStatus";
        public const string PLC_Mode = "PLC_Mode";
      
 
        public const string PC_SysTrigger = "PC_SysTrigger";

        public const string PC_UserName = "PC_UserName";
        public const string PC_Level = "PC_Level";
        public const string PC_Alarm = "PC_Alarm";
      
        public const string Online = "Online";

        public const string Count = "Count";
    }
 
    /// <summary>
    /// PLCSensor 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// </summary>
    internal static class PLCSensor
    {
        /// <summary>
        /// PC下发配方选择触发
        /// </summary>
        public const string RecipeSelect_Trigger = "RecipeSelect_Trigger";
        public const string RecipeSelect_ID = "RecipeSelect_ID";

        // <summary>
        /// PLC修改配方触发
        /// </summary>
        public const string RecipeModify_Trigger = "RecipeModify_Trigger";

        /// <summary>
        /// 机械手中心相机触发  1=读取ID 2=九点标定 3=湿度卡相机基准 4=湿度卡传感器位置示教 5=湿度卡检测 6=标签贴前检测 7=标签贴后检测
        /// </summary>
        public const string RobotCenterCam_Trigger = "RobotCenterCam_Trigger";
        public const string RobotX = "Caliber_X";
        public const string RobotY = "Caliber_Y";
        public const string RobotR = "Caliber_R";

        /// <summary>
        /// 湿度卡偏移
        /// </summary>
        public const string HIX = "HIX_10%";
        public const string HIY = "HIY_10%";

        public const string FOSB_Write = "FOSB_Write";

        /// <summary>
        /// 机械手侧边相机触发 1=进站存档 2=出站存档
        /// </summary>
        public const string RobotSideCam_Trigger = "RobotSideCam_Trigger";
 
        /// <summary>
        /// 读取Load触发 1=上料请求 2=读取RFID
        /// </summary>
        public const string  Load_Trigger = "ReadRFID_Trigger";

        /// <summary>
        /// 打印机触发
        /// </summary>
        public const string Print_Trigger = "Print_Trigger";
 

        /// <summary>
        /// AL袋定位触发  
        /// </summary>
        public const string AL_Trigger = "AL_Trigger";
        public const string AL_CalibWidth = "AL_CalibWidth";
        public const string AL_Width = "AL_Width";
 
        /// <summary>
        /// 干燥剂复拍触发  
        /// </summary>
        public const string Accessory_Trigger = "Accessory_Trigger";
 
        /// <summary>
        /// 封口检测触发
        /// </summary>
        public const string Heat_Trigger = "Heat_Trigger";
    }


    /// <summary>
    /// PLCProcess 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// </summary>
    internal static class PLCProcess
    {
        /// <summary>
        /// 进料工站流程 1=开始 2=结束
        /// </summary>
        public const string LoadProcess_Trigger = "LoadProcess_Trigger";

        /// <summary>
        /// 干燥剂安装流程
        /// </summary>
        public const string AccessoryProcess_Trigger = "AccessoryProcess_Trigger";
 
        /// <summary>
        /// FOSB标签流程
        /// </summary>
        public const string FOSBLabelProcess_Trigger = "FOSBLabelProcess_Trigger";
 
        /// <summary>
        /// 包装流程 
        /// </summary>
        public const string PackagingProcess_Trigger = "FOSBBagInProcess_Trigger";
 
        /// <summary>
        /// AL袋标签流程
        /// </summary>
        public const string ALLabelProcess_Trigger = "ALLabelProcess_Trigger";
 
        /// <summary>
        /// 抽真空流程
        /// </summary>
        public const string EVAProcess_Trigger = "EVAProcess_Trigger";
        public const string EVAProcess_Pressure = "EVAProcess_Pressure";
        public const string EVAProcess_Traffic = "EVAProcess_Traffic";
        public const string EVAProcess_Time = "EVAProcess_Time";
 
        /// <summary>
        /// 冲氮气流程
        /// </summary>
        public const string NitProcess_Trigger = "NitProcess_Trigger";
        public const string NitProcess_Pressure = "NitProcess_Pressure";
        public const string NitProcess_Traffic = "NitProcess_Traffic";
        public const string NitProcess_Time = "NitProcess_Time";

        /// <summary>
        /// 热封流程
        /// </summary>
        public const string HeatProcess_Trigger = "HeatProcess_Trigger";
        public const string HeatProcess_Temp = "HeatProcess_Temp";
        public const string HeatProcess_Time = "HeatProcess_Time";
 
        /// <summary>
        /// 出料工站流程 3=取走
        /// </summary>
        public const string UnloadProcess_Trigger = "UnloadProcess_Trigger";
 
        /// <summary>
        /// 配方流程 PC-》PLC
        /// </summary>
        public const string RecipeProcess_Trigger = "RecipeProcess_Trigger";
 
        /// <summary>
        /// 配方更新流程 PLC->PC
        /// </summary>
        public const string RecipeUpdateProcess_Trigger = "RecipeUpdateProcess_Trigger";

    }
 
 
}

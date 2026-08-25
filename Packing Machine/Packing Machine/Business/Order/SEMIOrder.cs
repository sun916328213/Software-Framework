using Packing_Machine.UI.View;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Packing_Machine.Business.Order
{
    public static class EventOrder
    {
        public static string Device_Status = "Device Status";
        public static string Device_Mode = "Device Mode";
        public static string Device_Online = "Device Online";
        public static string Device_Offline = "Device Offline";

        public static string Process_Start { get; set; } = "Process Start";
        public static string Process_End { get; set; } = "Process End";
        public static string ProcessAbortComplete { get; set; } = "ProcessAbortComplete";

        public static string Load_Request { get; set; } = "Load Request";
        public static string Load_ReadID_Success { get; set; } = "Load ReadID Success";
        public static string Load_ReadID_Fail { get; set; } = "Load ReadID Fail";
        public static string Load_Check_Success { get; set; } = "Load Check Success";
        public static string Load_Check_Fail { get; set; } = "Load Check Fail";

        public static string Recipe_Request { get; set; } = "Recipe Request";
        public static string Recipe_Select_Success { get; set; } = "Recipe Select Success";
        public static string Recipe_Select_Fail { get; set; } = "Recipe Select Fail";
        public static string Recipe_Update { get; set; } = "Recipe Update";

        public static string ReadFOSB_Star { get; set; } = "ReadFOSB Star";
        public static string ReadFOSB_Success { get; set; } = "ReadFOSB Success";
        public static string ReadFOSB_Fail { get; set; } = "ReadFOSB Fail";
        public static string ReadFOSB_End { get; set; } = "ReadFOSB End";
 
        public static string PlaceDesiccant_Start { get; set; } = "PlaceDesiccant Start";
        public static string PlaceDesiccant_End { get; set; } = "PlaceDesiccant End";
        public static string Desiccant_DetectSuccess { get; set; } = "Desiccant Detect Success";
        public static string Desiccant_DetectFail { get; set; } = "Desiccant Detect Fail";

        public static string HeatSeal_Start { get; set; } = "HeatSeal Start";
        public static string HeatSeal_Info { get; set; } = "HeatSeal Info";
        public static string HeatSeal_Vision_Success { get; set; } = "HeatSeal Vision Success";
        public static string HeatSeal_Vision_Fail { get; set; } = "HeatSeal Vision Fail";
        public static string HeatSeal_End { get; set; } = "HeatSeal End";

        public static string LabelProcess_Satrt { get; set; }  = "LabelProcess Satrt";
        public static string Label_Print { get; set; }  = "Label Print";
        public static string LabelInfo { get; set; } = "LabelInfo";
        public static string LabelInfo_Check_Success { get; set; }  = "LabelInfo Check Success";
        public static string LabelInfo_Check_Fail { get; set; } = "LabelInfo Check Fail";
        public static string Label_Fitting_Success { get; set; } = "Label Fitting Success";
        public static string Label_Fitting_Fail { get; set; } = "Label Fitting Fail";
        public static string LabelProcess_End { get; set; } = "LabelProcess End";

        public static string UnLoad_Request { get; set; } = "UnLoad Request";
        public static string UnLoad_Complete { get; set; } = "UnLoad Complete";
         
    }

    internal static class S2F42Order
    {
        public static string FOSBID_Check = "FOSBID Check";
        public static string RecipeInfo = "RecipeInfo";
        public static string LabelInfo_Check = "LabelInfo Check";


      
        public static string FOSBID = "FOSBID";
        public static string Check_Result = "Check Result";
        public static string Recipe_ID = "Recipe ID";
    }

    public static class VIDOrder
    {
        public static string DeviceStatus = "DeviceStatus";
        public static string DeviceMode = "DeviceMode";

        public static string FOSBID = "FOSBID";
        public static string FOSBType = "FOSBType";

        public static string RecipeID = "RecipeID";
        public static string RecipeName = "RecipeName";

        
        public static string LabelType = "LabelType";
        public static string LabelPosition = "LabelPosition";
        public static string LabelInfos = "LabelInfos";

        public static string LoadPortID = "LoadPortID";
        public static string UnLoadPortID = "UnLoadPortID";
 
        public static string HeatSealTemp = "HeatSealTemp";
        public static string HeatSealTime = "HeatSealTime";

        public static string VacuumFlowRate = "VacuumFlowRate";
        public static string VacuumPressure = "VacuumPressure";
        public static string VacuumTime = "VacuumTime";

        public static string NitrogenFlowRate = "NitrogenFlushingRate";
        public static string NitrogenPressure = "NitrogenFlushingPressure";
        public static string NitrogenTime = "NitrogenFlushingTime";


        public static string HeatSealFOSBType = "HeatSealFOSBType";
        public static string VisionResult = "VisionResult";

        public static string DesiccantNum = "DesiccantNum";
        public static string HumidityIndicatorNum = "HumidityIndicatorNum";
    }
}

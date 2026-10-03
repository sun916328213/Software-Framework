// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：PCAlarm。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.Linq、System.Text、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MachineApp.Data
{
    /// <summary>
    /// PCAlarm 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// </summary>
    public static  class PCAlarm
    {
        public const short Tool_Error = 1;
        public const short Control_Error = 2;


        public const short Cam_Error = 3;
        public const short LightControl_Error = 4;
        public const short Scan_Error = 5;
        public const short RFID_Error = 6;

        public const short Community_Error = 7;
        public const short Secs_Error = 8;

    }
}

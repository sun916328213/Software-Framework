// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：WaferInspection。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls.Nodes、FlowEditor.Core.Nodes、System、System.Collections.Generic、System.Linq、System.Text。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MachineApp.Infra.VisionCore
{
    /// <summary>
    /// WaferInspection 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
    /// </summary>
    internal class WaferInspection  
    {
        /// <summary>
        /// 初始化 WaferInspection 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public WaferInspection()
        { 
            //Blob检测

            //获取点位信息
            
            //对点位信息进行处理 计算间隔方差和角度方差

            //对结果进行判断

            //输出结果
        }

    }

    
}

// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：AccessoryInfo、Accessory。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Extension.Core.Buffers、Extension.IO.Compression、Extension.Integration.Cloning、Extension.UI.Controls、DataManager.Core.Log.Definition、MachineApp.Data。导入不代表所有类型都会在运行时加载。
using Extension.Core.Buffers;
using Extension.IO.Compression;
using Extension.Integration.Cloning;
using Extension.UI.Controls;
using DataManager.Core.Log.Definition;
using MachineApp.Data;
using MachineApp.Business.Flow;
using MachineApp.Infra.Tool;
using MachineApp.Business.Order;

using System;
using System.Threading.Tasks;

namespace MachineApp.Business.Station
{
    /// <summary>
    /// AccessoryInfo 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// 继承或实现的契约：Result。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class AccessoryInfo : Result
    {
        /// <summary>
        /// 干燥剂数量
        /// </summary>
        public int Num_Desiccant { get; set; }
        /// <summary>
        /// 湿度卡数量
        /// </summary>
        public int Num_HumidityIndicator { get; set; }
    }

    /// <summary>
    /// Accessory 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// 继承或实现的契约：StationBase&lt;AccessoryInfo&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class Accessory :StationBase<AccessoryInfo>
    {

        internal string StationName = EProcess.配件流程.ToString();

        AccessoryInfo curInfo = new AccessoryInfo();

        /// <summary>
        /// 初始化 Accessory 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public Accessory(  StationServices services) : base(services)
        {
        }

 
        /// <summary>
        /// Sensor触发
        /// </summary>
        public async Task Trigger( string pLCOrder)
        {
            string Step = null, ID = null;

            try
            {
                ///干燥剂复检
                _PLCMethod.TriggerReply(StationName, pLCOrder);
                Step = _PLCMethod.ReadData(StationName, pLCOrder, PLCAddressOffest.Function);
                ID = _PLCMethod.ReadData(StationName, pLCOrder, PLCAddressOffest.ID);

               await Recheck(ID);
            }
            catch(Exception ex)
            {
                curInfo.CheckResult = EPLCResult.ERROR;
                string message = string.Join("\r\n", ex.Message, ex.StackTrace);
                _LogInfoService.AddRunInfo(StationName + ":" + message, ELogLevel.SysError);
            }
            finally
            {
                //通知PLC结果
                _PLCMethod.SendResult(StationName, pLCOrder, curInfo.CheckResult);

                AddUpdate(ID,curInfo.DeepClone());
                curInfo = new AccessoryInfo();//复位
            }
        }

        /// <summary>
        /// 配件复检
        /// </summary>
        /// <param name="LotId"></param>
        private async Task  Recheck(string LotId)
        {

            Func<Task> action = new Func<Task>(async() =>
            {
               curInfo= AccessoryDetect(LotId);
            
            });
            Func<Task> action1 = new Func<Task>(async () =>
            {
                curInfo = AccessoryDetect(LotId);
              
                _SecsMethod.Update(VIDOrder.FOSBID, LotId);                                                  //刷新VID
                _SecsMethod.Update(VIDOrder.DesiccantNum, curInfo.Num_Desiccant.ToString());                 //刷新VID   
                _SecsMethod.Update(VIDOrder.HumidityIndicatorNum, curInfo.Num_HumidityIndicator.ToString()); //刷新VID   

                if (curInfo.CheckResult == EPLCResult.OK)
                {
                    //将数据写入EAP
                    await _SecsMethod.SendS6F11(EventOrder.Desiccant_DetectSuccess);
                }
                else
                {
                    await _SecsMethod.SendS6F11(EventOrder.Desiccant_DetectFail);
                }
            });
            await _SecsMethod.Run(action, action1, action1);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 AccessoryDetect。
        /// 正文中的关键协作调用包括 AddRunInfo、GetMessage、GrabImage、DetectAccessory；具体条件和先后次序见方法体。
        /// </summary>
        private AccessoryInfo AccessoryDetect(string id)
        {
            _LogInfoService.AddRunInfo(StationName + Message.开始配件检测.GetMessage());
            AccessoryInfo info=new AccessoryInfo();
            var d1 = _VisionMethod.GrabImage(id,StationName, GlobalData.VisionItem_Accessory);
 
            // 执行视觉检程序
            var d2 = _VisionMethod.DetectAccessory(d1, id);
 
            info.Num_HumidityIndicator = d2.Num_HumidityIndicator;
            info.Num_Desiccant = d2.Num_Desiccant;

            //对比
            if (info.Num_HumidityIndicator == GlobalData.Num_HumidityIndicator && info.Num_Desiccant == GlobalData.Num_Desiccant)
            {
                info.CheckResult = EPLCResult.OK;
                _LogInfoService.AddRunInfo(StationName + Message.配件检测成功.GetMessage());
            }
            else
            {
                info.CheckResult = EPLCResult.NG;
                _LogInfoService.AddRunInfo(StationName + Message.配件检测失败.GetMessage(), ELogLevel.Error);
            }

            return info;
        }

      

        /// <summary>
        /// 从PLC获取湿度卡检测结果 上报EAP 暂时不用
        /// </summary>
        /// <param name="ID"></param>
        private void HIDetect(string ID)
        {
          
        }
    }
}

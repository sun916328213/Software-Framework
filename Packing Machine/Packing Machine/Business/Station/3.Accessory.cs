using Extension.Core.Buffers;
using Extension.IO.Compression;
using Extension.Integration.Cloning;
using Extension.UI.Controls;
using DataManager.Log.Definition;
using Packing_Machine.Data;
using Packing_Machine.Business.Flow;
using Packing_Machine.Infra.Tool;
using Packing_Machine.Business.Order;
using Prism.Ioc;
using System;
using System.Threading.Tasks;

namespace Packing_Machine.Business.Station
{
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

    public class Accessory :StationBase<AccessoryInfo>
    {

        internal string StationName = EProcess.配件流程.ToString();

        AccessoryInfo curInfo = new AccessoryInfo();

        public Accessory(  IContainerProvider container) : base(container)
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

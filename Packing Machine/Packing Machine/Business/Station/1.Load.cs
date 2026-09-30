using Extension.Core.Conversion;
using Extension.Core.Buffers;
using Extension.IO.Compression;
using Extension.Integration.Cloning;
using Extension.UI.Controls;
using Extension.Core.Models;
using Extension.Imaging.Models;
using Extension.Automation.Device;
using Extension.Automation.Secs;
using DataManager.Log.Definition;
using Packing_Machine.Data;
using Packing_Machine.Business.Flow;
using Packing_Machine.Infra.Tool;
using Packing_Machine.Business.Order;
using Prism.Ioc;
using Secs;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Packing_Machine.Business.Station
{
    public class LoadInfo:Result
    {
        /// <summary>
        /// RFID数据
        /// </summary>
        public string  RFIDInfo  { get; set; } 
    }

    public enum ELoad
    {
        上料请求,
        读取RFID,
    }

    public class Load : StationBase<LoadInfo>
    {
        /// <summary>
        /// 工站名称
        /// </summary>
        private string stationName = EProcess.进站流程.ToString();
        internal string StationName = " ";

        private Recipe _Recipe;

        public Load(IContainerProvider container) : base(container)
        {
            _Recipe=container.Resolve<Recipe>();
        }
        LoadInfo curInfo = new LoadInfo();

        /// <summary>
        /// 事件触发
        /// </summary>
        public async void Trigger(ELoad Step,string pLCOrder)
        {
            try
            {
                StationName = stationName + "_" + Step.ToString() + ": ";
                _PLCMethod.TriggerReply(StationName, pLCOrder);
                if (Step == ELoad.上料请求)
                {
                   var d1=await Load_Request();
                    curInfo.CheckResult=d1==true? EPLCResult.OK: EPLCResult.ERROR; 
                }
                else if (Step == ELoad.读取RFID)
                {
                    await ReadRFIDProcess();
                }
            }
            catch (Exception ex)
            {
                curInfo.CheckResult = EPLCResult.ERROR;
                string message = string.Join("\r\n", ex.Message, ex.StackTrace);
                _LogInfoService.AddRunInfo(StationName + ":" + message, ELogLevel.SysError);
            }
            finally
            {
                //通知PLC结果
                _PLCMethod.SendData(StationName, pLCOrder, PLCAddressOffest.ID, curInfo.RFIDInfo);
                _PLCMethod.SendResult(StationName, pLCOrder, curInfo.CheckResult);

                //将结果添加到集合中
                Infos.TryAdd(curInfo.RFIDInfo, curInfo.DeepClone());
                curInfo = new LoadInfo(); //清除
            }
        }

        private async Task<bool> Load_Request()
        {
            if (_SecsMethod.CurrentMode == ESecsMode.Offline)
                return false;

            _SecsMethod.Update(VIDOrder.LoadPortID, 1);//更新LoadPortID
            await _SecsMethod.SendS6F11(EventOrder.Load_Request); //上料请求
            return true;
        }


        /// <summary>
        /// 读取RFID流程
        /// </summary>
        private async Task ReadRFIDProcess()
        {
            Func<Task> action = new Func<Task>( async()  =>
            { 
                     curInfo = ReadRFID();
             });
            Func<Task > action1 = new Func<Task>(async () =>
            {
                //读取RFID
                curInfo = ReadRFID();

                //读取RFID成功
                if (curInfo.CheckResult == EPLCResult.OK)
                {
                    _SecsMethod.Update(VIDOrder.FOSBID, curInfo.RFIDInfo.ToString());  //更新VID数据
                    await _SecsMethod.SendS6F11(EventOrder.Load_ReadID_Success);  //将RFID结果发送给EAP

                    //等待S2F41指令
                    var d3 = await _SecsMethod.GetS2F41(curInfo.RFIDInfo, S2F42Order.FOSBID_Check);
                    if (!d3)
                    {
                        curInfo.CheckResult = EPLCResult.NG;
                        await _SecsMethod.SendS6F11(EventOrder.Load_Check_Fail);
                        _LogInfoService.AddRunInfo(StationName + Message.EAP回复校验失败.GetMessage(), ELogLevel.Error);
                    }
                    else
                    {
                        curInfo.CheckResult = EPLCResult.OK;
                        await _SecsMethod.SendS6F11(EventOrder.Load_Check_Success);
                        _LogInfoService.AddRunInfo(StationName + Message.EAP回复校验成功.GetMessage());

                        _LogInfoService.AddRunInfo(StationName + Message.FOSB上料成功.GetMessage());
                      
                        _Recipe.RecipeSwitch(curInfo.RFIDInfo); ///配方切换
                    }
                }
                //读取RFID失败
                else
                {
                    curInfo.CheckResult = EPLCResult.NG;
                    await _SecsMethod.SendS6F11(EventOrder.Load_ReadID_Fail);
                }
            });
            await _SecsMethod.Run(action, action1, action1);
        }

        /// <summary>
        /// 读取RFID
        /// </summary>
        /// <returns></returns>
        private LoadInfo ReadRFID()
        {
            _LogInfoService.AddRunInfo(StationName + Message.RFID开始读取.GetMessage());
            LoadInfo loadInfo = new LoadInfo();
            var d11 = _RFIDManager[0].Read(1, 2).RemoveControl();
           
            //读取RFID成功
            if (d11 != null&&d11.Length>1)
            {
                loadInfo.RFIDInfo = d11;
                loadInfo.CheckResult = EPLCResult.OK;
                _LogInfoService.AddRunInfo(StationName + Message.RFID读取成功.GetMessage() + ":" + loadInfo.RFIDInfo);
            }
            else
            {
                loadInfo.RFIDInfo = d11;
                loadInfo.CheckResult = EPLCResult.NG;
                _LogInfoService.AddRunInfo(StationName + Message.RFID读取失败.GetMessage(), ELogLevel.Error);
            }
            return loadInfo;
        }

   
    }
}

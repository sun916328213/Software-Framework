// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：LoadInfo、ELoad、Load。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Extension.Core.Conversion、Extension.Core.Buffers、Extension.IO.Compression、Extension.Integration.Cloning、Extension.UI.Controls、Extension.Core.Models。导入不代表所有类型都会在运行时加载。
using Extension.Core.Conversion;
using Extension.Core.Buffers;
using Extension.IO.Compression;
using Extension.Integration.Cloning;
using Extension.UI.Controls;
using Extension.Core.Models;
using Extension.Imaging.Models;
using Extension.Automation.Device;
using Extension.Automation.Secs;
using DataManager.Core.Log.Definition;
using MachineApp.Data;
using MachineApp.Business.Flow;
using MachineApp.Infra.Tool;
using MachineApp.Business.Order;

using SecsGem.Core;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace MachineApp.Business.Station
{
    /// <summary>
    /// LoadInfo 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// 继承或实现的契约：Result。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class LoadInfo:Result
    {
        /// <summary>
        /// RFID数据
        /// </summary>
        public string  RFIDInfo  { get; set; } 
    }

    /// <summary>
    /// ELoad 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// </summary>
    public enum ELoad
    {
        上料请求,
        读取RFID,
    }

    /// <summary>
    /// Load 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// 继承或实现的契约：StationBase&lt;LoadInfo&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class Load : StationBase<LoadInfo>
    {
        /// <summary>
        /// 工站名称
        /// </summary>
        private string stationName = EProcess.进站流程.ToString();
        internal string StationName = " ";

        private Recipe _Recipe;

        /// <summary>
        /// 初始化 Load 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 getRecipe；具体条件和先后次序见方法体。
        /// </summary>
        public Load(StationServices services, Func<Recipe> getRecipe) : base(services)
        {
            _Recipe=getRecipe();
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

        /// <summary>
        /// 执行加载操作；对应入口为 Load_Request。
        /// 正文中的关键协作调用包括 Update、SendS6F11；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
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
            var read = _devices.Rfid[0].ReadPagesAsync(1, 2).AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
            if (!read.Success) throw new InvalidOperationException(read.Message ?? read.ErrorCode);
            var d11 = (read.Value ?? string.Empty).RemoveControl();
           
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

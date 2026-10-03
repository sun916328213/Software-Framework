// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：EProcess、Process。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Extension.UI.Controls、Extension.Core.Models、Extension.Imaging.Models、Extension.Automation.Device、Extension.Automation.Secs、DataManager.Core.Log.Definition。导入不代表所有类型都会在运行时加载。
using Extension.UI.Controls;
using Extension.Core.Models;
using Extension.Imaging.Models;
using Extension.Automation.Device;
using Extension.Automation.Secs;
using DataManager.Core.Log.Definition;
using MachineApp.Data;
using MachineApp.Business.Station;
using MachineApp.Infra.Tool;
using MachineApp.Business.Order;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace MachineApp.Business.Flow
{
    /// <summary>
    /// EProcess 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// </summary>
    public enum EProcess
    {
        AL袋流程,

        抽真空流程,
        冲氮气流程,
        热封流程,

        ///实际流程
        进站流程,
        配件流程,
        贴标流程,
        入袋流程,
        出站流程,
    }

    /// <summary>
    /// Process 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// 继承或实现的契约：StationBase&lt;int&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class Process : StationBase<int>
    {
        CancellationTokenSource CancellationTokenSource_EVA = new CancellationTokenSource();
        CancellationTokenSource CancellationTokenSource_Nit = new CancellationTokenSource();
        CancellationTokenSource CancellationTokenSource_Temp = new CancellationTokenSource();

        /// <summary>
        /// 初始化 Process 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Cancel；具体条件和先后次序见方法体。
        /// </summary>
        public Process(StationServices services) : base(services)
        {
            CancellationTokenSource_EVA.Cancel();
            CancellationTokenSource_Nit.Cancel();
            CancellationTokenSource_Temp.Cancel();
        }
     

        /// <summary>
        /// 流程触发
        /// </summary>
        public async Task ProcessTrigger(string plcorder, EProcess process)
        {
            EPLCResult result = EPLCResult.NG;
            try
            {
                string Step = null, ID = null;
                switch (process)
                {
                    case EProcess.抽真空流程:
                        _PLCMethod.TriggerReply(process.ToString(),PLCProcess.EVAProcess_Trigger);
                        Step = _PLCMethod.ReadData(process.ToString(), PLCProcess.EVAProcess_Trigger, PLCAddressOffest.Function);
                        ID = _PLCMethod.ReadData(process.ToString(), PLCProcess.EVAProcess_Trigger, PLCAddressOffest.ID);

                        if (Step == "1")
                        {
                             //开始实时读取抽真空数据
                            StartEVAProcess();
                            result = EPLCResult.OK;
                        }
                        else if (Step == "2")
                        {
                            CancellationTokenSource_EVA.Cancel();
                            result = EPLCResult.OK;
                        }
                        break;

                    case EProcess.冲氮气流程:
                        _PLCMethod.TriggerReply(process.ToString(), PLCProcess.NitProcess_Trigger);
                        Step = _PLCMethod.ReadData(process.ToString(), PLCProcess.NitProcess_Trigger, PLCAddressOffest.Function);
                        ID = _PLCMethod.ReadData(process.ToString(), PLCProcess.NitProcess_Trigger, PLCAddressOffest.ID);

                        //开始实时读取冲氮气数据
                        if (Step == "1")
                        {
                            //开始实时读取抽真空数据
                            StartNitProcess();
                            result = EPLCResult.OK;
                        }
                        else if (Step == "2")
                        {
                            CancellationTokenSource_Nit.Cancel();
                            result = EPLCResult.OK;
                        }
                        break;

                    case EProcess.热封流程:
                        _PLCMethod.TriggerReply(process.ToString(), PLCProcess.HeatProcess_Trigger);
                        Step = _PLCMethod.ReadData(process.ToString(), PLCProcess.HeatProcess_Trigger, PLCAddressOffest.Function);
                        ID = _PLCMethod.ReadData(process.ToString(), PLCProcess.HeatProcess_Trigger, PLCAddressOffest.ID);

                        //开始实时读取热封温度数据
                        if (Step == "1")
                        {
                            //开始实时读取抽真空数据
                            StartTempProcess();
                            result = EPLCResult.OK;
                        }
                        else if (Step == "2")
                        {
                            CancellationTokenSource_Temp.Cancel();
                            result = EPLCResult.OK;
                        }
                        break;

                    case EProcess.出站流程:
                        _PLCMethod.TriggerReply(process.ToString(), PLCProcess.UnloadProcess_Trigger);
                        Step = _PLCMethod.ReadData(process.ToString(), PLCProcess.UnloadProcess_Trigger, PLCAddressOffest.Function);
                        ID = _PLCMethod.ReadData(process.ToString(), PLCProcess.UnloadProcess_Trigger, PLCAddressOffest.ID);
                         
                        if (Step == "1")
                        {
                            Start(process, ID);
                            result = EPLCResult.OK;
                        }
                        else if (Step == "2")
                        {
                            End(process, ID);

                            await _SecsMethod.SendS6F11(EventOrder.UnLoad_Request);
                            result = EPLCResult.OK;
                        }
                        else if (Step == "3")
                        {
                            UnLoadComplete(ID);
                            result = EPLCResult.OK;
                        }
                        break;

                    default:
                        _PLCMethod.TriggerReply(process.ToString(), plcorder);//回复PLC
                        Step = _PLCMethod.ReadData(process.ToString(), plcorder, PLCAddressOffest.Function);//获取Step
                        ID = _PLCMethod.ReadData(process.ToString(), plcorder, PLCAddressOffest.ID);//获取FOSBID

                        if (Step == "1")//开始
                        {
                            Start(process, ID);
                            result = EPLCResult.OK;
                        }
                        else if (Step == "2")///结束
                        {
                            End(process, ID);
                            result = EPLCResult.OK;
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                result = EPLCResult.ERROR;
                string message = string.Join("\r\n", ex.Message, ex.StackTrace);
                _LogInfoService.AddRunInfo(process + ":" + message, ELogLevel.SysError);
            }
            finally
            {
                _PLCMethod.SendResult(process.ToString(), plcorder, result);
            }
        }

        /// <summary>
        /// 开始
        /// </summary>
        /// <returns></returns>
        private async void Start(EProcess StationName, string ID)
        {
            Func<Task> action = new Func<Task>(async () =>
            {
                _SecsMethod.Update(VIDOrder.FOSBID, ID);  //更新VID数据
                _LogInfoService.AddRunInfo(StationName.ToString() + ":" + LangSwitchEx.GetMessage(Message.开始));
                await _SecsMethod.SendS6F11(EventOrder.ReadFOSB_Star);
                switch (StationName)
                {
                    case EProcess.进站流程:
                        await _SecsMethod.SendS6F11(EventOrder.ReadFOSB_Star);
                        break;
                    case EProcess.配件流程:
                        await _SecsMethod.SendS6F11(EventOrder.PlaceDesiccant_Start);
                        break;
                    case EProcess.入袋流程:
                        await _SecsMethod.SendS6F11(EventOrder.HeatSeal_Start);
                        break;
                    case EProcess.贴标流程:
                        await _SecsMethod.SendS6F11(EventOrder.LabelProcess_Satrt);
                        break;
                }
            });
            Func<Task> action1 = new Func<Task>(async () =>
            {
                _LogInfoService.AddRunInfo(StationName.ToString() + ":" + LangSwitchEx.GetMessage(Message.开始));
            });
            await _SecsMethod.Run(action1, action, action);
        }

        /// <summary>
        /// 结束
        /// </summary>
        /// <param name="ID"></param>
        private async void End(EProcess  StationName, string ID)
        {
            Func<Task> action = new Func<Task>(async () =>
            {
                _SecsMethod.Update(VIDOrder.FOSBID, ID);  //更新VID数据
                _LogInfoService.AddRunInfo(StationName.ToString() + ":" + LangSwitchEx.GetMessage(Message.结束));
                switch (StationName)
                {
                    case EProcess.进站流程:
                        await _SecsMethod.SendS6F11(EventOrder.ReadFOSB_End);
                        break;
                    case EProcess.配件流程:
                        await _SecsMethod.SendS6F11(EventOrder.PlaceDesiccant_End);
                        break;
                    case EProcess.入袋流程:
                        await _SecsMethod.SendS6F11(EventOrder.HeatSeal_End);
                        break;
                    case EProcess.贴标流程:
                        await _SecsMethod.SendS6F11(EventOrder.LabelProcess_End);
                        break;
                }
            });
            Func<Task> action1 = new Func<Task>(async () =>
            {
                _LogInfoService.AddRunInfo(StationName.ToString() + ":" + LangSwitchEx.GetMessage(Message.结束));
            });
            await _SecsMethod.Run(action1, action, action);
        }


        /// <summary>
        /// 抽真空流程 实时读取数据 
        /// </summary>
        private void StartEVAProcess()
        {
            if(!CancellationTokenSource_EVA.IsCancellationRequested==true) return;//检测是否已经取消  运行中则不运行

            CancellationTokenSource_EVA = new CancellationTokenSource();
            Task.Run(  () =>
            {
                _LogInfoService.AddRunInfo( Message.抽真空流程开始.GetMessage());
                
                while (!CancellationTokenSource_EVA.IsCancellationRequested)
                {
                    Thread.Sleep(1000);
                    //读取PLC信息
                    string stationname = EProcess.抽真空流程.ToString();
                    var d1 = Convert.ToInt32(_PLCMethod.ReadData(stationname, PLCProcess.EVAProcess_Traffic, 0));
                    var d2 = Convert.ToInt32(_PLCMethod.ReadData(stationname, PLCProcess.EVAProcess_Pressure, 0));
                    var d3 = Convert.ToInt32(_PLCMethod.ReadData(stationname, PLCProcess.EVAProcess_Time, 0));

                    if (_SecsMethod.CurrentMode != ESecsMode.Offline)
                    {
                        //更新VID
                        _SecsMethod.Append(VIDOrder.VacuumFlowRate, d1);
                        _SecsMethod.Append(VIDOrder.VacuumPressure, d2);
                        _SecsMethod.Append(VIDOrder.VacuumTime, d3);
                    }
                }
            });
        }

        /// <summary>
        /// 充氮气流程 实时读取数据 
        /// </summary>
        private void StartNitProcess()
        {
            if (!CancellationTokenSource_Nit.IsCancellationRequested == true) return;//检测是否已经取消  运行中则不运行
            CancellationTokenSource_Nit = new CancellationTokenSource();
            Task.Run(() =>
            {
                _LogInfoService.AddRunInfo( Message.充氮气流程开始.GetMessage());
                while (!CancellationTokenSource_Nit.IsCancellationRequested)
                {
                    Thread.Sleep(1000);
                    //读取PLC信息
                    string stationname = EProcess.冲氮气流程.ToString();

                    var d1 = Convert.ToInt32(_PLCMethod.ReadData(stationname, PLCProcess.NitProcess_Traffic, 0));
                    var d2 = Convert.ToInt32(_PLCMethod.ReadData(stationname, PLCProcess.NitProcess_Pressure, 0));
                    var d3 = Convert.ToInt32(_PLCMethod.ReadData(stationname, PLCProcess.NitProcess_Time, 0));

                    if (_SecsMethod.CurrentMode != ESecsMode.Offline)
                    {
                        //更新VID
                        _SecsMethod.Append(VIDOrder.NitrogenFlowRate, d1);
                        _SecsMethod.Append(VIDOrder.NitrogenPressure, d2);
                        _SecsMethod.Append(VIDOrder.NitrogenTime, d3);
                    }
                }
            });
        }

        /// <summary>
        /// 热封温度流程 实时读取数据 
        /// </summary>
        private void StartTempProcess()
        {
            if (!CancellationTokenSource_Temp.IsCancellationRequested == true) return;//检测是否已经取消  运行中则不运行
            CancellationTokenSource_Temp = new CancellationTokenSource();
            Task.Run(async () =>
            {
                _LogInfoService.AddRunInfo(Message.热封流程开始.GetMessage());
                while (!CancellationTokenSource_Temp.IsCancellationRequested)
                {
                    Thread.Sleep(1000);
                    //读取PLC信息
                    string stationname = EProcess.热封流程.ToString();
                    var d1 = Convert.ToInt32(_PLCMethod.ReadData(stationname, PLCProcess.HeatProcess_Temp, 0));
                    var d2 = Convert.ToInt32(_PLCMethod.ReadData(stationname, PLCProcess.HeatProcess_Time, 0));

                    if (_SecsMethod.CurrentMode != ESecsMode.Offline)
                    {
                        //更新VID
                        _SecsMethod.Append(VIDOrder.HeatSealTemp, d1);
                        _SecsMethod.Append(VIDOrder.HeatSealTime, d2);
                    }
                }
                ///热封流程结束后将数据发送给EAP
                await _SecsMethod.SendS6F11(EventOrder.HeatSeal_Info);
            });
        }

     
        /// <summary>
        /// 出站已取走
        /// </summary>
        /// <param name="LotID"></param>
        private async void UnLoadComplete(string LotID)
        {
            Func<Task> action = new Func<Task>(async () =>
            {
                _LogInfoService.AddRunInfo( Message.FOSB下料成功.GetMessage());

                _SecsMethod.Update(VIDOrder.FOSBID, LotID.ToString());  //更新VID
                await _SecsMethod.SendS6F11(EventOrder.UnLoad_Complete);

                //流程结束
                await _SecsMethod.SendS6F11(EventOrder.Process_End);
            });
            Func<Task> action1 = new Func<Task>(async () =>
            {
                _LogInfoService.AddRunInfo(Message.FOSB下料成功.GetMessage());
            });
            await  _SecsMethod.Run(action1, action, action);
        }
    }
}

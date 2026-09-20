using Communication;
using Communication.MC;
using Extension.UI.Controls;
using DataManager.Log.Definition;
using Packing_Machine.Data;
using Packing_Machine.Business.Order;
using Prism.Ioc;
using Secs;
using System.Diagnostics;
using WPFControl.CustomControl.Models;
using WPFControl.Helper;
using WPFControl.ProgramControl.Services;
using Packing_Machine.Business.Station;

namespace Packing_Machine.Infra.Tool
{
    public static class PLCAddressOffest
    {
        public static int Trigger = 0;
        public static int Reply = 1;
        public static int Result = 2;
        public static int Function = 3;
        public static int Count = 4;
        public static int CountReply = 5;

        public static int ID = 10;
    }

    public class PLCTool
    {

        readonly MCHelper _MCHelper;
        readonly CommunicationManager _CommunicationManager;
        readonly IContainerProvider _container;
        LogInfoService _LogInfoService => _container.Resolve<LogInfoService>();
        readonly SecsControl _SecsControl;
     
        readonly object _Lock = new object();

        public PLCTool(IContainerProvider container)
        {
            _container = container;
            _CommunicationManager = container.Resolve<CommunicationManager>();
            _MCHelper = container.Resolve<MCHelper>();
            _SecsControl = container.Resolve<SecsControl>();
           
        }

        /// <summary>
        /// 刷新任务
        /// </summary>
        public void Refresh()
        {
            TryCatchHelper.TryExecute(() =>
          {
              _CommunicationManager.SetWriteTask(PLCSystem.Online, 1);//设置心跳

              _CommunicationManager.SetReadTask("Trigger", "D8000", 1000, Extension.Core.Comparison.EComparePolarity.BothEdges);//设置读取数据区间
              _CommunicationManager.SetReadTask("Alarm", "D6000", 1000, Extension.Core.Comparison.EComparePolarity.None);//设置读取数据区间

              _CommunicationManager.StartLoop();//开启循环
          });
        }

        public void StopRefresh()
        {
            _CommunicationManager.StopLoopRead();
        }

 
        /// <summary>
        /// 触发回复
        /// </summary>
        /// <param name="sationName"></param>
        /// <param name="pPLCOrder"></param>
        public void TriggerReply(string stationName, string pPLCOrder)
        {
            TryCatchHelper.TryExecute(() =>
            {
                lock (_Lock)
                {
                    short triggerval = (short)_CommunicationManager.Read(pPLCOrder);
                    AddLog(stationName, "接收触发信息", pPLCOrder, PLCAddressOffest.Trigger, triggerval.ToString());

                    if (_CommunicationManager.Reset(pPLCOrder, PLCAddressOffest.Result, 1))
                        AddLog(stationName, "复位结果", pPLCOrder, PLCAddressOffest.Result, null);

                    ////for(int i=0;i<10;i++)
                    //if (_CommunicationManager.Reset(pPLCOrder, PLCAddressOffest.ID, 20))
                    //    AddLog(stationName, "复位ID", pPLCOrder, PLCAddressOffest.ID, null);

                    //写入回复
                    triggerval = 1;
                    if (_CommunicationManager.Write(pPLCOrder, PLCAddressOffest.Reply, triggerval))
                        AddLog(stationName, "发送回复", pPLCOrder, PLCAddressOffest.Reply, triggerval.ToString());

                    //读取计数
                    short countval = (short)_CommunicationManager.Read(pPLCOrder, PLCAddressOffest.Count);
                    //写入计数
                    if (_CommunicationManager.Write(pPLCOrder, PLCAddressOffest.CountReply, Convert.ToInt16(countval)))
                        AddLog(stationName, "发送计数", pPLCOrder, PLCAddressOffest.CountReply, countval.ToString());
                }
            });
        }

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="sationName"></param>
        /// <param name="pPLCOrder"></param>
        /// <param name="OffestAdd"></param>
        /// <param name="info"></param>
        public void SendData(string sationName, string pPLCOrder, int OffestAdd, object info)
        {
            TryCatchHelper.TryExecute(() =>
            {
                if (_CommunicationManager.Write(pPLCOrder, OffestAdd, info))
                    AddLog(sationName, "发送数据", pPLCOrder, OffestAdd,  info.ToString());
            });
        }


        /// <summary>
        /// 发送结果
        /// </summary>
        /// <param name="pPLCOrder"></param>
        /// <param name="info"></param>
        public void SendResult(string sationName, string pPLCOrder, EPLCResult info)
        {
            TryCatchHelper.TryExecute(() =>
            {
                //写入结果
                if (_CommunicationManager.Write(pPLCOrder, PLCAddressOffest.Result, info))
                    AddLog(sationName, "发送结果", pPLCOrder, PLCAddressOffest.Result, info.ToString());
            });
        }

        /// <summary>
        /// 流程结束
        /// </summary>
        /// <param name="sationName"></param>
        /// <param name="pPLCOrder"></param>
        /// <param name="info"></param>
        public void Complete(string sationName, string pPLCOrder)
        {
            TryCatchHelper.TryExecute(() =>
            {
                //复位
                if (_CommunicationManager.Reset(pPLCOrder, PLCAddressOffest.Reply, 1))
                    AddLog(sationName, "复位", pPLCOrder, PLCAddressOffest.Reply, null);
            });
        }

        /// <summary>
        /// 所有流程结束
        /// </summary>
        /// <param name="sationName"></param>
        /// <param name="pPLCOrder"></param>
        /// <param name="length"></param>
        public void CompleteAll(string sationName, string pPLCOrder, int length)
        {
            TryCatchHelper.TryExecute(() =>
            {
                if (_CommunicationManager.Reset(pPLCOrder, 0, length))
                    AddLog(sationName, "复位所有", pPLCOrder, 0, null);
            });
        }


        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="sationName"></param>
        /// <param name="pPLCOrder"></param>
        /// <param name="OffestAdd"></param>
        /// <returns></returns>
        public string ReadData(string sationName, string pPLCOrder, int OffestAdd = 0,bool islog=true)
        {
            return TryCatchHelper.TryExecute(() =>
              {
                  var d = _CommunicationManager.Read(pPLCOrder, OffestAdd)?.ToString();
                  if (islog) AddLog(sationName, "读取数据", pPLCOrder, OffestAdd, d);
                  return d;
              });
        }

        /// <summary>
        /// 写入用户信息
        /// </summary>
        /// <param name="pPLCOrder"></param>
        /// <param name="value"></param>
        public void TriggerUserInfo( UserInfo value)
        {
            TryCatchHelper.TryExecute(() =>
          {
              _CommunicationManager.Reset(PLCSystem.PC_UserName, 0, 10);
              if (_CommunicationManager.Write(PLCSystem.PC_UserName, 0, value.Name))
                  AddLog("Alarm", "报警信息", PLCSystem.PC_UserName, 0, value.Name.ToString());
              if (_CommunicationManager.Write(PLCSystem.PC_Level, 0, (int)value.Level))
                  AddLog("Alarm", "报警信息", PLCSystem.PC_Level, 0, ((int)value.Level).ToString());
          });
        }

        /// <summary>
        /// 写入报警信息
        /// </summary>
        /// <param name="pPLCOrder"></param>
        /// <param name="value"></param>
        public void TriggerAlarm(string pPLCOrder, short value)
        {
            TryCatchHelper.TryExecute(() =>
            {
                if (_CommunicationManager.Write(pPLCOrder, 0, value))
                    AddLog("Alarm", "报警信息", pPLCOrder, 0, value.ToString());
            });
        }

        /// <summary>
        /// 添加日志
        /// </summary>
        /// <param name="stationName"></param>
        /// <param name="fun"></param>
        /// <param name="pPLCOrder"></param>
        /// <param name="offestAdd"></param>
        /// <param name="value"></param>
        private void AddLog(string stationName, string fun, string pPLCOrder, int offestAdd, string value)
        {
            stationName = stationName.Replace(":","").Trim() + ": ";
            _LogInfoService.AddRunInfo(stationName + fun + " " + "地址:" + pPLCOrder + " " + "偏移:" + offestAdd + " " + "数据:" + value);
        }

        int count;

       /// <summary>
       /// 触发PLC 
       /// </summary>
       /// <param name="stationName"></param>
       /// <param name="pPLCOrder"></param>
       /// <param name="data"></param>
       /// <returns></returns>
        public async Task<EPLCResult> TriggerAsync(string stationName, string pPLCOrder, string data)
        {
            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                //写入计数
                if (_CommunicationManager.Write(pPLCOrder, PLCAddressOffest.Count, Convert.ToInt16(count++)))
                    AddLog(stationName, "发送计数", pPLCOrder, PLCAddressOffest.Count, count.ToString());

                //数据
                if (_CommunicationManager.Write(pPLCOrder, PLCAddressOffest.CountReply + 1, data))
                    AddLog(stationName, "发送数据", pPLCOrder, PLCAddressOffest.CountReply + 1, data);

                if (_CommunicationManager.Write(pPLCOrder, PLCAddressOffest.Trigger, 1))
                    AddLog(stationName, "触发信息", pPLCOrder, PLCAddressOffest.Trigger, "1");

                //等待发送回复
                while (true)
                {
                    int d1 = Convert.ToInt32(_CommunicationManager.Read(pPLCOrder, PLCAddressOffest.Reply));
                    if (d1 == 1)
                        break;
                    else if (stopwatch.ElapsedMilliseconds > 200)
                    {
                        _LogInfoService.AddRunInfo(stationName + Message.PLC回复超时.GetMessage(), ELogLevel.Error);
                        throw new Exception("time out");
                    }
                    await Task.Delay(100);
                }

                //读取计数
                short countval = (short)_CommunicationManager.Read(pPLCOrder, PLCAddressOffest.CountReply);
                AddLog(stationName, "读取计数", pPLCOrder, PLCAddressOffest.CountReply, countval.ToString());


                ///等待发送结果
                while (true)
                {
                    int d1 = Convert.ToInt32(_CommunicationManager.Read(pPLCOrder, PLCAddressOffest.Result));
                    if (d1 == 1)
                        break;
                    else if (stopwatch.ElapsedMilliseconds > 200)
                    {
                        _LogInfoService.AddRunInfo(stationName + Message.PLC回复超时.GetMessage(), ELogLevel.Error);
                        throw new Exception("time out");
                    }
                    await Task.Delay(100);
                }
                return EPLCResult.OK;
                }
            catch (Exception ex)
            {
                return EPLCResult.NG;
            }
            finally {

                //清除触发
                if (_CommunicationManager.Write(pPLCOrder, PLCAddressOffest.Trigger, 0))
                    AddLog(stationName, "触发信息清除", pPLCOrder, PLCAddressOffest.Trigger, "0");
            }
        }
    }
}

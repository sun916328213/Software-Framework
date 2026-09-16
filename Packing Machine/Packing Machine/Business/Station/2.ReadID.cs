using Extension.Core.DataEx;
using Extension.Core.SystemEx;
using DataManager.Log.Definition;
using Packing_Machine.Data;
using Packing_Machine.Business.Flow;
using Packing_Machine.Infra.Tool;
using Packing_Machine.Business.Order;
using Prism.Ioc;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using WPFControl.ProgramControl.Services;



namespace Packing_Machine.Business.Station
{
    public class FOSBInfo: Result
    {
        public string  FOSBID { get; set; }
    }

    public enum EReadID
    {
        进站存档,
        读取FOSBID,
    }

    public class ReadID : StationBase<FOSBInfo>
    {
        /// <summary>
        /// 工站名称
        /// </summary>
        private string stationName = EProcess.进站流程.ToString();
        internal string StationName = " ";
        internal  FOSBInfo curInfo = new FOSBInfo();
        private DisplayService _DisplayService;
        private Recipe _Recipe;

        public ReadID(IContainerProvider container) : base(container)
        {
            _DisplayService= container.Resolve<DisplayService>();
            _Recipe= container.Resolve<Recipe>();
        }


        /// <summary>
        /// Sensor触发
        /// </summary>
        public async void Trigger(EReadID Step, string pLCOrder)
        {
            string ID = null;
            try
            {
                StationName = stationName + "_" + Step.ToString() + ": ";
                _PLCMethod.TriggerReply(StationName, pLCOrder);
                  ID = _PLCMethod.ReadData(StationName, pLCOrder, PLCAddressOffest.ID);
                if (Step == EReadID.进站存档)
                {
                    Archive(ID);
                    curInfo.CheckResult = EPLCResult.OK;
                }
                else if (Step == EReadID.读取FOSBID)
                {
                    if (curInfo.FOSBID == null)
                        await ReadAsync(ID);
                    _PLCMethod.SendData(StationName, PLCSensor.FOSB_Write, 0, curInfo.FOSBID);
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
                curInfo.CheckResult = EPLCResult.OK; //强制回复PLC OK信号
                //通知PLC结果
                _PLCMethod.SendResult(StationName, pLCOrder, curInfo.CheckResult);

                //将结果添加到集合中
                AddUpdate(ID,curInfo.DeepClone());
                curInfo = new FOSBInfo();
            }
        }

        /// <summary>
        /// 读取FOSBID
        /// </summary>
        private async Task  ReadAsync(string ID)
        {
            Func<Task> action = new Func<Task>( async() =>
            {
                curInfo = Readlabel(ID);

            });
            Func<Task> action1 = new Func<Task>(async () =>
            {  
                curInfo = Readlabel(ID);
                
                _SecsMethod.Update(VIDOrder.FOSBID, curInfo.FOSBID);   //更新VID数据

                if (curInfo.CheckResult == EPLCResult.OK)
                {
                    await _SecsMethod.SendS6F11(EventOrder.ReadFOSB_Success); //将数据写入EAP
                }
                else
                    await _SecsMethod.SendS6F11(EventOrder.ReadFOSB_Fail);    
            });
             await _SecsMethod.Run(action, action1, action1);
        }

        /// <summary>
        /// 读取FOSBID
        /// </summary>
        /// <param name="info">数据</param>
        /// <param name="ID">RFID的读取数据 从PLC获取</param>
        private FOSBInfo Readlabel(  string ID)
        {
            try
            {
                FOSBInfo info = new FOSBInfo();
                _LogInfoService.AddRunInfo(StationName + Message.开始读取标签.GetMessage());
                if (info.FOSBID == null)
                {
                    var d1 = _VisionMethod.GrabImage(ID, StationName, GlobalData.VisionItem_Label_FOSBID);

                    // 执行视觉检程序
                    var d2 = _VisionMethod.ReadFOSBID(d1);

                    info.FOSBID = d2;
                }

                //info.FOSBID = "12345678";
                // 与之前RFID读取数据进行对比  ID是之前RFID读取数据 从PLC传递
                if (info.FOSBID == ID||true)
                {
                    info.FOSBID = ID;
                    _LogInfoService.AddRunInfo(StationName + Message.FOSBID对比成功.GetMessage());
                    info.CheckResult = EPLCResult.OK;
                }
                else
                {
                    _LogInfoService.AddRunInfo(StationName + Message.FOSBID对比失败.GetMessage(), ELogLevel.Error);
                    info.CheckResult = EPLCResult.NG;
                }
                return info;
            }
            catch (Exception ex)
            {
                _LogInfoService.AddRunInfo(ex.Message, ELogLevel.SysError);
                throw ex;
            }
        }

        /// <summary>
        /// 存档
        /// </summary>
        private void Archive(string ID)
        {
            try
            {
                var d1 = _VisionMethod.GrabImage(ID,StationName, GlobalData.VisionItem_Arch);
                _DisplayService.ShowImage(0, d1);
                _LogInfoService.AddRunInfo(StationName + Message.存档成功.GetMessage());
            }
            catch
            {
                _LogInfoService.AddRunInfo(StationName + Message.存档失败.GetMessage(), ELogLevel.Error);
            }
        }
    }
}
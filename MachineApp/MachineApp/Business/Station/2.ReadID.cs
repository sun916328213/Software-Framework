// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：FOSBInfo、EReadID、ReadID。成员说明可结合下方具体实现阅读。
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
using System.Diagnostics;
using System.Threading.Tasks;
using WPFControls.ProgramControl.Services;



namespace MachineApp.Business.Station
{
    /// <summary>
    /// FOSBInfo 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// 继承或实现的契约：Result。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class FOSBInfo: Result
    {
        public string  FOSBID { get; set; }
    }

    /// <summary>
    /// EReadID 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// </summary>
    public enum EReadID
    {
        进站存档,
        读取FOSBID,
    }

    /// <summary>
    /// ReadID 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// 继承或实现的契约：StationBase&lt;FOSBInfo&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
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

        /// <summary>
        /// 初始化 ReadID 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 getDisplayService、getRecipe；具体条件和先后次序见方法体。
        /// </summary>
        public ReadID(StationServices services, Func<DisplayService> getDisplayService, Func<Recipe> getRecipe) : base(services)
        {
            _DisplayService= getDisplayService();
            _Recipe= getRecipe();
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
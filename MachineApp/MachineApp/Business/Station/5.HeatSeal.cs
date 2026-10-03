// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：HeatSealInfo、HeatSeal。成员说明可结合下方具体实现阅读。
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MachineApp.Business.Station
{
    /// <summary>
    /// HeatSealInfo 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// 继承或实现的契约：Result。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class  HeatSealInfo : Result
    {
        /// <summary>
        /// NG图像ID
        /// </summary>
        public List<int> Index { get; set; }
    }

    /// <summary>
    /// HeatSeal 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// 继承或实现的契约：StationBase&lt;HeatSealInfo&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class HeatSeal : StationBase<HeatSealInfo>
    {
        internal string StationName =EProcess.热封流程.ToString();

        HeatSealInfo curInfo = new HeatSealInfo();

        /// <summary>
        /// 初始化 HeatSeal 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public HeatSeal(StationServices services) : base(services)
        {
        }
 
        /// <summary>
        /// Sensor触发
        /// </summary>
        public async Task Trigger(string pLCOrder)
        {
            string ID = null;

            try
            {
                _LogInfoService.AddRunInfo(StationName + Message.触发热封相机拍照.GetMessage());
                //开启光源
                 foreach (var item in GlobalData.VisionItem_AI.LightItems)
                    MachineApp.Infra.Device.ProductionDeviceSession.Ensure(await _devices.Lights[0].SetIntensityAsync(item.ChannelIndex, item.LightIntensity));

                _PLCMethod.TriggerReply( StationName,pLCOrder);
                ID = _PLCMethod.ReadData(StationName, pLCOrder, PLCAddressOffest.ID );

             await  HeatSealDetectAsync(ID);
            }
            catch (Exception ex)
            {
                curInfo.CheckResult = EPLCResult.ERROR;
                string message = string.Join("\r\n", ex.Message, ex.StackTrace);
                _LogInfoService.AddRunInfo(StationName + ":" + message, ELogLevel.SysError);
            }
            finally
            {
                //关闭光源
                foreach (var item in GlobalData.VisionItem_AI.LightItems)
                    MachineApp.Infra.Device.ProductionDeviceSession.Ensure(await _devices.Lights[0].SetIntensityAsync(item.ChannelIndex, 0));

                //通知PLC结果
                _PLCMethod.SendResult(StationName, pLCOrder, curInfo.CheckResult);

                //将结果添加到集合中
                AddUpdate(ID,  curInfo.DeepClone());
                curInfo = new HeatSealInfo();
            }
        }


        /// <summary>
        /// 热封口检测
        /// </summary>
        /// <param name="ID"></param>
        private async Task  HeatSealDetectAsync(string ID)
        {
            Func<Task> action = new Func<Task>( async() =>
            {
                curInfo = await Detect(ID);
            });

            Func<Task> action1 = new Func<Task>(async () =>
            {
                curInfo =  await Detect(ID);

                //刷新VID信息-----------------------------------------
                _SecsMethod.Update(VIDOrder.FOSBID, ID);  //更新ID
                _SecsMethod.Update(VIDOrder.VisionResult, curInfo.CheckResult== EPLCResult.OK?0:1); //更新视觉检测

                if (curInfo.CheckResult == EPLCResult.OK)
                {
                    _SecsMethod.Update(VIDOrder.VisionResult, 0);          //视觉检测结果
                    await _SecsMethod.SendS6F11(EventOrder.HeatSeal_Vision_Success); //将数据写入EAP
                }
                else
                {
                    _SecsMethod.Update(VIDOrder.VisionResult, 1);          //视觉检测结果
                    await _SecsMethod.SendS6F11(EventOrder.HeatSeal_Vision_Fail); //将数据写入EAP
                }

            });
            await _SecsMethod.Run(action, action1, action1);
        }

        /// <summary>
        /// 热封口视觉检测
        /// </summary>
        public async Task<HeatSealInfo> Detect(string FOSBID)
        {
            try
            {
                HeatSealInfo info = new HeatSealInfo();
                Stopwatch sw = Stopwatch.StartNew();

                var d1 = await _VisionMethod.GrabImages(FOSBID, StationName, 5, GlobalData.VisionItem_AI, 6);

                _LogInfoService.AddRunInfo(StationName + Message.热封相机拍照成功.GetMessage());

                // 执行视觉检程序
                var d = _VisionMethod.HeatSealDetect(d1);
                if (d == null)
                {
                    info.CheckResult = EPLCResult.OK;
                }
                else
                {
                    info.CheckResult = EPLCResult.NG;
                    info.Index = d;
                }
                //if (sw.ElapsedMilliseconds > 1000)
                //{
                //    _LogInfoService.AddRunInfo(StationName + Message.热封相机拍照失败.GetMessage(), ELogLevel.Error);
                //    info.CheckResult = EPLCResult.NG;
                //    break;
                //}
                return info;
            }
            catch(Exception ex)
            {
                _LogInfoService.AddRunInfo(StationName + Message.热封相机拍照失败.GetMessage());
                throw ex;
            }
        }

    }
}
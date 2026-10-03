// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：ALInfo、ALBase、EALDetect、ALDetect。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MachineApp.Data、MachineApp.Business.Flow、MachineApp.Infra.Tool、MachineApp.Business.Order、System、System.Windows。导入不代表所有类型都会在运行时加载。
using MachineApp.Data;
using MachineApp.Business.Flow;
using MachineApp.Infra.Tool;
using MachineApp.Business.Order;

using System;
using System.Windows;
using Extension.Core.Conversion;
using DataManager.Core.Log.Definition;
using Extension.Core.Buffers;
using Extension.IO.Compression;
using Extension.Integration.Cloning;
using Extension.UI.Controls;
using WPFCustomControls.Window.Views;

namespace MachineApp.Business.Station
{
    /// <summary>
    /// ALInfo 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// 继承或实现的契约：Result。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class ALInfo : Result
    {
        /// <summary>
        /// 宽度
        /// </summary>
        public double Width { get; set; }
        /// <summary>
        /// 角度
        /// </summary>
        public double Angle { get; set; }
    }

    /// <summary>
    /// ALBase 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// </summary>
    public class ALBase
    {
        /// <summary>
        /// 基准宽度
        /// </summary>
        public double BaseWidth { get; set; }
        /// <summary>
        /// 左侧基准
        /// </summary>
        public Point Left { get; set; }
        /// <summary>
        /// 右侧基准
        /// </summary>
        public Point Right { get; set; }
    }

    /// <summary>
    /// EALDetect 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// </summary>
    public enum EALDetect
    {
        AL示教,
        AL检测,
    }

    /// <summary>
    /// ALDetect 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// 继承或实现的契约：StationBase&lt;ALInfo&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class ALDetect : StationBase<ALInfo>
    {
 
        ALInfo curInfo = new ALInfo();

        ALBase aLBase = new ALBase();
        private string stationName = EProcess.AL袋流程.ToString();
        internal string StationName = " ";

        string FilePath = "Config\\";
        string FileName = "ALInfo";
        /// <summary>
        /// 初始化 ALDetect 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Read；具体条件和先后次序见方法体。
        /// </summary>
        public ALDetect(StationServices services) : base(services)
        {
            aLBase = JsonEx.Read<ALBase>(FilePath, FileName);
            if (aLBase == null) aLBase = new ALBase();
        }

 
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Trigger。
        /// 正文中的关键协作调用包括 ToString、TriggerReply、ReadData、ToDouble、Teach；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public void Trigger(EALDetect order, string pLCOrder)
        {
            EPLCResult result = EPLCResult.NG;
            string ID = null;
            try
            {
                StationName = stationName + "_" + order.ToString() + ": ";
               
                _PLCMethod.TriggerReply(StationName, pLCOrder);
                ID = _PLCMethod.ReadData(StationName, pLCOrder, PLCAddressOffest.ID );
                switch (order)
                {
                    case EALDetect.AL示教:
                        var width = Convert.ToDouble(_PLCMethod.ReadData(StationName, PLCSensor.AL_CalibWidth));
                        Teach(width);
                        result = EPLCResult.OK;
                        break;
                    case EALDetect.AL检测:
                        curInfo = Detect(ID);
                        result = EPLCResult.OK;
                        _PLCMethod.SendData(StationName, PLCSensor.AL_Width, 0, (curInfo.Angle * 100).ToString("0"));
                        break;
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
                _PLCMethod.SendResult(StationName, pLCOrder, result);

                AddUpdate(ID,curInfo.DeepClone());
                curInfo = new ALInfo();

            }
        }

        /// <summary>
        /// 检测AL袋
        /// </summary>
        /// <returns></returns>
        private ALInfo Detect(string id)
        {
            _LogInfoService.AddRunInfo(StationName + Message.开始AL袋定位.GetMessage());

            var d1 = _VisionMethod.GrabImage(id, StationName, GlobalData.VisionItem_ALLeft);
            var d2 = _VisionMethod.GrabImage(id, StationName, GlobalData.VisionItem_ALRight);

            // 执行视觉检程序
            var d3 = _VisionMethod.ALDetect(d1, d2, id);
            
            var w1 = d3[0].Value - aLBase.Left + d3[1].Value - aLBase.Right;
            var w2 = w1.X + aLBase.BaseWidth;
            ALInfo cALInfo = new ALInfo() { Width = w2, Angle = 0 };
            return cALInfo;
        }

        /// <summary>
        /// 示教AL
        /// </summary>
        /// <param name="BaseWIdth"></param>
        private void Teach(double BaseWIdth = 500)
        {
            var d1 = _VisionMethod.GrabImage(null, StationName, GlobalData.VisionItem_ALLeft, false);
            var d2 = _VisionMethod.GrabImage(null, StationName, GlobalData.VisionItem_ALRight, false);

            var d3 = _VisionMethod.ALDetect(d1, d2, null);
            if (d3 != null)
            {
                aLBase.Left = d3[0].Value;
                aLBase.Right = d3[1].Value;
                aLBase.BaseWidth = BaseWIdth;
                JsonEx.Save(aLBase, FilePath, FileName);
            }
            else
            {
                MessagePrompt messagePrompt = new MessagePrompt("提示", "AL袋示教检测失败", Visibility.Hidden);
                messagePrompt.Show();
                throw new Exception("AL袋示教检测失败");
            }
        }
    }
}


 
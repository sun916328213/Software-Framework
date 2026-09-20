using Packing_Machine.Data;
using Packing_Machine.Business.Flow;
using Packing_Machine.Infra.Tool;
using Packing_Machine.Business.Order;
using Prism.Ioc;
using System;
using System.Windows;
using Extension.Core.Conversion;
using DataManager.Log.Definition;
using Extension.Core.Buffers;
using Extension.IO.Compression;
using Extension.Integration.Cloning;
using Extension.UI.Controls;
using WPFCustomControls.Window.Views;

namespace Packing_Machine.Business.Station
{
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

    public enum EALDetect
    {
        AL示教,
        AL检测,
    }

    public class ALDetect : StationBase<ALInfo>
    {
 
        ALInfo curInfo = new ALInfo();

        ALBase aLBase = new ALBase();
        private string stationName = EProcess.AL袋流程.ToString();
        internal string StationName = " ";

        string FilePath = "Config\\";
        string FileName = "ALInfo";
        public ALDetect(IContainerProvider container) : base(container)
        {
            aLBase = JsonEx.Read<ALBase>(FilePath, FileName);
            if (aLBase == null) aLBase = new ALBase();
        }

 
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


 
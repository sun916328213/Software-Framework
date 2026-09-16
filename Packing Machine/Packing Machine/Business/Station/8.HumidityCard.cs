using DPLibrary_Halcon_F48.Tools;
using Extension.Core.ControlEx;
using Extension.Core.ConvertEx;
using Extension.Core.SystemEx;
using Extension.Struct;
using Packing_Machine.Data;
using Packing_Machine.Business.Flow;
using Packing_Machine.Infra.Tool;
using Packing_Machine.Business.Order;
using Packing_Machine.UI.ViewModel;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;


namespace Packing_Machine.Business.Station
{
    public class HumidityCardInfo
    {
        /// <summary>
        /// 湿度卡名称
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 相机基准位置 10%
        /// </summary>
        public Point BaseCam{get;set;}
        /// <summary>
        /// 10%偏移
        /// </summary>
        public Point Offest10 { get; set; }
        /// <summary>
        /// 20%偏移
        /// </summary>
        public Point Offest20 { get; set; }
        /// <summary>
        /// 30%偏移
        /// </summary>
        public Point Offest30 { get; set; }
        /// <summary>
        /// 40%偏移
        /// </summary>
        public Point Offest40 { get; set; }
        /// <summary>
        /// 50%偏移
        /// </summary>
        public Point Offest50 { get; set; }
        /// <summary>
        /// 60%偏移
        /// </summary>
        public Point Offest60 { get; set; }

        /// <summary>
        /// 颜色传感器示教时Robot的坐标
        /// </summary>
        public Point Robot_SensorTeach { get; set; }
        /// <summary>
        /// 相机标定点集合
        /// </summary>
        public List<Point> CamCalibPoint { get; set; }=new List<Point>();
        /// <summary>
        /// 机器人标定点集合
        /// </summary>
        public List<Point> RobotCalibPoint { get; set; } = new List<Point>();
    }

    public enum EHumidityCard
    {
        九点标定开始,
        九点标定,
        湿度卡相机准基,
        颜色传感器位置示教,
        湿度卡相机检测,
    }


    public class HumidityCard : StationBase<int>
    {
        public HumidityCard(IContainerProvider container) : base(container)
        {
            //读取
            var data = JsonEx.Read<HumidityCardInfo>(Path, FileName);
            if (data != null)
                HumidityCardInfo = data;
        }

        private string stationName = "湿度卡工站";
        internal string StationName = "";

        private string Path = "Config//";

        private string FileName = "HumidityCard";

        public HumidityCardInfo HumidityCardInfo { get; set; } = new HumidityCardInfo();
 
        private CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();

        /// <summary>
        /// Sensor触发
        /// </summary>
        public void Trigger(EHumidityCard order,string pLCOrder)
        {
            EPLCResult result = EPLCResult.NG;
            try
            {
                StationName = stationName + "_" + order.ToString() + ": ";
                _PLCMethod.TriggerReply(StationName, PLCSensor.RobotCenterCam_Trigger);
                string Step = _PLCMethod.ReadData(StationName, pLCOrder, PLCAddressOffest.Function);
                string ID = _PLCMethod.ReadData(StationName, pLCOrder, PLCAddressOffest.ID).ToString();

                switch (order)
                {
                    case EHumidityCard.九点标定开始:
                        HumidityCardInfo.CamCalibPoint.Clear();
                        HumidityCardInfo.RobotCalibPoint.Clear();
                        StartCalib();
                        result = EPLCResult.OK;
                        return;
                    case EHumidityCard.九点标定:
                        StartCalib();
                        result = EPLCResult.OK;
                        return;
                    case EHumidityCard.湿度卡相机准基:
                        HICamReference();
                        result = EPLCResult.OK;
                        break;
                    //case EHumidityCard.颜色传感器位置示教:
                    //    Step = _PLCMethod.ReadData(StationName, PLCSensor.RobotCenterCam_Trigger, PLCAddressOffest.Function);
                    //    ID = _PLCMethod.ReadData(StationName, PLCSensor.RobotCenterCam_Trigger, PLCAddressOffest.FOSBID);
                    //    HISensorTeach();
                    //    result = EPLCResult.OK;
                    //    break;
                    case EHumidityCard.湿度卡相机检测:
                      
                        CalHIPoint(ID);
                        result = EPLCResult.OK;

                        _PLCMethod.SendData(StationName, PLCSensor.HIX, 0, (HumidityCardInfo.Offest10.X * 100).ToString("0"));
                        _PLCMethod.SendData(StationName, PLCSensor.HIY, 0, (HumidityCardInfo.Offest10.Y * 100).ToString("0"));

                        _PLCMethod.SendData(StationName, PLCSensor.HIX, 4, (HumidityCardInfo.Offest20.X * 100).ToString("0"));
                        _PLCMethod.SendData(StationName, PLCSensor.HIY, 4, (HumidityCardInfo.Offest20.Y * 100).ToString("0"));
                                                                                     
                        _PLCMethod.SendData(StationName, PLCSensor.HIX, 8, (HumidityCardInfo.Offest30.X * 100).ToString("0"));
                        _PLCMethod.SendData(StationName, PLCSensor.HIY, 8, (HumidityCardInfo.Offest30.Y * 100).ToString("0"));

                        _PLCMethod.SendData(StationName, PLCSensor.HIX, 12, (HumidityCardInfo.Offest40.X * 100).ToString("0"));
                        _PLCMethod.SendData(StationName, PLCSensor.HIY, 12, (HumidityCardInfo.Offest40.Y * 100).ToString("0"));
                                                                                      
                        _PLCMethod.SendData(StationName, PLCSensor.HIX, 16, (HumidityCardInfo.Offest50.X * 100).ToString("0"));
                        _PLCMethod.SendData(StationName, PLCSensor.HIY, 16, (HumidityCardInfo.Offest50.Y * 100).ToString("0"));
                                                                                      
                        _PLCMethod.SendData(StationName, PLCSensor.HIX, 20, (HumidityCardInfo.Offest60.X * 100).ToString("0"));
                        _PLCMethod.SendData(StationName, PLCSensor.HIY, 20, (HumidityCardInfo.Offest60.Y * 100).ToString("0"));

                        break;
                }
            }
            catch (Exception ex)
            {
                result = EPLCResult.ERROR;
                string message = string.Join("\r\n", ex.Message, ex.StackTrace);
                _LogInfoService.AddRunInfo(StationName + ":" + message, ELogLevel.SysError);
            }
            finally
            {
                //通知PLC结果
                _PLCMethod.SendResult(StationName, pLCOrder, result);
            }
        }

        /// <summary>
        /// 开始标定流程
        /// </summary>
        private void StartCalib()
        {
            try
            {
 
                //读取机器人当前位置
                double x = Convert.ToSingle(_PLCMethod.ReadData(StationName, PLCSensor.RobotX, 0)) /*/ 100.0*/;
                double y = Convert.ToSingle(_PLCMethod.ReadData(StationName, PLCSensor.RobotY, 0)) /*/ 100.0*/;
                HumidityCardInfo.RobotCalibPoint.Add(new Point(x, y));

                //相机拍照
                var d2 = _VisionMethod.GrabImage(null, StationName, GlobalData.VisionItem_HI,false);
                var d3 = _VisionMethod.CaliberTemplateMatch(d2);
                HumidityCardInfo.CamCalibPoint.Add(d3);

                //发送结果
                _PLCMethod.SendResult(StationName, PLCSensor.RobotCenterCam_Trigger, EPLCResult.OK);
                if (HumidityCardInfo.CamCalibPoint.Count == 9)
                {
                    Action action = () =>
                    {
                        HumidityCardInfo.CamCalibPoint.Reverse();
                        _VisionMethod.Caliber(HumidityCardInfo.CamCalibPoint, HumidityCardInfo.RobotCalibPoint);
                    };
                    action.UIInvoke();
                }
            }
            catch
            {
                //发送结果
                _PLCMethod.SendResult(StationName, PLCSensor.RobotCenterCam_Trigger, EPLCResult.ERROR);
            }
        }


        /// <summary>
        /// 湿度卡拍照基准
        /// </summary>
        /// <returns></returns>
        private void HICamReference()
        {
            _LogInfoService.AddRunInfo(StationName + Message.湿度卡相机基准示教.GetMessage());
            var d1 = _VisionMethod.GrabImage(null, StationName, GlobalData.VisionItem_HI, true);

            // 执行视觉检程序
            var data = _VisionMethod.HICircle(d1, null);
            if (data != null)
            {
                HumidityCardInfo.BaseCam = data[0].Center;
                //保存
                HumidityCardInfo.Save(Path, FileName);
            }
        }

        /// <summary> 
        /// 颜色传感器位置示教  不需要
        /// </summary>
        public void HISensorTeach()
        {
            _LogInfoService.AddRunInfo(StationName + Message.湿度卡颜色传感器基准示教.GetMessage());
            //读取机械手点位
            double x = Convert.ToDouble(_PLCMethod.ReadData(StationName, PLCSensor.RobotX, 0)) / 100.0;
            double y = Convert.ToDouble(_PLCMethod.ReadData(StationName, PLCSensor.RobotY, 0)) / 100.0;
            HumidityCardInfo.Robot_SensorTeach = new Point(x, y);
            //保存
            HumidityCardInfo.Save(Path, FileName);
        }

        /// <summary>
        /// 计算湿度卡上的圆心坐标
        /// </summary>
        /// <returns></returns>
         private  void CalHIPoint(string id)
        {


            _LogInfoService.AddRunInfo(StationName + Message.湿度卡定位开始.GetMessage());
            var d1 = _VisionMethod.GrabImage(id, StationName, GlobalData.VisionItem_HI);
            // 执行视觉检程序 计算
            var point = _VisionMethod.HICircle(d1, id);

            HumidityCardInfo.Offest10 = CalOffest(point[0].Center);
            HumidityCardInfo.Offest20 = CalOffest(point[1].Center);
            HumidityCardInfo.Offest30 = CalOffest(point[2].Center);
            HumidityCardInfo.Offest40 = CalOffest(point[3].Center);
            HumidityCardInfo.Offest50 = CalOffest(point[4].Center);
            HumidityCardInfo.Offest60 = CalOffest(point[5].Center);
        }

        private Point CalOffest(Point CurCam)
        {
            double x = (CurCam.X - HumidityCardInfo.BaseCam.X) /*+ HumidityCardInfo.Robot_SensorTeach.X*/;
            double y = (CurCam.Y - HumidityCardInfo.BaseCam.Y) /*+ HumidityCardInfo.Robot_SensorTeach.Y*/;
            return new Point(x,y);
            //return new Point(y, x);

        }
    }
}


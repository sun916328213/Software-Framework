// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：VisionTool。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MachineApp.Business.Ports、MachineApp.Business.Models、Vision.Tools.Halcon.Sessions、Vision.Tools.Halcon.Measurement、Vision.Tools.Halcon.Matching、Vision.Tools.Halcon.Identification。导入不代表所有类型都会在运行时加载。
using MachineApp.Business.Ports;
using MachineApp.Business.Models;
using Vision.Tools.Halcon.Sessions;
using Vision.Tools.Halcon.Measurement;
using Vision.Tools.Halcon.Matching;
using Vision.Tools.Halcon.Identification;
using Vision.Tools.Halcon.Coordinate;
using Vision.Tools.Halcon.Calibration;
using Vision.Core.Geometry;
using Device.Core.Cameras;
using MachineApp.Infra.Device;
using Vision.Core.Vision;
using Extension.Core.Conversion;
using Extension.Core.Buffers;
using Extension.IO.Compression;
using Extension.Integration.Cloning;
using Extension.IO.Files;
using Extension.IO.Csv;
using Extension.IO.Retention;
using Extension.Imaging.Halcon;
using Extension.Imaging.OpenCv;
using Extension.UI.Controls;
using Extension.Platform.Diagnostics;
using Extension.Platform.Native;
using Extension.Platform.Memory;
using Extension.Platform.Processes;
using DataManager.Core.Log.Definition;
using HalconDotNet;
using MachineApp.Data;
using MachineApp.Business.Station;
using MachineApp.UI.ViewModel;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Services;
 


namespace MachineApp.Infra.Tool
{
    /// <summary>
    /// VisionTool 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
    /// 继承或实现的契约：IProductionVision。具体约束按这些契约及各成员实现解释。
    /// </summary>
    internal class VisionTool : IProductionVision
    {
        readonly ProductionDeviceSession _devices;
        readonly DisplayService _DisplayService;

        object obj_cam = new object();
        VisionViewModel _VisionViewModel;

        private readonly IContainerProvider _container;
        LogInfoService _LogInfoService => _container.Resolve<LogInfoService>();

        /// <summary>
        /// 初始化 VisionTool 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Resolve；具体条件和先后次序见方法体。
        /// </summary>
        public VisionTool(IContainerProvider container)
        {
            _container = container;
            _devices = container.Resolve<ProductionDeviceSession>();
            _DisplayService= container.Resolve<DisplayService>();

            _VisionViewModel= container.Resolve<VisionViewModel>();

        }

        /// <summary>
        /// 拍照
        /// </summary>
        /// <param name="FosbID"></param>
        /// <param name="StationName"></param>
        /// <param name="CamIndex"></param>
        /// <param name="LightIndex"></param>
        /// <returns></returns>
        public HObject GrabImage(string FosbID, string StationName, VisionInspectionItem visionItem, bool isSaveImg=true)
        {
            lock (obj_cam)
            {
                try
                {
                    foreach (var item in visionItem.LightItems)
                        ProductionDeviceSession.Ensure(_devices.Lights[0].SetIntensityAsync(item.ChannelIndex, item.LightIntensity)
                            .AsTask().ConfigureAwait(false).GetAwaiter().GetResult());
                    var camera = _devices.Camera(visionItem.CamName);
                    ProductionDeviceSession.Ensure(camera.SetExposureGainAsync(visionItem.Exposure, visionItem.Gain)
                        .AsTask().ConfigureAwait(false).GetAwaiter().GetResult());
                    var frame = camera.CaptureAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
                    var image = ToHalconImage(frame);
                    _LogInfoService.AddRunInfo(StationName + Message.相机拍照成功.GetMessage());
                    if (isSaveImg)
                    {
                        try
                        {
                            string path = DirectoryEx.CreatDir(GlobalData.Path_Image, FosbID);
                            image.SaveImage(path, StationName.Trim().Replace(":", "_") + DateTime.Now.ToTimeMs());
                        }
                        catch { image.Dispose(); throw; }
                    }
                    return image;
                }
                finally
                {
                    foreach (var item in visionItem.LightItems)
                        ProductionDeviceSession.Ensure(_devices.Lights[0].SetIntensityAsync(item.ChannelIndex, 0)
                            .AsTask().ConfigureAwait(false).GetAwaiter().GetResult());
                }
            }
        }

        /// <summary>
        /// 拍照获取多张图像
        /// </summary>
        /// <param name="FosbID"></param>
        /// <param name="StationName"></param>
        /// <param name="CamIndex"></param>
        /// <returns></returns>
        public async Task<List<HObject>> GrabImages(string FosbID, string StationName, int CamIndex, VisionInspectionItem visionItem, int triggernum)
        {

            var images = new List<HObject>();
            var camera = _devices.Cameras[CamIndex];
            ProductionDeviceSession.Ensure(await camera.SetExposureGainAsync(visionItem.Exposure, visionItem.Gain));
            ProductionDeviceSession.Ensure(await camera.SetTriggerAsync(
                new CameraTriggerSettings { Mode = CameraTriggerMode.On, Source = CameraTriggerSources.Line1 },
                camera.Options.LineTrigger));
            try
            {
                for (var index = 0; index < triggernum; index++)
                {
                    using var timeout = new CancellationTokenSource(camera.Options.TimeoutMilliseconds);
                    var frame = await camera.CaptureAsync(timeout.Token);
                    var image = ToHalconImage(frame);
                    images.Add(image);
                    string path = DirectoryEx.CreatDir(GlobalData.Path_Image, FosbID);
                    image.SaveImage(path, StationName.Trim().Replace(":", "") + DateTime.Now.ToTimeMs());
                }
                _LogInfoService.AddRunInfo(StationName.Trim() + Message.相机拍照成功.GetMessage());
                return images;
            }
            catch
            {
                foreach (var image in images) image.Dispose();
                throw;
            }
            finally
            {
                ProductionDeviceSession.Ensure(await camera.SetTriggerAsync(camera.Options.FrameTrigger, camera.Options.LineTrigger));
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ToHalconImage。
        /// 正文中的关键协作调用包括 ToHImage、ToArray；具体条件和先后次序见方法体。
        /// </summary>
        private static HObject ToHalconImage(ImageFrame frame)
        {
            // GenImage1 accepts unpacked monochrome data, not Bayer or interleaved RGB.
            if (frame.PixelFormat is not ("Mono8" or "Mono16"))
                throw new NotSupportedException($"当前检测链只支持 Mono8/Mono16，相机输出为 {frame.PixelFormat}。");
            return frame.Data.ToArray().ToHImage(frame.Width, frame.Height);
        }

        /// <summary>
        /// 显示并保存处理图像
        /// </summary>
        /// <param name="ID">FOSBID</param>
        /// <param name="DisplayControlIndex">显示控件ID</param>
        /// <param name="Tool">HVisionTool工具</param>
        private static void Execute(HalconToolSession tool)
        {
            tool.Run();
            if (tool.ToolState == Vision.Core.Tools.E_ToolState.Failed)
                throw new InvalidOperationException(tool.RunMessage);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ShowHandleImage。
        /// 正文中的关键协作调用包括 ShowImage；具体条件和先后次序见方法体。
        /// </summary>
        private void ShowHandleImage(int DisplayControlIndex, HalconToolSession Tool, string ID)
        {
            //显示图像
            _DisplayService.ShowImage(DisplayControlIndex, Tool.Image);
            //if (ID != null)
            //{
            //    //存储处理图像
            //    string path =  DirectoryEx.CreatDir(GlobalData.Path_Image, ID);
            //    _DisplayService.SaveDisplayImage(DisplayControlIndex, path, DateTime.Now.ToTimeMs());
            //}
        }


        /// <summary>
        /// 读取FOSBID
        /// </summary>
        /// <returns></returns>
        public string ReadFOSBID(HObject image)
        {
            BarcodeSession  Tool = _VisionViewModel.Datas.BarcodeReader;
            Tool.Image = image;
            Execute(Tool);

            _VisionViewModel.Datas.BarcodeReader = Tool;

            var d1 = Tool.LastResult?.Codes ?? [];
            var ids = d1.Select(s => s.Text);
            var Id = ids.FirstOrDefault();

            ShowHandleImage(5, Tool, Id);
            return Id;
        }

        /// <summary>
        /// 配件检测
        /// </summary>
        /// <returns></returns>
        public AccessoryInfo DetectAccessory(HObject image,string ID)
        {
            ShapeMatchSession Tool = _VisionViewModel.Datas.TemplateMatch_Accessory;
            Tool.Image = image;
            Execute(Tool);
            var d1 = Tool.LastResult?.Matches ?? [];

            ShowHandleImage(2, Tool, ID);
            if (d1.Count == 1)
            {
                return new AccessoryInfo { Num_Desiccant = 4, Num_HumidityIndicator = 1, CheckResult = EPLCResult.OK };
            }
            else
            {
                return new AccessoryInfo { CheckResult = EPLCResult.NG };
            }
        }

        /// <summary>
        /// 湿度卡定位
        /// </summary>
        /// <param name="image"></param>
        /// <param name="pCardType">0=A 1=B 默认湿度卡A</param>
        /// <returns></returns>
        public List<CCircleInfo> HICircle(HObject image, string ID, int pCardType = 0)
        {
            ShapeMatchSession Tool;
            if (pCardType == 0) Tool = _VisionViewModel.Datas.TemplateMatch_HumidityCardA;
            else Tool = _VisionViewModel.Datas.TemplateMatch_HumidityCardB;

            Tool.Image = image;
            Execute(Tool);
            var d1 = Tool.LastResult?.Matches ?? [];

            if (d1.Count == 0) return null;

            var d2 = FindCircle(image);

            TransformPointSession tp = _VisionViewModel.Datas.TransformPointSession;

            //添加圆轮廓
            for (int i = 0; i < d2.Count; i++)
            {

                tp.Parameters.Point = new Point2D(d2[i].Center.X, d2[i].Center.Y);
                tp.Run();
                if (tp.LastResult?.Success != true) throw new InvalidOperationException(tp.RunMessage);
                d2[i].Center = new Point(tp.LastResult.Point.X, tp.LastResult.Point.Y);
            }
            //foreach (var item in d2)
            //{
            //    I_HXLD xld = new HXLD_Null(item.Xld) { DispColor = E_VisionColor.Blue };
            //    Tool.ResultXLDs.Add(xld);
            //}

            if (pCardType == 0) _VisionViewModel.Datas.TemplateMatch_HumidityCardA = Tool;
            else _VisionViewModel.Datas.TemplateMatch_HumidityCardB = Tool;

            ShowHandleImage(0, Tool, ID);

            //判断角度
            if (Math.Abs(d1[0].AngleDeg) < 10)
            {
                // 按Y坐标排序
                return d2.OrderByDescending(c => c.Center.Y).ToList();
            }
            else
            {
                // 按Y坐标排序
                return d2.OrderBy(c => c.Center.Y).ToList();
            }
        }



        /// <summary>
        /// 查找圆
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public List<CCircleInfo> FindCircle(HObject image)
        {
            List<CCircleInfo> circleInfos = new List<CCircleInfo>();
            HObject Region;
            HTuple Rows, Cols, Radius, StartPhi, EndPhi, PointOrder;

            HOperatorSet.EdgesSubPix(image, out Region, "canny", 1, 20, 40);
            HOperatorSet.SelectShapeXld(Region, out Region,new HTuple ("circularity", "area"), "and", new HTuple(0.85, 20000), new HTuple(2, 999999999));
            HOperatorSet.FitCircleContourXld(Region, "algebraic", -1, 0, 0, 3, 2,
                out Rows, out Cols, out Radius, out StartPhi, out EndPhi, out PointOrder);

            for (int index = 0; index < Rows.Length; index++)
            {
                HObject ContCircle;
                CCircleInfo cCircleInfo = new CCircleInfo();
                cCircleInfo.Center = new Point(Cols[index], Rows[index]);
                cCircleInfo.R = Radius[index];

                HOperatorSet.GenCircleContourXld(out ContCircle, Rows[index], Cols[index], Radius[index],
                    StartPhi[index], EndPhi[index], "positive", 1);
                cCircleInfo.Xld = ContCircle;
                circleInfos.Add(cCircleInfo);
            }

            // 按半径降序排序，取前6个作为外圆
            var outerCircles = circleInfos.OrderByDescending(c => c.R).Take(6).ToList();

            return outerCircles;
        }

        /// <summary>
        /// 湿度卡位置标定匹配
        /// </summary>
        /// <returns></returns>
        public Point CaliberTemplateMatch(HObject image )
        {
            ShapeMatchSession Tool = _VisionViewModel.Datas.TemplateMatch_Caliber;
            Tool.Image = image;
            Execute(Tool);
            var d1 = Tool.LastResult?.Matches ?? [];

            ShowHandleImage(0,  Tool,null);
            if (d1.Count == 0) throw new InvalidOperationException("标定模板未匹配到目标。");
            return new Point(d1[0].Center.X, d1[0].Center.Y);
        }

        /// <summary>
        /// 标定
        /// </summary>
        /// <param name="cam"></param>
        /// <param name="robot"></param>
        public void Caliber(List<Point> cam, List<Point> robot)
        {
            try
            {
                CalibrationSession Tool = _VisionViewModel.Datas.CalibrationSession;
                Tool.Parameters.Points.Clear();

                for (int index = 0; index < cam.Count; index++)
                {
                    CalibrationPoint calibPoint = new CalibrationPoint();
                    calibPoint.WorldX = robot[index].X;
                    calibPoint.WorldY = robot[index].Y;
                    calibPoint.PixelX = cam[index].X;
                    calibPoint.PixelY = cam[index].Y;
                    Tool.Parameters.Points.Add(calibPoint);
                }
                Execute(Tool);

                if (Tool.LastResult?.Success != true) throw new InvalidOperationException(Tool.RunMessage);
                _VisionViewModel.Datas.TransformPointSession.Parameters.Matrix = Tool.LastResult.Matrix;
                _VisionViewModel.Datas.TransformPointSession.Save();
                Tool.Save();
                _LogInfoService.AddRunInfo(Message.九点标定成功.GetMessage());
            }
            catch
            {
                _LogInfoService.AddRunInfo(Message.九点标定失败.GetMessage(), ELogLevel.Error);
            }
        }

        /// <summary>
        /// 读取标签 用,连接
        /// </summary>
        /// <returns></returns>
        public string ReadLabel(HObject image, string ID)
        {
            try
            {
                BarcodeSession Tool_Code = _VisionViewModel.Datas.BarcodeReader;
                DataCodeSession Tool_Code2D = _VisionViewModel.Datas.BarcodeReader_2D;
                Tool_Code.Image = image;
                Execute(Tool_Code);
                Tool_Code2D.Image = image;
                Execute(Tool_Code2D);

                _VisionViewModel.Datas.BarcodeReader = Tool_Code;
                _VisionViewModel.Datas.BarcodeReader_2D = Tool_Code2D;

                var d1 = Tool_Code.LastResult?.Codes ?? [];
                var d2 = Tool_Code2D.LastResult?.Codes ?? [];

                // 合并两个列表并用逗号连接
                var allResults = d1.Concat(d2).ToList();
                ShowHandleImage(5, Tool_Code, ID);

                var labelinfo = string.Join(",", allResults.Select(r => r.Text));
                _LogInfoService.AddRunInfo(labelinfo);
                return labelinfo;
            }
            catch (Exception ex)
            {
                _LogInfoService.AddRunInfo(ex.Message, ELogLevel.SysError);
                return null;
            }
        }

        /// <summary>
        /// 读取标签 用,连接
        /// </summary>
        /// <returns></returns>
        public string ReadLabel_Auto(HObject image, string ID)
        {
            AutoBarcodeSession Tool_Code = _VisionViewModel.Datas.BarcodeReader_Auto;
            DataCodeSession Tool_Code2D = _VisionViewModel.Datas.BarcodeReader_2D;
            Tool_Code.Image = image;
            Execute(Tool_Code);
            Tool_Code2D.Image = image;
            Execute(Tool_Code2D);
            var d1 = Tool_Code.LastResult?.Codes ?? [];
            var d2 = Tool_Code2D.LastResult?.Codes ?? [];

            // 合并两个列表并用逗号连接
            var allResults = d1.Concat(d2).ToList();
            ShowHandleImage(5, Tool_Code, ID);

            return string.Join(",", allResults.Select(r => r.Text));
        }

        /// <summary>
        /// 热封视觉检测
        /// </summary>
        /// <param name="Images">输入图像</param>
        /// <returns>返回NG图像的序列号</returns>
        public List<int> HeatSealDetect(List<HObject> Images)
        {

            return new List<int> { 0 };
        }

        /// <summary>
        /// 铝塑袋检测
        /// </summary>
        /// <param name="image1"></param>
        /// <param name="image2"></param>
        /// <returns></returns>
        public List<Point?> ALDetect(HObject image1, HObject image2, string ID)
        {
            LineGroupSession Tool1 = _VisionViewModel.Datas.BagPattern_Left;
            LineGroupSession Tool2 = _VisionViewModel.Datas.BagPattern_Right;

            Tool1.Image = image1;
            Execute(Tool1);
            Tool2.Image = image2;
            Execute(Tool2);

            _VisionViewModel.Datas.BagPattern_Left = Tool1;
            _VisionViewModel.Datas.BagPattern_Right = Tool2;

            ShowHandleImage(3, Tool1, ID);
            ShowHandleImage(4, Tool2, ID);

            var d1 = Tool1.LastResult?.Lines ?? [];
            var d2 = Tool2.LastResult?.Lines ?? [];

            if (d1.Count < 2 || d2.Count < 2) return null;
            var d3 = IntersectionLine(d1[0], d1[1]);
            var d4 = IntersectionLine(d2[0], d2[1]);
            if (d3 == null || d4 == null) return null;

            List<Point?> result = new List<Point?>();
            result.Add(d3);
            result.Add(d4);
            return result;
        }

        /// <summary>
        /// 计算两条直线的交点
        /// </summary>
        /// <param name="line1">第一条直线</param>
        /// <param name="line2">第二条直线</param>
        /// <returns>交点坐标，如果平行则返回null</returns>
        public Point? IntersectionLine(FindLineResult line1, FindLineResult line2)
        {
            // 检查输入是否为空
            if (line1 == null || line2 == null)
            {
                return null;
            }

            // 调用计算交点的方法
            return CalculateLineIntersection(new Point(line1.Start.X,line1.Start.Y), new Point(line1.End.X,line1.End.Y), new Point(line2.Start.X,line2.Start.Y), new Point(line2.End.X,line2.End.Y));
        }

        /// <summary>
        /// 计算两条直线的交点
        /// </summary>
        /// <param name="line1Start">第一条直线的起点</param>
        /// <param name="line1End">第一条直线的终点</param>
        /// <param name="line2Start">第二条直线的起点</param>
        /// <param name="line2End">第二条直线的终点</param>
        /// <returns>交点坐标，如果平行则返回null</returns>
        public Point? CalculateLineIntersection(Point line1Start, Point line1End, Point line2Start, Point line2End)
        {
            // 直线1的方向向量
            double dx1 = line1End.X - line1Start.X;
            double dy1 = line1End.Y - line1Start.Y;

            // 直线2的方向向量
            double dx2 = line2End.X - line2Start.X;
            double dy2 = line2End.Y - line2Start.Y;

            // 计算叉积
            double cross = dx1 * dy2 - dy1 * dx2;

            // 如果叉积为0，则两直线平行
            if (Math.Abs(cross) < 1e-8)
            {
                return null; // 平行线无交点
            }

            // 计算参数t
            double dx3 = line1Start.X - line2Start.X;
            double dy3 = line1Start.Y - line2Start.Y;

            double t = (dx3 * dy2 - dy3 * dx2) / cross;

            // 计算交点
            return new Point(line1Start.X + t * dx1, line1Start.Y + t * dy1);
        }
    }
}

using Device.Camera;
using Device.Camera.Definition;
using Device.Camera.Gige;
using Device.Light;
using Vision.Core.Vision;
using DPLibrary_Halcon_F48;
using DPLibrary_Halcon_F48.Tools;
using DPLibrary_Halcon_F48.Tools.Find;
using DPLibrary_Halcon_F48.Tools.FindLine;
using DPLibrary_Halcon_F48.Tools.TemplateMatch;
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
using DataManager.Log.Definition;
using HalconDotNet;
using Packing_Machine.Data;
using Packing_Machine.Business.Station;
using Packing_Machine.UI.ViewModel;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;
 


namespace Packing_Machine.Infra.Tool
{
    internal class VisionTool
    {
        readonly CamManager _CamManager;
        readonly DisplayService _DisplayService;
        readonly LightControlManager _LightControlManager;
        object obj_cam = new object();
        VisionViewModel _VisionViewModel;

        private readonly IContainerProvider _container;
        LogInfoService _LogInfoService => _container.Resolve<LogInfoService>();

        public VisionTool(IContainerProvider container)
        {
            _container = container;
            _CamManager = container.Resolve<CamManager>();
            _DisplayService= container.Resolve<DisplayService>();
            _LightControlManager= container.Resolve<LightControlManager>();
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
                foreach (var item in visionItem.LightItems)
                    _LightControlManager[0].Setlight(item.LightIntensity, item.ChannelIndex);
                //DelayEx.Delay(500);

                _CamManager[visionItem.CamName].Exposure = visionItem.Exposure;
                _CamManager[visionItem.CamName].Gain= visionItem.Gain;
                _CamManager[visionItem.CamName].Snap();
                //获取图像
                ImageData imagedata = _CamManager[visionItem.CamName].GetImage();
                _LogInfoService.AddRunInfo(StationName + Message.相机拍照成功.GetMessage());

                ///关闭光源
                foreach (var item in visionItem.LightItems)
                    _LightControlManager[0].Setlight(0, item.ChannelIndex);

                var image = RawEx.ToHImage(imagedata.RawData, imagedata.Width, imagedata.Height);

                if (isSaveImg)
                {
                    //存储图像
                    string path = DirectoryEx.CreatDir(GlobalData.Path_Image, FosbID);
                    image.SaveImage(path, StationName.Trim().Replace(":","_") + DateTime.Now.ToTimeMs());
                }
                return image;
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

            List<HObject> images = new List<HObject>();
            GigeCameraBase cam = (GigeCameraBase)_CamManager[CamIndex];

            // 清空通道残留数据，避免读到上次检测的旧图像
            cam.ImagesChannel.Clear();

            cam.Exposure = visionItem.Exposure;
            cam.SetTriggerMode(Device.Camera.Gige.Definition.ETriggerMode.Frame_Line1);

            Stopwatch stopwatch = Stopwatch.StartNew();
            while (cam.GetImageCount != triggernum)
            {
                await Task.Delay(1000);
                if (stopwatch.ElapsedMilliseconds == 50000000)
                    throw new Exception("PLC Trigger Cam Fail!!");
            }


            for (int i = 0; i < triggernum; i++)
            {
                //获取图像
                if (!cam.ImagesChannel.TryRead(out var imagedata))
                {
                    _LogInfoService.AddRunInfo($"{StationName} 读取图像失败: 第{i + 1}张", ELogLevel.Error);
                    continue;
                }

                HObject image;
                    // 海康/Basler/大华/埃科：使用RawData创建HImage（GC自动回收，自动识别位深格式）
                    image = imagedata.RawData.ToHImage(imagedata.Width, imagedata.Height);
    
                images.Add(image);
                //存储图像
                string path = DirectoryEx.CreatDir(GlobalData.Path_Image, FosbID);

                image.SaveImage(path, StationName.Trim().Replace(":", "") + DateTime.Now.ToTimeMs());

                await Task.Delay(100);
            }
            _LogInfoService.AddRunInfo(StationName.Trim() + Message.相机拍照成功.GetMessage());
            return images;
        }

        /// <summary>
        /// 显示并保存处理图像
        /// </summary>
        /// <param name="ID">FOSBID</param>
        /// <param name="DisplayControlIndex">显示控件ID</param>
        /// <param name="Tool">HVisionTool工具</param>
        private void ShowHandleImage(int DisplayControlIndex, HVisionTool Tool, string ID)
        {
            //显示图像
            _DisplayService.ShowImage(DisplayControlIndex, Tool);
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
            HTool_BarCode  Tool = _VisionViewModel.Datas.BarcodeReader;
            Tool.Image = image.Clone();
            Tool.Run();

            _VisionViewModel.Datas.BarcodeReader = Tool;

            List<HCodeResult> d1 = Tool.Results.OfType<HCodeResult>().ToList();
            var ids = d1.Select(s => s.Code);
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
            HTool_TemplateMatch Tool = _VisionViewModel.Datas.TemplateMatch_Accessory;
            Tool.Image = image.Clone();
            Tool.Run();
            List<HTemplateResult> d1 = Tool.Results.OfType<HTemplateResult>().ToList();

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
            HTool_TemplateMatch Tool;
            if (pCardType == 0) Tool = _VisionViewModel.Datas.TemplateMatch_HumidityCardA;
            else Tool = _VisionViewModel.Datas.TemplateMatch_HumidityCardB;

            Tool.Image = image.Clone();
            Tool.Run();
            List<HTemplateResult> d1 = Tool.Results.OfType<HTemplateResult>().ToList();

            if (d1.Count == 0) return null;

            var d2 = FindCircle(image);

            HTool_TransforPoint tp = _VisionViewModel.Datas.HTool_TransforPoint;

            //添加圆轮廓
            for (int i = 0; i < d2.Count; i++)
            {
                I_HXLD xld = new HXLD_Null(d2[i].Xld) { DispColor = E_VisionColor.Blue };
                Tool.ResultXLDs.Add(xld);
                tp.InputPoint = d2[i].Center;
                tp.Run();
                d2[i].Center = ((TransforPointResult)(tp.Results[0])).OutputPoint_T;
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
            if (Math.Abs(d1[0].R) < 10)
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

        public class CCircleInfo
        {
            public Point Center { get; set; }
 
            public double R { get; set; }

            public HObject Xld { get; set; }
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
            HTool_TemplateMatch Tool = _VisionViewModel.Datas.TemplateMatch_Caliber;
            Tool.Image = image.Clone();
            Tool.Run();
            List<HTemplateResult> d1 = Tool.Results.OfType<HTemplateResult>().ToList();

            ShowHandleImage(0,  Tool,null);
            return d1[0].Center;
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
                HTool_NPoints Tool = _VisionViewModel.Datas.HTool_NPoints;
                Tool.CalibPoints_XY.Clear();

                for (int index = 0; index < cam.Count; index++)
                {
                    CalibPoint calibPoint = new CalibPoint();
                    calibPoint.Rx = robot[index].X;
                    calibPoint.Ry = robot[index].Y;
                    calibPoint.Cx = cam[index].X;
                    calibPoint.Cy = cam[index].Y;
                    Tool.CalibPoints_XY.Add(calibPoint);
                }
                Tool.Run();

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
                HTool_BarCode Tool_Code = _VisionViewModel.Datas.BarcodeReader;
                HTool_2DCode Tool_Code2D = _VisionViewModel.Datas.BarcodeReader_2D;
                Tool_Code.Image = image.Clone();
                Tool_Code.Run();
                Tool_Code2D.Image = image.Clone();
                Tool_Code2D.Run();

                _VisionViewModel.Datas.BarcodeReader = Tool_Code;
                _VisionViewModel.Datas.BarcodeReader_2D = Tool_Code2D;

                List<HCodeResult> d1 = Tool_Code.Results.OfType<HCodeResult>().ToList();
                List<HCodeResult> d2 = Tool_Code2D.Results.OfType<HCodeResult>().ToList();

                // 合并两个列表并用逗号连接
                var allResults = d1.Concat(d2).ToList();
                ShowHandleImage(5, Tool_Code, ID);

                var labelinfo = string.Join(",", allResults.Select(r => r.Code));
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
            HTool_BarCode_AutoRegionZoomFind Tool_Code = _VisionViewModel.Datas.BarcodeReader_Auto;
            HTool_2DCode Tool_Code2D = _VisionViewModel.Datas.BarcodeReader_2D;
            Tool_Code.Image = image.Clone();
            Tool_Code.Run();
            Tool_Code2D.Image = image.Clone();
            Tool_Code2D.Run();
            List<HCodeResult> d1 = Tool_Code.Results.OfType<HCodeResult>().ToList();
            List<HCodeResult> d2 = Tool_Code2D.Results.OfType<HCodeResult>().ToList();

            // 合并两个列表并用逗号连接
            var allResults = d1.Concat(d2).ToList();
            ShowHandleImage(5, Tool_Code, ID);

            return string.Join(",", allResults.Select(r => r.Code));
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
            HTool_FindGroup Tool1 = _VisionViewModel.Datas.BagPattern_Left;
            HTool_FindGroup Tool2 = _VisionViewModel.Datas.BagPattern_Right;

            Tool1.Image = image1.Clone();
            Tool1.Run();
            Tool2.Image = image2.Clone();
            Tool2.Run();

            _VisionViewModel.Datas.BagPattern_Left = Tool1;
            _VisionViewModel.Datas.BagPattern_Right = Tool2;

            ShowHandleImage(3, Tool1, ID);
            ShowHandleImage(4, Tool2, ID);

            var d1 = Tool1.Results.OfType<HFindLineResult>().ToList();
            var d2 = Tool2.Results.OfType<HFindLineResult>().ToList();

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
        public Point? IntersectionLine(HFindLineResult line1, HFindLineResult line2)
        {
            // 检查输入是否为空
            if (line1 == null || line2 == null)
            {
                return null;
            }

            // 调用计算交点的方法
            return CalculateLineIntersection(line1.StartPoint, line1.EndPoint, line2.StartPoint, line2.EndPoint);
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

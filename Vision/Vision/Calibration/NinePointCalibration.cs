using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Vision.Calibration
{
    internal class NinePointCalibration
    {
        /// <summary>
        /// 相机坐标
        /// </summary>
        public List<PointF> Cam { get; set; }=new List<PointF>();
        /// <summary>
        /// 机器人坐标
        /// </summary>
        public List<PointF> Robot { get; set; }= new List<PointF>();

        /// <summary>
        /// 放射变换模型
        /// </summary>
        public HTuple HomMat2D = null;


        /// <summary>
        /// 运行标定
        /// </summary>
        /// <exception cref="Exception"></exception>
        public void Run()
        {
            try
            {
                HTuple cam_x = (HTuple)Cam.Select(s => s.X);
                HTuple cam_y = (HTuple)Cam.Select(s => s.Y);
                HTuple robot_x = (HTuple)Robot.Select(s => s.X);
                HTuple robot_y = (HTuple)Robot.Select(s => s.Y);

                HOperatorSet.VectorToHomMat2d(cam_x, cam_y, robot_x, robot_y, out HomMat2D);
            }
            catch (Exception ex)
            {
                string msg = ex.Message + ex.StackTrace;
                throw new Exception(msg);
            }
        }

        /// <summary>
        /// 相机坐标转换
        /// </summary>
        /// <param name="pCamPoint">相机坐标系</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public PointF Transform(PointF pCamPoint)
        {
            if (HomMat2D == null)
                throw new Exception("Not calibrated!!");
            try
            {
                HTuple camx = (HTuple)pCamPoint.X;
                HTuple camy = (HTuple)pCamPoint.Y;
                HTuple transformx, transformy;
                HOperatorSet.AffineTransPoint2d(HomMat2D, camx, camy, out transformx, out transformy);
                return new PointF(transformx, transformy);
            }
            catch (Exception ex)
            {
                string msg = ex.Message + ex.StackTrace;
                throw new Exception(msg);
            }
        }
    }
}

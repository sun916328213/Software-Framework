using Device.Camera.Definition;
using System;
using System.Collections.Generic;

namespace Device.Camera
{
    public interface ICamera:IDevice
    {
        /// <summary>
        /// 相机是否连接
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 是否实时
        /// </summary>
        bool IsLive { get; set; }
        /// <summary>
        /// 相机的曝光时间,单位ms
        /// </summary>
        int Exposure { get; set; }
        /// <summary>
        /// 相机的增益
        /// </summary>
        double Gain { get; set; }
 
        /// <summary>
        /// 重新相机连接
        /// </summary>
        bool ReConnect();
        /// <summary>
        /// 相机采集图像
        /// </summary>
        void Snap();
        /// <summary>
        /// 相机实时拍照
        /// </summary>
        void Live();
        /// <summary>
        /// 开启实时
        /// </summary>
        /// <param name="ShowImageAction">显示图像委托</param>
        /// <param name="DelayTime">延时时间</param>
        void StartLive(Action ShowImageAction, int DelayTime = 100);
        /// <summary>
        /// 停止实时
        /// </summary>
        void StopLive();
        /// <summary>
        /// 获取图像
        /// </summary>
        /// <returns></returns>
        ImageData GetImage();
        /// <summary>
        /// 获取通道中数据数量
        /// </summary>
        int GetImageCount {  get; }
    }
}

using Device.Camera.Definition;
using Device.Camera.Gige.Definition;
using MvCamCtrl.NET;
using MvCamCtrl.NET.CameraParams;
using MVSDK_Net;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace Device.Camera.Gige.Hik
{
    internal class HikHelper : GigeCameraBase
    {
        cbExceptiondelegate ConnectCallback;

        cbOutputExdelegate ImageCallback;

        private CCamera _cam = new CCamera();

        private CCameraInfo _device = new CCameraInfo();


        protected override bool ConnectRefactor()
        {
            InitCam();
            return ReConnect();
        }

        protected override void DisConnectRefactor()
        {
            if (_cam != null)
            {
                StopGrab();
                // ch:关闭设备 | en:Close Device
                _cam.CloseDevice();
                _cam.DestroyHandle();                                           // 关闭相机 | close camera 
            }
            _isConnected = false;
        }

        public override bool ReConnect()
        {
            _isConnected = false;
            Open();

            SetTriggerMode(CamParam.TriggerMode);

            ConnectCallback = new cbExceptiondelegate(ConnectCallbackFunc);
            int nRet = _cam.RegisterExceptionCallBack(ConnectCallback, IntPtr.Zero);

            // ch:注册回调函数 | en:Register image callback
            ImageCallback = new cbOutputExdelegate(ImageCallbackFunc);
            nRet = _cam.RegisterImageCallBackEx(ImageCallback, IntPtr.Zero);
            if (CErrorDefine.MV_OK != nRet)
            {
                return false;
            }
           
            StarGrab();
            _isConnected = true;
            return true;
        }

        protected override void SetExposure(int exposure)
        {
            CamParam.Exposure = exposure;
            exposure *= 1000;
            int nRet = CErrorDefine.MV_OK;
            // 获取可设置增益 
            CFloatValue pcFloatValue = new CFloatValue();

            nRet = _cam.GetFloatValue("ExposureTime", ref pcFloatValue);

            // 设置增益 
            _cam.SetEnumValue("ExposureAuto", 0);
            nRet = _cam.SetFloatValue("ExposureTime", System.Convert.ToSingle(exposure));
            if (nRet != CErrorDefine.MV_OK)
                throw new Exception("Set Exposure Fail!");
        }

        protected  override void SetGain(double gain)
        {
            CamParam.Gain = gain  ;
            int nRet = CErrorDefine.MV_OK;
            // 获取可设置增益 
            CFloatValue pcFloatValue = new CFloatValue();

            nRet = _cam.GetFloatValue("Gain", ref pcFloatValue);

            // 设置增益 
            _cam.SetEnumValue("GainAuto", 0);
            nRet = _cam.SetFloatValue("Gain", System.Convert.ToSingle(gain));
            if (nRet != CErrorDefine.MV_OK)
                throw new Exception("Set Gain Fail!");
        }

        public override void SetTriggerMode(ETriggerMode mode)
        {
            int res = CErrorDefine.MV_OK;
            switch (mode)
            {
                case ETriggerMode.OFF:
                    _cam.SetEnumValue("TriggerMode", (uint)MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_OFF);
                    break;
                case ETriggerMode.Software:
                    _cam.SetEnumValue("TriggerMode", (uint)MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_ON);
                    res = _cam.SetEnumValue("TriggerSource", (uint)MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_SOFTWARE);
                    break;
                case ETriggerMode.Frame_Line1:
                    _cam.SetEnumValue("TriggerMode", (uint)MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_ON);
                    res = _cam.SetEnumValue("TriggerSource", (uint)MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_LINE0);
                    break;
                case ETriggerMode.Frame_Line2:
                    _cam.SetEnumValue("TriggerMode", (uint)MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_ON);
                    res = _cam.SetEnumValue("TriggerSource", (uint)MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_LINE2);
                    break;
                default:
                    throw new Exception("TriggerMode is not support!");
            }
            if (CErrorDefine.MV_OK != res)
                throw new Exception("Set Trigger Source failed!");

        }

        protected override void ExecuteTrigger()
        {
            _cam.SetCommandValue("TriggerSoftware");
        }

        /// <summary>
        /// 初始化相机
        /// </summary>
        /// <exception cref="Exception"></exception>
        private void InitCam()
        {
            int nRet = CErrorDefine.MV_OK;
            //初始化设备
            List<CCameraInfo> m_ltDeviceList = new List<CCameraInfo>();
            nRet = CSystem.EnumDevices(CSystem.MV_GIGE_DEVICE | CSystem.MV_USB_DEVICE, ref m_ltDeviceList);
            if (0 != nRet)
                throw new Exception("Enumerate devices fail!");

            // ch:在窗体列表中显示设备名 | en:Display device name in the form list
            for (int i = 0; i < m_ltDeviceList.Count; i++)
            {
                if (m_ltDeviceList[i].nTLayerType == CSystem.MV_GIGE_DEVICE)
                {
                    CGigECameraInfo gigeInfo = (CGigECameraInfo)m_ltDeviceList[i];
                    uint nIp1 = ((gigeInfo.nCurrentIp & 0xff000000) >> 24);
                    uint nIp2 = ((gigeInfo.nCurrentIp & 0x00ff0000) >> 16);
                    uint nIp3 = ((gigeInfo.nCurrentIp & 0x0000ff00) >> 8);
                    uint nIp4 = (gigeInfo.nCurrentIp & 0x000000ff);

                    string ip = nIp1 + "." + nIp2 + "." + nIp3 + "." + nIp4;
                    if (CamParam.IP == ip)
                    {
                        _device = m_ltDeviceList[i];
                        break;
                    }
                }
                else if (m_ltDeviceList[i].nTLayerType == CSystem.MV_USB_DEVICE)
                {
                    CUSBCameraInfo usbInfo = (CUSBCameraInfo)m_ltDeviceList[i];
                }
            }
            // ch:创建设备 | en:Create device
            nRet = _cam.CreateHandle(ref _device);
            if (0 != nRet)
                throw new Exception("Failed to create handle");
        }

        private void Open()
        {
            _cam?.CloseDevice();
            // 打开设备 
            int nRet = _cam.OpenDevice();
            if (CErrorDefine.MV_OK != nRet)
            {
                throw new Exception("Open device fail!");
            }
            // ch:探测网络最佳包大小(只对GigE相机有效) | en:Detection network optimal package size(It only works for the GigE camera)
            if (_device.nTLayerType == CSystem.MV_GIGE_DEVICE)
            {
                int nPacketSize = _cam.GIGE_GetOptimalPacketSize();
                if (nPacketSize > 0)
                    nRet = _cam.SetIntValue("GevSCPSPacketSize", (uint)nPacketSize);
            }
        }

        /// <summary>
        /// 开始采集
        /// </summary>
        private void StarGrab()
        {
            // ch:开始采集 | en:Start Grabbing
            int nRet = _cam.StartGrabbing();
            if (CErrorDefine.MV_OK != nRet)
                throw new Exception("Start Grabbing Fail!");
        }

        /// <summary>
        /// 关闭取流
        /// </summary>
        private void StopGrab()
        {
            SetTriggerMode(ETriggerMode.OFF);
            // ch:停止采集 | en:Stop Grabbing
            int nRet = _cam.StopGrabbing();
            if (nRet != CErrorDefine.MV_OK)
            {
                throw new Exception("Stop Grabbing Fail!");
            }
        }

        /// <summary>
        /// 连接回调函数
        /// </summary>
        /// <param name="nMsgType"></param>
        /// <param name="pUser"></param>
        private void ConnectCallbackFunc(uint nMsgType, IntPtr pUser)
        {
            if (nMsgType == 32769)
            {
                while (true)
                {
                    Thread.Sleep(200);
                    if (ReConnect())
                        return;
                }
            }
        }
        /// <summary>
        /// 数据帧回调函数
        /// </summary>
        /// <param name="pData"></param>
        /// <param name="pFrameInfo"></param>
        /// <param name="pUser"></param>
        private void ImageCallbackFunc(IntPtr pData, ref MV_FRAME_OUT_INFO_EX pFrameInfo, IntPtr pUser)
        {
            ImageData imageData = new ImageData();
            imageData.Width = pFrameInfo.nWidth;
            imageData.Height = pFrameInfo.nHeight;
            imageData.ImageType = CamParam.ImageType;

            // 深拷贝图像数据到托管byte[]（GC自动回收，无需手动释放）
            int size = (int)pFrameInfo.nFrameLen > 0 ? (int)pFrameInfo.nFrameLen : pFrameInfo.nWidth * pFrameInfo.nHeight;
            byte[] buffer = new byte[size];
            Marshal.Copy(pData, buffer, 0, size);
            imageData.RawData = buffer;
 
            ImagesChannel.Write(imageData);
            // 图像采集完成，释放信号量
            _autoResetEvent.Set();
        }
 
    }
}

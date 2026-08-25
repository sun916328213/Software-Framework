using Device.Camera.Definition;
using Device.Camera.Gige.Definition;
using MVSDK_Net;
using System;
using System.Runtime.InteropServices;

namespace Device.Camera.Gige.Dahua
{
    internal class DaHuaHelper : GigeCameraBase
    {
        IMVDefine.IMV_ConnectCallBack ConnectCallBack;
        IMVDefine.IMV_FrameCallBack frameCallBack;
        MyCamera _cam = new MyCamera();
        int LastStatus = -1;
 
        protected override bool ConnectRefactor()
        {
            InitCam();
            Open();
            return ReConnect();
        }

        protected override void DisConnectRefactor()
        {
            ImagesChannel.Clear();
            if (_cam != null)
            {
                if (_cam.IMV_IsGrabbing())
                {
                    StopGrab();
                }
                _cam.IMV_Close();                                                // 关闭相机 | close camera 
                _cam.IMV_DestroyHandle();
            }  
        }

        public override bool ReConnect()
        {
            _isConnected = false;
            int nRet = _cam.IMV_SetBufferCount(8);
 
            // 设备连接状态事件回调函数
            ConnectCallBack = onDeviceLinkNotify;
            nRet = _cam.IMV_SubscribeConnectArg(ConnectCallBack, IntPtr.Zero);

            // 注册数据帧回调函数
            // Register data frame callback function
            frameCallBack = onGetFrame;
            nRet = _cam.IMV_AttachGrabbing(frameCallBack, IntPtr.Zero);
            SetTriggerMode(CamParam.TriggerMode);
            StarGrab();
            _isConnected = true;
            return true;
        }

        protected override void ExecuteTrigger()
        {
            _cam.IMV_ExecuteCommandFeature("TriggerSoftware");
        }

        protected override void SetExposure(int exposure)
        {
            CamParam.Exposure = exposure  ;
            exposure *= 1000;
            int res = IMVDefine.IMV_OK;

            res = _cam.IMV_SetDoubleFeatureValue("ExposureTime", exposure);
            if (IMVDefine.IMV_OK != res)
                throw new Exception("set exposuretime fail!");
        }

        protected override void SetGain(double gain)
        {
            CamParam.Gain = gain;
            int res = IMVDefine.IMV_OK;
            // 设置增益 
            res = _cam.IMV_SetDoubleFeatureValue("Gain", gain);
            if (IMVDefine.IMV_OK != res)
                throw new Exception("set gain fail!");
        }

        public override void SetTriggerMode(ETriggerMode mode)
        {
            int res = IMVDefine.IMV_OK;
            switch (mode)
            {
                case ETriggerMode.OFF:
                    _cam.IMV_SetEnumFeatureSymbol("TriggerMode", "Off");
                    break;
                case ETriggerMode.Software:
                    _cam.IMV_SetEnumFeatureSymbol("TriggerMode", "On");
                    res = _cam.IMV_SetEnumFeatureSymbol("TriggerSource", "Software");
                    break;
                case ETriggerMode.Frame_Line1:
                    _cam.IMV_SetEnumFeatureSymbol("TriggerMode", "On");
                    res = _cam.IMV_SetEnumFeatureSymbol("TriggerSource", "Line1");
                    break;
                case ETriggerMode.Frame_Line2:
                    _cam.IMV_SetEnumFeatureSymbol("TriggerMode", "On");
                    res = _cam.IMV_SetEnumFeatureSymbol("TriggerSource", "Line2");
                    break;
                default:
                    throw new Exception("TriggerMode is not support!");
            }
            if (IMVDefine.IMV_OK != res)
                throw new Exception("Set Trigger Source failed!");
        }

        private void InitCam()
        {
            int res = IMVDefine.IMV_OK;

            //初始化设备
            IMVDefine.IMV_DeviceList deviceList = new IMVDefine.IMV_DeviceList();
            IMVDefine.IMV_EInterfaceType interfaceTp = IMVDefine.IMV_EInterfaceType.interfaceTypeAll;
            res = MyCamera.IMV_EnumDevices(ref deviceList, (uint)interfaceTp);
            // 创建设备句柄
            res = _cam.IMV_CreateHandle(IMVDefine.IMV_ECreateHandleMode.modeByIPAddress, 0, CamParam.IP);
            if (res != IMVDefine.IMV_OK)
                throw new Exception("Failed to create handle");

        }

        private void Open()
        {
            _cam.IMV_Close();
            int res = _cam.IMV_Open();
            if (res != IMVDefine.IMV_OK)
                throw new Exception("Failed to open device");
        }

        // 连接事件通知回调函数
        void onDeviceLinkNotify(ref IMVDefine.IMV_SConnectArg pConnectArg, IntPtr pUser)
        {
            if ((int)pConnectArg.EvType != LastStatus)
            {
                // 断线通知
                if (IMVDefine.IMV_EVType.offLine == pConnectArg.EvType)
                {

                }

                // 上线通知
                else if (IMVDefine.IMV_EVType.onLine == pConnectArg.EvType)
                {
                    ReConnect();
                }
            }
            LastStatus = (int)pConnectArg.EvType;
        }

        // 数据帧回调函数
        private void onGetFrame(ref IMVDefine.IMV_Frame frame, IntPtr pUser)
        {
            ImageData imageData = new ImageData();
            imageData.Width = (int)frame.frameInfo.width;
            imageData.Height = (int)frame.frameInfo.height;
            imageData.ImageType = CamParam.ImageType;

            // 深拷贝图像数据到托管byte[]，避免SDK buffer被回收后数据失效
            int size = (int)frame.frameInfo.size > 0 ? (int)frame.frameInfo.size : imageData.Width * imageData.Height;
            byte[] buffer = new byte[size];
            Marshal.Copy(frame.pData, buffer, 0, size);
            imageData.RawData = buffer;

            ImagesChannel.Write(imageData);
            // 图像采集完成，释放信号量
            _autoResetEvent.Set();
        }

        /// <summary>
        /// 开始取流
        /// </summary>
        private void StarGrab()
        {
            // 开启码流 
            if (!_cam.IMV_IsGrabbing())
            {
                if (IMVDefine.IMV_OK != _cam.IMV_StartGrabbing())
                    throw new Exception("Start grabbing failed");
            }
        }

        /// <summary>
        /// 关闭取流
        /// </summary>
        private void StopGrab()
        {
            SetTriggerMode(ETriggerMode.OFF);
            // 关闭码流 
            // start grabbing 
            if (IMVDefine.IMV_OK != _cam.IMV_StopGrabbing())
                throw new Exception("Stop grabbing failed");
        }
    }
}

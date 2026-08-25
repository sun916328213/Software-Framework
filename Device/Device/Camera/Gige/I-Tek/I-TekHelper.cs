using Device.Camera.Definition;
using Device.Camera.Gige;
using Device.Camera.Gige.Definition;
using Device.Camera.Gige.I_Tek;
using IKapC.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Device.Camera.Gige.I_Tek
{
    internal class I_TekManager : GigeCameraBase
    {
        IKapCCamera _cam = new IKapCCamera();
 
        Dictionary<uint, IKapCLib.ITKGIGEDEV_INFO> _Infos=new Dictionary<uint, IKapCLib.ITKGIGEDEV_INFO> ();


        protected override bool ConnectRefactor()
        {
            InitCam();
            return true;
        }

        protected override void DisConnectRefactor()
        {
            StopGrab();
        }

        protected override void ExecuteTrigger()
        {
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;
            res = IKapCLib.ItkDevExecuteCommand(_cam.m_hCamera, "TriggerSoftware");
        }

        public override bool ReConnect()
        {
            DisConnect();
            InitCam();
            return true;
        }

        protected override void SetExposure(int exposure)
        {
            SetParam("ExposureTime", Exposure);
        }

        protected override void SetGain(double gain)
        {
            SetParam("DigitalGain", Gain);
        }

        public override void SetTriggerMode(ETriggerMode mode)
        {
            bool res;
            switch (mode)
            {
                case ETriggerMode.OFF:
                    res = SetParam(new string[] { "TriggerSelector", "TriggerMode" }, new string[] { "LineStart", "Off" });
                    res = SetParam(new string[] { "TriggerSelector", "TriggerMode" }, new string[] { "FrameStart", "Off" });
                    break;
                case ETriggerMode.Software:
                    res = SetParam(new string[] { "TriggerSelector", "TriggerMode" }, new string[] { "LineStart", "Off" });
                    res = SetParam(new string[] { "TriggerSelector", "TriggerMode", "TriggerSource" }, new string[] { "FrameStart", "On", "Software" });
                    break;
                case ETriggerMode.Frame_Line1:
                    res = SetParam(new string[] { "TriggerSelector", "TriggerMode" }, new string[] { "LineStart", "Off" });
                    res = SetParam(new string[] { "TriggerSelector", "TriggerMode", "TriggerSource" }, new string[] { "FrameStart", "On", "Line3" });
                    break;
                case ETriggerMode.Frame_Encoder:
                    res = SetParam(new string[] { "TriggerSelector", "TriggerMode", "TriggerSource" }, new string[] { "LineStart", "On", "RotaryEncoder1" });
                    res = SetParam(new string[] { "TriggerSelector", "TriggerMode", "TriggerSource" }, new string[] { "FrameStart", "On", "Line3" });
                    break;
            }
        }

        private void InitCam()
        {
            //初始化环境
            if (!IKapCCamera.InitEnvironment())
                throw new Exception("Init environment failure");

            //获取相机的所有信息
            _Infos = IKapCCamera.GetCameraInfo();

            for (uint Index = 0; Index < _Infos.Count; Index++)
            {
                if (_Infos.Values.ElementAt((int)Index).Ip == CamParam.IP)
                {
                    // 打开 GigECamera 相机。
                    var bReturn = _cam.OpenDevice(_Infos.Keys.ElementAt((int)Index));
                    if (!bReturn) throw new System.Exception("opne device failure");

                    ///注册相机掉线事件
                    uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;
                    res = IKapCLib.ItkDevRegisterCallback(_cam.m_hCamera, "DeviceRemove", removalCallbackProc, _cam.m_hCamera);

                    _cam.OnGrabOver += Camera_OnGrabOver;
                    SetTriggerMode(CamParam.TriggerMode);
                    StartGrab();
                }
            }
        }

        /// <summary>
        /// 掉线事件
        /// </summary>
        /// <param name="context"></param>
        /// <param name="eventInfo"></param>
        private void removalCallbackProc(IntPtr context, IntPtr eventInfo)
        {
            IntPtr type = Marshal.AllocHGlobal(4);
            IntPtr countstamp = Marshal.AllocHGlobal(8);
            IKapCLib.ItkEventInfoGetPrm(eventInfo, (uint)ItkEventInfoPrm.ITKEVENTINFO_PRM_TYPE, type);

            DisConnect ();
            InitCam();
        }

        private void Camera_OnGrabOver(IntPtr intPtr)
        {
            ImageData imageData = new ImageData();
            imageData.Width = (int)_cam.Width;
            imageData.Height = (int)_cam.Height;
            imageData.ImageType = CamParam.ImageType;

            // 深拷贝图像数据到托管byte[]，避免SDK buffer被回收后数据失效
            int size = _cam.CurrentFrameSize > 0 ? _cam.CurrentFrameSize : (int)_cam.Width * (int)_cam.Height;
            byte[] buffer = new byte[size];
            Marshal.Copy(intPtr, buffer, 0, size);
            imageData.RawData = buffer;

            ImagesChannel.Write(imageData);
            // 图像采集完成，释放信号量
            _autoResetEvent.Set();
        }

        /// <summary>
        /// 开启数据流
        /// </summary>
        /// <returns></returns>
        private bool StartGrab()
        {
            //创建缓冲区
            var bReturn = _cam.CreateStreamAndBuffer();
            if (!bReturn) return false;
            //开启数据流
            bReturn = _cam.StartStream((uint)IKapCLib.ITKSTREAM_CONTINUOUS);
            if (!bReturn) return false;

            return true;
        }

        /// <summary>
        /// 关闭数据流
        /// </summary>
        /// <returns></returns>
        private bool StopGrab()
        {
            _cam.StopStream();
            _cam.ClearStreamAndBuffer();
            return true;
        }

        /// <summary>
        /// 设置相机参数
        /// </summary>
        /// <param name="pParamName">需要修改参数的名称</param>
        /// <param name="pParamValue">需要修改参数的数据</param>
        /// <returns></returns>
        private bool SetParam(object pParamName, object pParamValue)
        {
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;

            switch (pParamValue)
            {
                case int _:
                    long val = Convert.ToInt64(pParamValue);
                    res = IKapCLib.ItkDevSetInt64(_cam.m_hCamera, (string)pParamName, val);
                    break;
                case double _:
                    res = IKapCLib.ItkDevSetDouble(_cam.m_hCamera, (string)pParamName, (double)pParamValue);
                    break;
                case string _:
                    res = IKapCLib.ItkDevFromString(_cam.m_hCamera, (string)pParamName, (string)pParamValue);
                    break;
                case string[] _:
                    string[] keys = (string[])pParamName;
                    string[] values = (string[])pParamValue;
                    for (int index = 0; index < values.Length; index++)
                    {
                        string key = keys[index], value = values[index];
                        res = IKapCLib.ItkDevFromString(_cam.m_hCamera, key, value);
                    }
                    break;
            }
            return CheckIKapC(res);
        }
      private  bool CheckIKapC(uint res)
        {
            string code = res.ToString("x8");
            if (res != (uint)ItkStatusErrorId.ITKSTATUS_OK) return false;
            else return true;
        }
    }
}

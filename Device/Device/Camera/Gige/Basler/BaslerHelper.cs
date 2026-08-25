using Basler.Pylon ;
using Device.Camera.Definition;
using Device.Camera.Gige.Definition;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

using BaslerPylon = Basler.Pylon;

namespace Device.Camera.Gige.Basler
{
    internal class BaslerHelper : GigeCameraBase
    {
        BaslerPylon.Camera _cam = new BaslerPylon.Camera();

        Dictionary<string, ICameraInfo> _foundCameraInfos = new Dictionary<string, ICameraInfo>();
 
        protected  override bool ConnectRefactor()
        {
            _foundCameraInfos = InitCam();
            return ReConnect();
        }

        protected override void DisConnectRefactor()
        {
            if (_cam != null)
            {
                _cam.StreamGrabber.Stop();

                SetTriggerMode(ETriggerMode.OFF);
                _cam.Close();
                _cam.Dispose();
                _cam = null;
            }
            _isConnected = false;
        }

        public override bool ReConnect()
        {
            ICameraInfo stDevInfo = _foundCameraInfos[CamParam.IP];
            _cam = new BaslerPylon.Camera(stDevInfo);
            _cam.Close();
            _cam.ConnectionLost += OnConnectionLost;
            _cam.StreamGrabber.ImageGrabbed += OnImageGrabbed;
            _cam.Open();

            SetTriggerMode(CamParam.TriggerMode);
            _cam.Parameters[PLCamera.AcquisitionMode].SetValue(PLCamera.AcquisitionMode.Continuous);
            _cam.StreamGrabber.Start(GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);
            return _isConnected = true;
        }


        protected override void ExecuteTrigger()
        {
            // 执行软件触发
            _cam.ExecuteSoftwareTrigger();
        }

        public override void SetTriggerMode(ETriggerMode mode)
        {
            switch (mode)
            {
                case ETriggerMode.OFF:
                    _cam.Parameters[PLCamera.TriggerMode].TrySetValue(PLCamera.TriggerMode.Off);
                    break;
                case ETriggerMode.Software:
                    _cam.Parameters[PLCamera.TriggerMode].TrySetValue(PLCamera.TriggerMode.On);
                    _cam.Parameters[PLCamera.TriggerSource].TrySetValue(PLCamera.TriggerSource.Software);
                    break;
                case ETriggerMode.Frame_Line1:
                    _cam.Parameters[PLCamera.TriggerMode].TrySetValue(PLCamera.TriggerMode.On);
                    _cam.Parameters[PLCamera.TriggerSource].TrySetValue(PLCamera.TriggerSource.Line1);
                    break;
                case ETriggerMode.Frame_Line2:
                    _cam.Parameters[PLCamera.TriggerMode].TrySetValue(PLCamera.TriggerMode.On);
                    _cam.Parameters[PLCamera.TriggerSource].TrySetValue(PLCamera.TriggerSource.Line1);
                    break;
                default:
                    throw new Exception("TriggerMode is not support!");
            }
        }

        protected override void SetExposure(int exposure)
        {
            IFloatParameter p;
            if (_cam.Parameters.Contains(PLCamera.ExposureTimeAbs))
                p = _cam.Parameters[PLCamera.ExposureTimeAbs];
            else
                p = _cam.Parameters[PLCamera.ExposureTime];

            CamParam.Exposure = exposure;
            exposure *= 1000;

            if (p.GetValue() != exposure)
                p.SetValue(exposure);
        }


        protected override void SetGain(double gain)
        {
            IFloatParameter p;
            p = _cam.Parameters[PLCamera.GainAbs];
            CamParam.Gain = gain;

            if (p.GetValue() != gain)
                p.SetValue(gain);
        }

        protected void OnConnectionLost(object sender, EventArgs e)
        {
            DisConnect();
            while (true)
            {
               if( ReConnect())
                    break;
                Thread.Sleep(1000);
            }

        }

        private   void OnImageGrabbed(Object sender, ImageGrabbedEventArgs e)
        {
            IGrabResult grabResult = e.GrabResult;

            if (grabResult.IsValid)
            {
                ImageData imageData = new ImageData();
                imageData.Width = grabResult.Width;
                imageData.Height = grabResult.Height;
                imageData.ImageType =CamParam. ImageType;

                // 深拷贝图像数据到托管byte[]，避免SDK buffer被回收后数据失效
                int size = (int)grabResult.PayloadSize > 0 ? (int)grabResult.PayloadSize : grabResult.Width * grabResult.Height;
                byte[] buffer = new byte[size];
                Marshal.Copy(grabResult.PixelDataPointer, buffer, 0, size);
                imageData.RawData = buffer;

                ImagesChannel.Write(imageData);
            }
            // 图像采集完成，释放信号量
            _autoResetEvent.Set();
        }

        /// <summary>
        /// 初始化 查找相机
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, ICameraInfo> InitCam()
        {
            Dictionary<string, ICameraInfo> foundCameraInfos = new Dictionary<string, ICameraInfo>();
            List<ICameraInfo> allCameras = CameraFinder.Enumerate();
            if (allCameras.Count > 0)
            {
                // Maps the camera name to the camera info for use with the combo box.
                foreach (ICameraInfo cameraInfo in allCameras)
                {
                    foundCameraInfos.Add(cameraInfo[CameraInfoKey.DeviceIpAddress], cameraInfo);
                }
            }
            return foundCameraInfos;
        }
    }
}

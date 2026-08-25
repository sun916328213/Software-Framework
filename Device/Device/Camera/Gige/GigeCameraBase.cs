using Device.Camera.Definition;
using Device.Camera.Gige.Definition;
using Extension.Core.ControlEx;
using Extension.Core.SystemEx;
using Extension.Core.TaskEx;
using Extension.Helper.Channel;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Device.Camera.Gige
{
  
    public abstract class GigeCameraBase : ICamera
    {
        protected readonly AutoResetEvent _autoResetEvent = new AutoResetEvent(false);
        // 添加FPS计算器
        protected readonly FpsEx _fpsCalculator = new FpsEx(10);

        protected CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        /// <summary>
        /// 图像集合通道
        /// </summary>
        public virtual ChannelHelper<ImageData> ImagesChannel { get; set; } = new ChannelHelper<ImageData>();
        /// <summary>
        /// 相机参数
        /// </summary>
        public GigeCamParam CamParam { get; set; }

        public string Name
        {
            get => CamParam.Name;
            set => CamParam.Name = value;
        }

        internal bool _isConnected;
        public bool IsConnected => _isConnected;

        internal bool _isLive;
        public bool IsLive
        {
            get => _isLive;
            set => StopLive();
        }

       
        public virtual int Exposure
        {
            get => CamParam.Exposure;
            set 
            {
                CamParam.Exposure = value;
                SetExposure(value);
            } 
        }

      
        public virtual double Gain
        {
          get => CamParam.Gain;
          set 
                {
                CamParam.Gain = value;
                SetGain( value);
            }
        }

        public int GetImageCount => ImagesChannel.Reader.Count;

        public bool Connect()
        {
            if (CamParam == null)
                throw new Exception("CamPara not set");

            ConnectRefactor();

            SetExposure(Exposure);
            SetGain(Gain);
            return true;
        }

        protected abstract bool ConnectRefactor();

        public bool DisConnect()
        {
            DisConnectRefactor();
            ImagesChannel.Dispose();
            return true;
        }

        protected abstract void DisConnectRefactor();

        public abstract bool ReConnect();

        public virtual ImageData GetImage()
        { 
          if (ImagesChannel.TryRead(out var image))
                return image;
            return default;
        }



        public virtual void Snap()
        {
            _isLive = false;

            Reset();
            //复位
            _autoResetEvent.Reset();
            //执行触发
            ExecuteTrigger();
            //等待
            // 等待图像采集完成
            if (!_autoResetEvent.WaitOne(CamParam.TimeOut))
                throw new TimeoutException("Snap TimeOut");
        }

        public virtual void Live()
        {
            //复位
            _autoResetEvent.Reset();
            //执行触发
            ExecuteTrigger();
            //等待
            // 等待图像采集完成
            if (!_autoResetEvent.WaitOne(CamParam.TimeOut))
                throw new TimeoutException("Snap TimeOut");
        }

        protected abstract void ExecuteTrigger();

        public virtual void StartLive(Action ShowImageAction, int DelayTime = 100)
        {
            Reset();
            cancellationTokenSource = new CancellationTokenSource();
            _isLive = true;
            // 重置帧率计算
            _fpsCalculator.Reset();
        
            SetTriggerMode(ETriggerMode.Software);
            Action action = new Action(() =>
            {
                Live();
                // 更新帧率计算
                _fpsCalculator.Tick();

                ShowImageAction.UIInvoke();
            });
            _ = TaskEx.LongTimeRun(action, DelayTime, cancellationTokenSource.Token);
        }

        public virtual void StopLive()
        {
            Reset();
            _fpsCalculator.Stop();
            SetTriggerMode(CamParam.TriggerMode);
            _isLive = false;
        }

        protected abstract void SetExposure(int exposure);

        protected abstract void SetGain(double gain);

        public abstract void SetTriggerMode(ETriggerMode mode);


        private void Reset()
        {
            cancellationTokenSource.Cancel();
            ImagesChannel.Clear();
        }
    }
}

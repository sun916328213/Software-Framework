using Device.CamLinkCard.Euresys.MultiCam;
using System;
using System.Threading;

namespace Device.CamLinkCard.Euresys
{
    public class EuresysHelper : CamLinkCardBase
    {
        private UInt32 channel;
        private MC.CALLBACK multiCamCallback;
        private UInt32 currentSurface;
        private Mutex imageMutex = new Mutex();
        public override string Name { get  ; set  ; }

        public override event GrabOverHandler OnGrabOver;

        public override   bool DisConnect()
        {
            Stop();
            // Delete the channel
            if (channel != 0)
            {
                MC.Delete(channel);
                channel = 0;
            }
            return true;
        }

        public override bool Connect(out string pErrorInfo, string pPath_Config)
        {
            pErrorInfo = null;
            MC.OpenDriver();

            // Enable error logging
            MC.SetParam(MC.CONFIGURATION, "ErrorLog", "error.log");

            // In order to support a 10-tap camera on Grablink Full
            // BoardTopology must be set to MONO_DECA
            // In all other cases the default value will work properly 
            // and the parameter doesn't need to be set

            // Set the board topology to support 10 taps mode (only with a Grablink Full)
            // MC.SetParam(MC.BOARD + 0, "BoardTopology", "MONO_DECA");

            // Create a channel and associate it with the first connector on the first board
            MC.Create("CHANNEL", out channel);
            MC.SetParam(channel, "DriverIndex", 0);

            // In order to use single camera on connector A
            // MC_Connector must be set to A for Grablink DualBase
            // For all other Grablink boards the parameter has to be set to M  

            // For all Grablink boards except Grablink DualBase
            //MC.SetParam(channel, "Connector", "M");
            // For Grablink DualBase
            MC.SetParam(channel, "Connector", "A");

            // Choose the CAM file
            MC.SetParam(channel, "CamFile", pPath_Config);

            ////设置采集行数
            //MC.SetParam(channel, "PageLength_Ln", 2300);

            //// Choose the camera expose duration
            //MC.SetParam(channel, "TapConfiguration", "BASE_1T16");
            //// Choose the pixel color format
            //MC.SetParam(channel, "ColorFormat", "Y16");

            ////Set the acquisition mode to Snapshot
            //MC.SetParam(channel, "AcquisitionMode", "WEB");
            //// Choose the way the first acquisition is triggered
            //MC.SetParam(channel, "TrigMode", "IMMEDIATE");
            //// Choose the triggering mode for subsequent acquisitions
            //MC.SetParam(channel, "NextTrigMode", "REPEAT");

            // Choose the number of images to acquire 设置采集次数
            MC.SetParam(channel, "SeqLength_Fr", MC.INDETERMINATE);

            // Register the callback function
            multiCamCallback = new MC.CALLBACK(MultiCamCallback);
            MC.RegisterCallback(channel, multiCamCallback, channel);

            // Enable the signals corresponding to the callback functions
            MC.SetParam(channel, MC.SignalEnable + MC.SIG_SURFACE_PROCESSING, "ON");
            MC.SetParam(channel, MC.SignalEnable + MC.SIG_ACQUISITION_FAILURE, "OFF");

            // Prepare the channel in order to minimize the acquisition sequence startup latency
            MC.SetParam(channel, "ChannelState", "READY");

            Start();
            return true;
        }

        /// <summary>
        /// 开启采集
        /// </summary>
       private  void Start()
        {
            String channelState;
            MC.GetParam(channel, "ChannelState", out channelState);
            if (channelState != "ACTIVE")
                MC.SetParam(channel, "ChannelState", "ACTIVE");

            MC.SetParam(channel, "ForceTrig", "TRIG");

        }

        /// <summary>
        /// 关闭采集
        /// </summary>
        private void Stop()
        {
            if (channel != 0)
                MC.SetParam(channel, "ChannelState", "IDLE");
        }

        /// <summary>
        /// camlink采集卡采集完成回调事件
        /// </summary>
        /// <param name="signalInfo"></param>
        private void MultiCamCallback(ref MC.SIGNALINFO signalInfo)
        {
            switch (signalInfo.Signal)
            {
                case MC.SIG_SURFACE_PROCESSING:
                    ProcessingCallback(signalInfo);
                    break;
            }
        }

        /// <summary>
        /// 采集完成函数
        /// </summary>
        /// <param name="signalInfo"></param>
        private void ProcessingCallback(MC.SIGNALINFO signalInfo)
        {
            UInt32 currentChannel = (UInt32)signalInfo.Context;

            currentSurface = signalInfo.SignalInfo;

            try
            {
                // Update the image with the acquired image buffer data 
                Int32 width, height, bufferPitch;
                IntPtr bufferAddress;
                MC.GetParam(currentChannel, "ImageSizeX", out width);
                MC.GetParam(currentChannel, "ImageSizeY", out height);
                MC.GetParam(currentChannel, "BufferPitch", out bufferPitch);
                MC.GetParam(currentSurface, "SurfaceAddr", out bufferAddress);

                //HObject hObject = null;
                //HOperatorSet.GenImage1(out hObject, "uint2", (int)width, (int)height, bufferAddress);//取内存数据，生成图像，halcon实现
                //OnGrabOver.Invoke(Name, hObject);
            }
            catch (Euresys.MultiCamException exc)
            {
               throw new Exception ( "MultiCam Exception"+exc.StackTrace);
            }
            catch (System.Exception exc)
            {
                throw new Exception( "System Exception"+exc.StackTrace);
            }
        }
    }
}

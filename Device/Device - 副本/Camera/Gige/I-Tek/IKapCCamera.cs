using IKapC.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Device. Camera.Gige.I_Tek
{
    public delegate void GrabOverHandler(IntPtr intPtr);

    public class IKapCCamera
    {
        public event GrabOverHandler OnGrabOver;
        // 相机设备句柄。
        //
        // Camera device handle.
        public IntPtr m_hCamera = new IntPtr(-1);

        // 数据流句柄。
        //
        // Data stream handle.
        public IntPtr m_hStream = new IntPtr(-1);

        // 图像缓冲区句柄。
        //
        // Image buffer handle.
        List<IntPtr> m_hBufferList = new List<IntPtr>();

        // 保存图像的文件名。
        //
        // File name of image.
        public string m_strFileName = "C:\\CSharpImage.tif";

        // 当前帧索引。
        //
        // Current frame index.
        public int m_nCurFrameIndex = 0;

        // 图像缓冲区申请的帧数。
        //
        // The number of frames requested by buffer.
        public int m_nBufferCountOfStream = 5;

        // 缓冲区数据。
        //
        // Buffer data.
        public IntPtr m_bufferData = new IntPtr(-1);

        // 相机索引。
        //
        // Camera index.
        static public uint m_nGvStartIndex = 0;

        /// <summary>
        /// 图像宽度和高度
        /// </summary>
        public Int64 Width, Height;

        /// <summary>
        /// 当前帧图像数据大小（字节数）
        /// </summary>
        public int CurrentFrameSize { get; set; }

        #region Callback
        /* @brief：本函数被注册为一个回调函数。当数据流开始时，函数被调用。
         *
         * @brief：This function is registered as a callback function. When data stream starts, the function will be called. */
        public IKapCLib.PITKSTREAMCALLBACK cbOnStartOfStreamProc = null;

        /* @brief：本函数被注册为一个回调函数。当一帧图像采集结束时，函数被调用。
         *
         * @brief：This function is registered as a callback function. When grabbing a frame of image finished, the function will be called. */
        public IKapCLib.PITKSTREAMCALLBACK cbOnEndOfFrameProc = null;

        /* @brief：本函数被注册为一个回调函数。当图像采集超时时，函数被调用。
         *
         * @brief：This function is registered as a callback function. When grabbing images time out, the function will be called. */
        public IKapCLib.PITKSTREAMCALLBACK cbOnTimeOutProc = null;

        /* @brief：本函数被注册为一个回调函数。当采集丢帧时，函数被调用。
         *
         * @brief：This function is registered as a callback function. When grabbing frame lost, the function will be called. */
        public IKapCLib.PITKSTREAMCALLBACK cbOnFrameLostProc = null;

        /* @brief：本函数被注册为一个回调函数。当数据流结束时，函数被调用。
         *
         * @brief：This function is registered as a callback function. When data stream ends, the function will be called. */
        public IKapCLib.PITKSTREAMCALLBACK cbOnEndOfStreamProc = null;
        #endregion

        #region Callback
        /* @brief：本函数被注册为一个回调函数。当数据流开始时，函数被调用。
         * @param[in] eventType：事件类型。
         * @param[in] pContext：输入参数。
         *
         * @brief：This function is registered as a callback function. When data stream starts, the function will be called.
         * @param[in] eventType：Event type.
         * @param[in] pContext：Input parameter. */
        public void cbOnStartOfStreamFunc(uint eventType, IntPtr pContext)
        {
            Console.WriteLine("Start of stream");
        }

        /* @brief：本函数被注册为一个回调函数。当一帧图像采集结束时，函数被调用。
         * @param[in] eventType：事件类型。
         * @param[in] pContext：输入参数。
         *
         * @brief：This function is registered as a callback function. When grabbing a frame of image finished, the function will be called.
         * @param[in] eventType：Event type.
         * @param[in] pContext：Input parameter. */
        public void cbOnEndOfFrameFunc(uint eventType, IntPtr pContext)
        {
            Console.WriteLine("End of frame");

            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;
            IntPtr bufferStatus = Marshal.AllocHGlobal(4);
            IntPtr nImageSize = Marshal.AllocHGlobal(8);
            IntPtr hBuffer = m_hBufferList.ElementAt(m_nCurFrameIndex);
            uint nStatus = 0;
            uint nBufferSize = 0;

            res = IKapCLib.ItkBufferGetPrm(hBuffer, (uint)ItkBufferPrm.ITKBUFFER_PRM_STATE, (IntPtr)(bufferStatus));
            if (res != (uint)ItkStatusErrorId.ITKSTATUS_OK)
                return;
            nStatus = (uint)Marshal.ReadInt32(bufferStatus);

            // 当图像缓冲区满或者图像缓冲区非满但是无法采集完整的一帧图像时。
            //
            // When buffer is full or buffer is not full but cannot grab a complete frame of image.
            if (nStatus == (uint)ItkBufferState.ITKBUFFER_VAL_STATE_FULL || nStatus == (uint)ItkBufferState.ITKBUFFER_VAL_STATE_UNCOMPLETED)
            {
                // 保存图像。
                //
                // Save image. 
                /*
                res = IKapCLib.ItkBufferSave(hBuffer, m_strFileName, (uint)ItkBufferSaveType .ITKBUFFER_VAL_TIFF);
                CheckIKapC(res);
                 */

                // 读取缓冲区数据。
                //
                // Read buffer data.
                res = IKapCLib.ItkBufferGetPrm(hBuffer, (uint)ItkBufferPrm.ITKBUFFER_PRM_SIZE, nImageSize);
                if (res != (uint)ItkStatusErrorId.ITKSTATUS_OK)
                    return;
                nBufferSize = (uint)Marshal.ReadInt64(nImageSize);
                res = IKapCLib.ItkBufferRead(hBuffer, 0, m_bufferData, (uint)nBufferSize);
                if (res != (uint)ItkStatusErrorId.ITKSTATUS_OK)
                    return;

                CurrentFrameSize = (int)nBufferSize;
            }
            OnGrabOver?.Invoke(m_bufferData);
            m_nCurFrameIndex++;
            m_nCurFrameIndex = m_nCurFrameIndex % m_nBufferCountOfStream;
        }

        /* @brief：本函数被注册为一个回调函数。当图像采集超时时，函数被调用。
         * @param[in] eventType：事件类型。
         * @param[in] pContext：输入参数。
         *
         * @brief：This function is registered as a callback function. When grabbing images time out, the function will be called.
         * @param[in] eventType：Event type.
         * @param[in] pContext：Input parameter. */
        public void cbOnTimeOutFunc(uint eventType, IntPtr pContext)
        {
            Console.WriteLine("Grab timeout");
        }

        /* @brief：本函数被注册为一个回调函数。当采集丢帧时，函数被调用。
         * @param[in] eventType：事件类型。
         * @param[in] pContext：输入参数。
         *
         * @brief：This function is registered as a callback function. When grabbing frame lost, the function will be called.
         * @param[in] eventType：Event type.
         * @param[in] pContext：Input parameter. */
        public void cbOnFrameLostFunc(uint eventType, IntPtr pContext)
        {
            Console.WriteLine("Grab Frame lost");
        }

        /* @brief：本函数被注册为一个回调函数。当数据流结束时，函数被调用。
         * @param[in] eventType：事件类型。
         * @param[in] pContext：输入参数。
         *
         * @brief：This function is registered as a callback function. When data stream ends, the function will be called.
         * @param[in] eventType：Event type.
         * @param[in] pContext：Input parameter. */
        public void cbOnEndOfStreamFunc(uint eventType, IntPtr pContext)
        {
            Console.WriteLine("End of stream");
        }
        #endregion

        /* @brief：初始化IKapC 运行环境。
         *
         * @brief：Initialize IKapC runtime environment. */
        public static bool InitEnvironment()
        {
            // IKapC 函数返回值。
            //
            // Return value of IKapC functions.
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;

            res = IKapCLib.ItkManInitialize();
            if (res == (uint)ItkStatusErrorId.ITKSTATUS_OK)
                return true;
            return false;
        }

        /* @brief：释放 IKapC 运行环境。
         *
         * @brief：Release IKapC runtime environment. */
        public static void ReleaseEnvironment()
        {
            IKapCLib.ItkManTerminate();
        }

        /* @brief：获取连接的相机数量。
         *
         * Get the number of connected cameras. */
        public static uint GetCameraCount()
        {
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;
            uint numCameras = 0;
            bool bFirst = true;

            // 枚举可用相机的数量。在打开相机前，必须调用 ItkManGetDeviceCount() 函数。
            //
            // Enumerate the number of available cameras. Before opening the camera, ItkManGetDeviceCount() function must be called.
            res = IKapCLib.ItkManGetDeviceCount(ref numCameras);

            // 当没有连接的相机时。
            //
            // When there is no connected cameras.
            if (numCameras == 0 || res != (uint)ItkStatusErrorId.ITKSTATUS_OK)
            {
                Console.Write("No cameras found.\n");
                return 0;
            }

            for (uint i = 0; i < numCameras; i++)
            {
                IKapCLib.ITKDEV_INFO di = new IKapCLib.ITKDEV_INFO();

                // 获取相机设备信息。
                //
                // Get camera device information.
                res = IKapCLib.ItkManGetDeviceInfo(i, ref di);
                Console.Write("Using camera: serial: {0}, name: {1}, interface: {2}.\n", di.SerialNumber, di.FullName, di.DeviceClass);

                // 当设备为 GigECamera 相机且序列号正确时。
                //
                // When the device is GigECamera camera and the serial number is proper.
                if (di.DeviceClass == "GigEVision" && di.SerialNumber != "")
                {
                    if (bFirst)
                        m_nGvStartIndex = i;
                    bFirst = false;

                    // 获取 GigECamera 相机设备信息。
                    //
                    // Get GigECamera camera device information.
                    IKapCLib.ITKGIGEDEV_INFO gv_board_info = new IKapCLib.ITKGIGEDEV_INFO();
                    res = IKapCLib.ItkManGetGigEDeviceInfo(i, ref gv_board_info);
                }
            }
            return numCameras;
        }

        /// <summary>
        /// 获取相机的信息  SL自己添加的函数
        /// </summary>
        /// <returns>所有相机的信息 </returns>
        public static Dictionary<uint, IKapCLib.ITKGIGEDEV_INFO> GetCameraInfo()
        {
            Dictionary<uint, IKapCLib.ITKGIGEDEV_INFO> CameraInfos = new Dictionary<uint, IKapCLib.ITKGIGEDEV_INFO>();
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;
            uint numCameras = 0;
            bool bFirst = true;

            // 枚举可用相机的数量。在打开相机前，必须调用 ItkManGetDeviceCount() 函数。
            //
            // Enumerate the number of available cameras. Before opening the camera, ItkManGetDeviceCount() function must be called.
            res = IKapCLib.ItkManGetDeviceCount(ref numCameras);

            // 当没有连接的相机时。
            //
            // When there is no connected cameras.
            if (numCameras == 0 || res != (uint)ItkStatusErrorId.ITKSTATUS_OK)
            {
                Console.Write("No cameras found.\n");
                return null;
            }

            for (uint i = 0; i < numCameras; i++)
            {
                IKapCLib.ITKDEV_INFO di = new IKapCLib.ITKDEV_INFO();

                // 获取相机设备信息。
                //
                // Get camera device information.
                res = IKapCLib.ItkManGetDeviceInfo(i, ref di);
                Console.Write("Using camera: serial: {0}, name: {1}, interface: {2}.\n", di.SerialNumber, di.FullName, di.DeviceClass);

                // 当设备为 GigECamera 相机且序列号正确时。
                //
                // When the device is GigECamera camera and the serial number is proper.
                if (di.DeviceClass == "GigEVision" && di.SerialNumber != "")
                {
                    if (bFirst)
                        m_nGvStartIndex = i;
                    bFirst = false;

                    // 获取 GigECamera 相机设备信息。
                    //
                    // Get GigECamera camera device information.
                    IKapCLib.ITKGIGEDEV_INFO gv_board_info = new IKapCLib.ITKGIGEDEV_INFO();
                    res = IKapCLib.ItkManGetGigEDeviceInfo(i, ref gv_board_info);
                    CameraInfos.Add(i, gv_board_info);
                }
            }
            return CameraInfos;
        }


        /* @brief：打开 GigECamera 相机。
         *
         * Open GigECamera camera. */
        public bool OpenDevice(uint nIndex)
        {
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;
            res = IKapCLib.ItkDevOpen(nIndex, (int)ItkDeviceAccessMode.ITKDEV_VAL_ACCESS_MODE_EXCLUSIVE, ref m_hCamera);
            var code = res.ToString("X8");
            if (res == (uint)ItkStatusErrorId.ITKSTATUS_OK)
                return true;
            return false;
        }

        /* @brief：关闭 GigECamera 相机。
         *
         * Close GigECamera camera. */
        public void CloseDevice()
        {
            if (!m_hCamera.Equals(-1))
            {
                IKapCLib.ItkDevClose(m_hCamera);
                m_hCamera = (IntPtr)(-1);
            }
        }

        /* @brief：创建数据流和缓冲区。
         *
         * @brief：Create data stream and buffer. */
        public bool CreateStreamAndBuffer()
        {
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;

            // 数据流数量。
            //
            // The number of data stream.
            uint streamCount = 0;

            // 图像宽度。
            //
            // Image width.
            Int64 nWidth = 0;

            // 图像高度。
            //
            // Image height.
            Int64 nHeight = 0;

            // 像素格式。
            //
            // Pixel format.
            uint nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_MONO8;

            // 图像大小。
            //
            // Image size.
            IntPtr nImageSize = Marshal.AllocHGlobal(8);

            // 像素格式名称。
            //
            // Pixel format name.
            StringBuilder pixelFormat = new StringBuilder(128);

            // 像素格式名称长度。
            //
            // Pixel format name length.
            uint pixelFormatSize = 128;

            // 缓冲区大小。
            //
            // Buffer size.
            uint nBufferSize = 0;

            // 获取数据流数量。
            //
            // Get the number of data stream.
            res = IKapCLib.ItkDevGetStreamCount(m_hCamera, ref streamCount);
            if (res != (uint)ItkStatusErrorId.ITKSTATUS_OK)
                return false;

            if (streamCount == 0)
            {
                Console.WriteLine("Camera does not have image stream channel.");
                return false;
            }

            // 获取图像宽度。
            //
            // Get image width.
            res = IKapCLib.ItkDevGetInt64(m_hCamera, "Width", ref nWidth);
            if (res != (uint)ItkStatusErrorId.ITKSTATUS_OK)
                return false;
            Width = nWidth;

            // 获取图像高度。
            //
            // Get image height.
            res = IKapCLib.ItkDevGetInt64(m_hCamera, "Height", ref nHeight);
            if (res != (uint)ItkStatusErrorId.ITKSTATUS_OK)
                return false;
            Height = nHeight;

            // 获取像素格式。
            //
            // Get pixel format.
            res = IKapCLib.ItkDevToString(m_hCamera, "PixelFormat", pixelFormat, ref pixelFormatSize);
            if (res != (uint)ItkStatusErrorId.ITKSTATUS_OK)
                return false;

            if (pixelFormat.ToString() == "Mono8")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_MONO8;
            else if (pixelFormat.ToString() == "BayerGR8")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_GR8;
            else if (pixelFormat.ToString() == "BayerRG8")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_RG8;
            else if (pixelFormat.ToString() == "BayerGB8")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_GB8;
            else if (pixelFormat.ToString() == "BayerBG8")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_BG8;
            else if (pixelFormat.ToString() == "Mono10")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_MONO10;
            else if (pixelFormat.ToString() == "BayerGR10")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_GR10;
            else if (pixelFormat.ToString() == "BayerRG10")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_RG10;
            else if (pixelFormat.ToString() == "BayerGB10")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_GB10;
            else if (pixelFormat.ToString() == "BayerBG10")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_BG10;
            else if (pixelFormat.ToString() == "Mono10Packed")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_MONO10PACKED;
            else if (pixelFormat.ToString() == "BayerGR10Packed")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_GR10PACKED;
            else if (pixelFormat.ToString() == "BayerRG10Packed")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_RG10PACKED;
            else if (pixelFormat.ToString() == "BayerGB10Packed")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_GB10PACKED;
            else if (pixelFormat.ToString() == "BayerBG10Packed")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_BG10PACKED;
            else if (pixelFormat.ToString() == "Mono12")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_MONO12;
            else if (pixelFormat.ToString() == "BayerGR12")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_GR12;
            else if (pixelFormat.ToString() == "BayerRG12")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_RG12;
            else if (pixelFormat.ToString() == "BayerGB12")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_GB12;
            else if (pixelFormat.ToString() == "BayerBG12")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_BG12;
            else if (pixelFormat.ToString() == "Mono12Packed")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_MONO12PACKED;
            else if (pixelFormat.ToString() == "Mono14")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_MONO14;
            else if (pixelFormat.ToString() == "Mono16")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_MONO16;
            else if (pixelFormat.ToString() == "RGB8")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_RGB888;
            else if (pixelFormat.ToString() == "RGB10")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_RGB101010;
            else if (pixelFormat.ToString() == "RGB12")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_RGB121212;
            else if (pixelFormat.ToString() == "RGB14")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_RGB141414;
            else if (pixelFormat.ToString() == "RGB16")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_RGB161616;
            else if (pixelFormat.ToString() == "BGR8")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BGR888;
            else if (pixelFormat.ToString() == "BGR10")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BGR101010;
            else if (pixelFormat.ToString() == "BGR12")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BGR121212;
            else if (pixelFormat.ToString() == "BGR14")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BGR141414;
            else if (pixelFormat.ToString() == "BGR16")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BGR161616;
            else if (pixelFormat.ToString() == "YUV422_8")
                nFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_YUV422_8_YUYV;
            else
            {
                Console.WriteLine("Camera does not support pixel format %s.", pixelFormat);
                return false;
            }

            // 创建图像缓冲区。
            //
            // Create image buffer.
            IntPtr hBuffer = new IntPtr();
            res = IKapCLib.ItkBufferNew(nWidth, nHeight, nFormat, ref hBuffer);
            if (res != (uint)ItkStatusErrorId.ITKSTATUS_OK)
                return false;
            m_hBufferList.Add(hBuffer);

            // 获取缓冲区大小。
            //
            // Get buffer size.
            res = IKapCLib.ItkBufferGetPrm(hBuffer, (uint)ItkBufferPrm.ITKBUFFER_PRM_SIZE, nImageSize);
            if (res != (uint)ItkStatusErrorId.ITKSTATUS_OK)
                return false;
            nBufferSize = (uint)Marshal.ReadInt64(nImageSize);

            // 创建缓冲区数据存储。
            //
            // Create buffer data saving.
            m_bufferData = Marshal.AllocHGlobal((int)nBufferSize);
            if (m_bufferData.Equals(-1))
            {
                Console.WriteLine("Apply buffer data failure");
                IKapCLib.ItkManTerminate();
                Console.ReadLine();
                Environment.Exit(1);
            }

            // 申请数据流资源。
            //
            // Allocate data stream source.
            res = IKapCLib.ItkDevAllocStream(m_hCamera, 0, hBuffer, ref m_hStream);
            if (res != (uint)ItkStatusErrorId.ITKSTATUS_OK)
                return false;

            // 向数据流中添加缓冲区。
            //
            // Add buffers to data stream.
            for (int i = 1; i < m_nBufferCountOfStream; i++)
            {
                res = IKapCLib.ItkBufferNew(nWidth, nHeight, nFormat, ref hBuffer);
                if (res != (uint)ItkStatusErrorId.ITKSTATUS_OK)
                    return false;
                res = IKapCLib.ItkStreamAddBuffer(m_hStream, hBuffer);
                if (res != (uint)ItkStatusErrorId.ITKSTATUS_OK)
                    return false;
                m_hBufferList.Add(hBuffer);
            }
            return true;
        }

        /* @brief：开始数据流。
         *
         * @brief：Start data stream. */
        public bool StartStream(uint nGrabCount)
        {
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;

            // 传输模式。
            //
            // Transfer mode.
            IntPtr xferMode = Marshal.AllocHGlobal(4);
            Marshal.WriteInt32(xferMode, 0, (int)ItkStreamTransferMode.ITKSTREAM_VAL_TRANSFER_MODE_SYNCHRONOUS_WITH_PROTECT);

            // 采集模式。
            //
            // Grab mode.
            IntPtr startMode = Marshal.AllocHGlobal(4);
            Marshal.WriteInt32(startMode, 0, (int)ItkStreamStartMode.ITKSTREAM_VAL_START_MODE_NON_BLOCK);

            // 超时时间。
            //
            // Time out time.
            IntPtr timeOut = Marshal.AllocHGlobal(4);
            Marshal.WriteInt32(timeOut, 0, (int)IKapCLib.ITKSTREAM_CONTINUOUS);

            // 设置采集模式。
            //
            // Set grab mode.
            res = IKapCLib.ItkStreamSetPrm(m_hStream, (uint)ItkStreamPrm.ITKSTREAM_PRM_START_MODE, startMode);
            if (res != (uint)ItkStatusErrorId.ITKSTATUS_OK)
                return false;

            // 设置采集模式。
            //
            // Set grab mode.
            res = IKapCLib.ItkStreamSetPrm(m_hStream, (uint)ItkStreamPrm.ITKSTREAM_PRM_TRANSFER_MODE, xferMode);
            if (res != (uint)ItkStatusErrorId.ITKSTATUS_OK)
                return false;

            // 设置超时时间。
            //
            // Set time out time.
            res = IKapCLib.ItkStreamSetPrm(m_hStream, (uint)ItkStreamPrm.ITKSTREAM_PRM_TIME_OUT, timeOut);
            if (res != (uint)ItkStatusErrorId.ITKSTATUS_OK)
                return false;

            // 注册回调函数
            //
            // Register callback functions.
            cbOnStartOfStreamProc = new IKapCLib.PITKSTREAMCALLBACK(cbOnStartOfStreamFunc);
            res = IKapCLib.ItkStreamRegisterCallback(m_hStream, (uint)ItkStreamEventType.ITKSTREAM_VAL_EVENT_TYPE_START_OF_STREAM, cbOnStartOfStreamProc, m_hStream);
            if (res != (uint)ItkStatusErrorId.ITKSTATUS_OK)
                return false;
            cbOnEndOfStreamProc = new IKapCLib.PITKSTREAMCALLBACK(cbOnEndOfStreamFunc);
            res = IKapCLib.ItkStreamRegisterCallback(m_hStream, (uint)ItkStreamEventType.ITKSTREAM_VAL_EVENT_TYPE_END_OF_STREAM, cbOnEndOfStreamProc, m_hStream);
            if (res != (uint)ItkStatusErrorId.ITKSTATUS_OK)
                return false;
            cbOnEndOfFrameProc = new IKapCLib.PITKSTREAMCALLBACK(cbOnEndOfFrameFunc);
            res = IKapCLib.ItkStreamRegisterCallback(m_hStream, (uint)ItkStreamEventType.ITKSTREAM_VAL_EVENT_TYPE_END_OF_FRAME, cbOnEndOfFrameProc, m_hStream);
            if (res != (uint)ItkStatusErrorId.ITKSTATUS_OK)
                return false;
            cbOnTimeOutProc = new IKapCLib.PITKSTREAMCALLBACK(cbOnTimeOutFunc);
            res = IKapCLib.ItkStreamRegisterCallback(m_hStream, (uint)ItkStreamEventType.ITKSTREAM_VAL_EVENT_TYPE_TIME_OUT, cbOnTimeOutProc, m_hStream);
            if (res != (uint)ItkStatusErrorId.ITKSTATUS_OK)
                return false;
            cbOnFrameLostProc = new IKapCLib.PITKSTREAMCALLBACK(cbOnFrameLostFunc);
            res = IKapCLib.ItkStreamRegisterCallback(m_hStream, (uint)ItkStreamEventType.ITKSTREAM_VAL_EVENT_TYPE_FRAME_LOST, cbOnFrameLostProc, m_hStream);
            if (res != (uint)ItkStatusErrorId.ITKSTATUS_OK)
                return false;

            // 开始图像采集。
            //
            // Start grabbing images.
            m_nCurFrameIndex = 0;
            res = IKapCLib.ItkStreamStart(m_hStream, nGrabCount);
            if (res != (uint)ItkStatusErrorId.ITKSTATUS_OK)
                return false;
            return true;
        }

        /* @brief：停止数据流。
         *
         * @brief：Stop data stream. */
        public bool StopStream()
        {
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;

            // 停止图像采集。
            //
            // Stop grabbing images.
            res = IKapCLib.ItkStreamStop(m_hStream);

            // 清除回调函数。
            //
            // Unregister callback functions.
            res = IKapCLib.ItkStreamUnregisterCallback(m_hStream, (uint)ItkStreamEventType.ITKSTREAM_VAL_EVENT_TYPE_START_OF_STREAM);
            res = IKapCLib.ItkStreamUnregisterCallback(m_hStream, (uint)ItkStreamEventType.ITKSTREAM_VAL_EVENT_TYPE_END_OF_STREAM);
            res = IKapCLib.ItkStreamUnregisterCallback(m_hStream, (uint)ItkStreamEventType.ITKSTREAM_VAL_EVENT_TYPE_END_OF_FRAME);
            res = IKapCLib.ItkStreamUnregisterCallback(m_hStream, (uint)ItkStreamEventType.ITKSTREAM_VAL_EVENT_TYPE_TIME_OUT);
            res = IKapCLib.ItkStreamUnregisterCallback(m_hStream, (uint)ItkStreamEventType.ITKSTREAM_VAL_EVENT_TYPE_FRAME_LOST);
            return true;
        }

        /* @brief：释放数据流和缓冲区。
         *
         * Free data stream and buffers. */
        public void ClearStreamAndBuffer()
        {
            foreach (var it in m_hBufferList)
            {
                IKapCLib.ItkStreamRemoveBuffer(m_hStream, it);
                IKapCLib.ItkBufferFree(it);
            }
            m_hBufferList.Clear();
            IKapCLib.ItkDevFreeStream(m_hStream);

            if (!m_bufferData.Equals(-1))
            {
                Marshal.FreeHGlobal(m_bufferData);
            }
        }
    }
}

using Matrox.MatroxImagingLibrary;
using System;
using System.Runtime.InteropServices;

namespace Device.CamLinkCard.Matrox
{
    public unsafe class MatroxManager : CamLinkCardBase
    {
        public class HookDataStruct
        {
            public MIL_ID MilDigitizer;
            public MIL_ID MilImageDisp;
            public int ProcessedImageCount;
        };

        const int BUFFERING_SIZE_MAX = 1;
        const int BUFFER_WIDTH = 8192;
        const int BUFFER_HEIGHT = 41954;
        int MilGrabBufferListSize = 0;

        MIL_ID MilApplication = MIL.M_NULL;
        MIL_ID MilSystem = MIL.M_NULL;
        MIL_ID MilDigitizer = MIL.M_NULL;
        MIL_ID MilImageDisp = MIL.M_NULL;
        MIL_ID[] MilGrabBufferList = new MIL_ID[BUFFERING_SIZE_MAX];
        MIL_INT ProcessFrameCount = 0;
        MIL_INT iDigWidth = 0;
        MIL_INT iDigHeight = 0;
        HookDataStruct UserHookData = new HookDataStruct();

        GCHandle hUserData;
        MIL_DIG_HOOK_FUNCTION_PTR ProcessingFunctionPtr;

        public override string Name { get  ; set  ; }

        public override event GrabOverHandler OnGrabOver;

        public override bool DisConnect()
        {
            if (MilApplication == MIL.M_NULL) return true;

            // Stop the processing.
            MIL.MdigProcess(MilDigitizer, MilGrabBufferList, MilGrabBufferListSize, MIL.M_STOP, MIL.M_DEFAULT, ProcessingFunctionPtr, GCHandle.ToIntPtr(hUserData));

            // Free the GCHandle when no longer used
            hUserData.Free();

            while (MilGrabBufferListSize > 0)
            {
                MIL.MbufFree(MilGrabBufferList[--MilGrabBufferListSize]);
            }

            MIL.MbufFree(MilImageDisp);
            MIL.MdigFree(MilDigitizer);
            MIL.MsysFree(MilSystem);
            MIL.MappFree(MilApplication);
            return true;
        }

        public override bool Connect(out string pErrorInfo, string pPath_Config)
        {
            pErrorInfo = null;

            //加载采集卡部分
            MIL.MappAlloc("M_DEFAULT", MIL.M_DEFAULT, ref MilApplication);
            MIL.MsysAlloc(MilApplication, MIL.M_SYSTEM_RADIENTEVCL, MIL.M_DEFAULT, MIL.M_DEFAULT, ref MilSystem);
            MIL.MdigAlloc(MilSystem, MIL.M_DEFAULT, pPath_Config, MIL.M_DEFAULT, ref MilDigitizer);

            iDigWidth = MIL.MdigInquire(MilDigitizer, MIL.M_SIZE_X, MIL.M_NULL);
            iDigHeight = MIL.MdigInquire(MilDigitizer, MIL.M_SIZE_Y, MIL.M_NULL);

            MIL.MdigControl(MilDigitizer, MIL.M_PROCESS_GRAB_MONITOR, MIL.M_DISABLE);

            // Allocate a monochrome display buffer. 
            MIL.MbufAlloc2d(MilSystem,
                iDigWidth,
                iDigHeight * BUFFERING_SIZE_MAX,
                8 + MIL.M_UNSIGNED,
                MIL.M_IMAGE + MIL.M_GRAB + MIL.M_PROC + MIL.M_DISP,
                ref MilImageDisp);
            MIL.MbufClear(MilImageDisp, MIL.M_COLOR_BLACK);

            // Allocate the grab buffers and clear them.
            MIL.MappControl(MIL.M_DEFAULT, MIL.M_ERROR, MIL.M_PRINT_DISABLE);
            for (MilGrabBufferListSize = 0; MilGrabBufferListSize < BUFFERING_SIZE_MAX; MilGrabBufferListSize++)
            {
                MIL.MbufChild2d(MilImageDisp,
                                0,
                                iDigHeight * MilGrabBufferListSize,
                                iDigWidth,
                                iDigHeight,
                                ref MilGrabBufferList[MilGrabBufferListSize]);

                if (MilGrabBufferList[MilGrabBufferListSize] != MIL.M_NULL)
                {
                    MIL.MbufClear(MilGrabBufferList[MilGrabBufferListSize], 0xFF);
                }
                else
                {
                    break;
                }
            }
            MIL.MappControl(MIL.M_DEFAULT, MIL.M_ERROR, MIL.M_PRINT_ENABLE);

            // Initialize the user's processing function data structure.
            UserHookData.MilDigitizer = MilDigitizer;
            UserHookData.MilImageDisp = MilImageDisp;
            UserHookData.ProcessedImageCount = 0;

            // get a handle to the HookDataStruct object in the managed heap, we will use this 
            // handle to get the object back in the callback function
            hUserData = GCHandle.Alloc(UserHookData);
            ProcessingFunctionPtr = new MIL_DIG_HOOK_FUNCTION_PTR(ProcessingFunction);

            // Start the processing. The processing function is called with every frame grabbed.
            MIL.MdigProcess(MilDigitizer, MilGrabBufferList, MilGrabBufferListSize, MIL.M_START, MIL.M_DEFAULT, ProcessingFunctionPtr, GCHandle.ToIntPtr(hUserData));

            MIL.MdigControl(MilDigitizer, MIL.M_PROCESS_TIMEOUT, MIL.M_INFINITE);
            // Here the main() is free to perform other tasks while the processing is executing.
            MIL.MdigControl(MilDigitizer, MIL.M_GRAB_TIMEOUT, MIL.M_INFINITE);
            return true;
        }

        /// <summary>
        /// camlink采集图像回调函数
        /// </summary>
        /// <param name="HookType"></param>
        /// <param name="HookId"></param>
        /// <param name="HookDataPtr"></param>
        /// <returns></returns>
        MIL_INT ProcessingFunction(MIL_INT HookType, MIL_ID HookId, IntPtr HookDataPtr)
        {
            MIL_ID ModifiedBufferId = MIL.M_NULL;
            // this is how to check if the user data is null, the IntPtr class
            // contains a member, Zero, which exists solely for this purpose
            if (!IntPtr.Zero.Equals(HookDataPtr))
            {
                // get the handle to the DigHookUserData object back from the IntPtr
                GCHandle hUserData = GCHandle.FromIntPtr(HookDataPtr);

                // get a reference to the DigHookUserData object
                HookDataStruct UserData = hUserData.Target as HookDataStruct;

                // Retrieve the MIL_ID of the grabbed buffer.
                MIL.MdigGetHookInfo(HookId, MIL.M_MODIFIED_BUFFER + MIL.M_BUFFER_ID, ref ModifiedBufferId);

                // Increment the frame counter.
                UserData.ProcessedImageCount++;

                MIL_INT width = MIL.MbufInquire(ModifiedBufferId, MIL.M_SIZE_X, MIL.M_NULL);
                MIL_INT height = MIL.MbufInquire(ModifiedBufferId, MIL.M_SIZE_Y, MIL.M_NULL);
                MIL_INT band = MIL.MbufInquire(ModifiedBufferId, MIL.M_SIZE_BAND, MIL.M_NULL);
                MIL_INT bits = MIL.MbufInquire(ModifiedBufferId, MIL.M_SIZE_BIT, MIL.M_NULL);
                MIL_INT type = MIL.MbufInquire(ModifiedBufferId, MIL.M_TYPE, MIL.M_NULL);
                MIL_INT pixType = type - bits;


                System.Threading.Thread.Sleep(500);

                byte[] buffer = new byte[iDigWidth * iDigHeight];
                void* p;
                IntPtr ptr;
                fixed (byte* pc = buffer)
                {
                    p = (void*)pc;
                    ptr = new IntPtr(p);
                }

                MIL.MbufGet(UserData.MilImageDisp, buffer);


                ////利用halcon从内存中采集图片并保存  
                //HObject ImageTemp = null;
                //HOperatorSet.GenImage1(out ImageTemp, "byte", (int)iDigWidth, (int)iDigHeight, ptr);//取内存数据，生成图像，halcon实现

                //OnGrabOver?.Invoke(Name, ImageTemp);
            }
            return 0;
        }
    }
}

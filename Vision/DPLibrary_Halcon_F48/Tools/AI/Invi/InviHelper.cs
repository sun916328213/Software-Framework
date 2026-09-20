using DPLibrary_Halcon_F48.Tools.AI.Definition;
using Extension.Core.Conversion;
using Extension.Core.Enums;
using Extension.Imaging.Halcon;
using Extension.Imaging.OpenCv;
using Extension.Platform.Diagnostics;
using Extension.Platform.Native;
using Extension.Platform.Memory;
using Extension.Platform.Processes;
using HalconDotNet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using OpenCvSharp;

namespace DPLibrary_Halcon_F48.Tools.AI.Invi
{
    internal class InviHelper : AIBase
    {
        private readonly DynamicDllLoaderEx _loader;

        private delegate IntPtr CreateInferManagerDelegate();
        private delegate bool DestroyInferManagerDelegate(IntPtr manager);
        private readonly ConcurrentDictionary<string, bool> _loadedModels = new ConcurrentDictionary<string, bool>();


        private delegate bool CreateInferAPIDelegate(IntPtr manager, string modelPath, int type);

        private delegate IntPtr InferExeDelegate(IntPtr manager, string modelName, int type, int length,
            IntPtr imagePtr, int inputsize = 512, double threshold = 0.5, int batch_size = 1, int mARea = 100);

        private delegate IntPtr InferIntPtrExeDelegate(IntPtr manager, string modelName, int type, int w, int h, int channel,
         IntPtr imagePtr, int inputsize = 512, double threshold = 0.5, int batch_size = 1, int mARea = 100);


        private delegate bool DestroyInferDelegate(IntPtr manager, string modelPath);

        private CreateInferManagerDelegate _createInferManager;
        private DestroyInferManagerDelegate _destoryInferManager;

        private CreateInferAPIDelegate _createInferApi;
        private InferExeDelegate _inferExe;
        private InferIntPtrExeDelegate _inferIntPtrExe;
        private DestroyInferDelegate _destroyInfer;

        private IntPtr _InferManager;

        private string _modelPath;
        private ToolType _type;
        int Decimal = 3;

        public InviHelper()
        {
            string _instanceDllPath = "INVI API.dll";

            try
            {
                _loader = new DynamicDllLoaderEx(_instanceDllPath);
                InitializeDelegates();
            }
            catch (Exception)
            {
            }
        }
        private void InitializeDelegates()
        {
            _createInferManager = _loader.GetFunction<CreateInferManagerDelegate>("CreateInferManager");
            _destoryInferManager = _loader.GetFunction<DestroyInferManagerDelegate>("DestroyInferManager");

            _createInferApi = _loader.GetFunction<CreateInferAPIDelegate>("CreateInferAPI");
            _inferExe = _loader.GetFunction<InferExeDelegate>("InferExe");
            _inferIntPtrExe = _loader.GetFunction<InferIntPtrExeDelegate>("InferExe_Intptr");
            _destroyInfer = _loader.GetFunction<DestroyInferDelegate>("DestoryInfer");

            _InferManager = _createInferManager();
        }


        public override InferParam InferParam { get; set; }

        public override List<InferResultInfo> Inference(HObject image)
        {
            List<InferResultInfo> inferResultInfos = new List<InferResultInfo>();
            List<InviResult> result;

            //var mat = image.Clone().ToMat().ToBytes();

            //using (var imageHandle = SafeMemoryHandle.CreateFromBytes(mat))
            //{
            //    //image.ToIntPtr(out IntPtr imageHandle, out int imageWidth, out int imageHeight, out int imageChannels);
            //    Stopwatch stopwatch = new Stopwatch();

            //    stopwatch.Start();
            //    var resultHandle = InferExe(_modelPath, (int)_type, imageHandle.Size, imageHandle.GetPointer(), inputSize, filterArea);
            //    stopwatch.Stop();
            //    var d1 = Marshal.PtrToStringAnsi(resultHandle);
            //    //数据转换
            //    result = JsonEx.FromJson<List<InviResult>>(d1);
            //}

            Stopwatch stopwatch = new Stopwatch();
            image.ToIntPtr(out IntPtr imageHandle, out int imageWidth, out int imageHeight, out int imageChannels);
            stopwatch.Start();
            var resultHandle = InferExe_IntPtr(_modelPath, (int)_type, imageWidth, imageHeight, imageChannels, imageHandle);
            stopwatch.Stop();
            var d111 = Marshal.PtrToStringAnsi(resultHandle);
            //数据转换
            result = JsonEx.FromJson<List<InviResult>>(d111);

            for (int i = 0; i < result.Count; i++)
            {
                var resultInfo = result[i];

                // 获取轮廓点集合
                HTuple rows = new HTuple(), cols = new HTuple();
                for (var vv = 0; vv < resultInfo.contour_points.Count; vv++)
                {
                    cols.Append(resultInfo.contour_points.ElementAt(vv).x);
                    rows.Append(resultInfo.contour_points.ElementAt(vv).y);
                }

                InferResult cInferResult = new InferResult(resultInfo.area, resultInfo.confidence, resultInfo.class_name, cols, rows);

                var d1 = JudgeParam(cInferResult, InferParam);
                if (d1 != null)
                {
                    InferResultInfo Contour = new InferResultInfo()
                    {
                        ContourInfo = d1,
                        DefectInfo = new DefectInfo
                        {
                            DefectName = resultInfo.class_name,
                            Area = Math.Round(cInferResult.area, Decimal),
                            Confidence = resultInfo.confidence,
                        }
                    };
                    inferResultInfos.Add(Contour);
                }
            }
            return inferResultInfos;
        }

        public override void InitModel(string path, int type)
        {
            if (_InferManager == IntPtr.Zero)
            {
                throw new Exception("Invi load model fail");
            }

            if (!CreateInferAPI(path, EnumEx.GetEnum<ToolType>(type.ToString())))
            {
                throw new Exception("CreateInferAPI fail");
            }
            _modelPath = path;
            _type = (ToolType)type;
        }

        public bool CreateInferAPI(string modelPath, ToolType type)
        {
            if (_loadedModels.TryAdd(modelPath, true))
            {
                return _createInferApi(_InferManager, modelPath, (int)type);
            }
            return false;
        }

        public IntPtr InferExe(string modelName, int type, int length, IntPtr imagePtr, int inputsize = 512, int mARea = 100, double threshold = 0.5, int batch_size = 1)
        {
            return _inferExe(_InferManager, modelName, type, length, imagePtr, inputsize, threshold, batch_size, mARea);
        }

        public IntPtr InferExe_IntPtr(string modelName, int type, int w, int h, int channel, IntPtr imagePtr, int inputsize = 512, int mARea = 100, double threshold = 0.5, int batch_size = 1)
        {
            return _inferIntPtrExe(_InferManager, modelName, type, w, h, channel, imagePtr, inputsize, threshold, batch_size, mARea);
        }
    }
}

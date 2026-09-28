using Vision.Core;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DPLibrary_Halcon_F48.Base
{
    internal static class HHelper
    {
        public static void SaveImageAsync(HObject img, string dir, string filename, string format = "png") 
        {
            HTuple ser = new HTuple();
            try
            {
                HOperatorSet.SerializeImage(img, out ser);
                Task.Run(async () =>
                {
                    HObject toSave = null;
                    HTuple serToUse = null;
                    serToUse = ser;

                    try
                    {
                        HOperatorSet.DeserializeImage(out toSave, serToUse);
                        BaseHelper.JudgeDirectoryExist(dir);
                        string path;
                        path = System.IO.Path.Combine(dir, filename+$".{format}");
                        HOperatorSet.WriteImage(toSave, format, 0, path);
                    }
                    catch (Exception ex)
                    {
                        // 不要在后台线程直接弹窗，编组到 UI 线程再提示
                        var dispatcher = Application.Current?.Dispatcher;
                        if (dispatcher != null)
                        {
                            dispatcher.Invoke(() => MessageBox.Show(ex.Message));
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"保存图像失败: {ex.Message}");
                        }
                    }
                    finally
                    {
                        ser?.Dispose();
                        serToUse?.Dispose();
                        toSave?.Dispose();
                    }
                });
            }
            catch (Exception)
            {
                ser?.Dispose();
            }
        }
    }
}

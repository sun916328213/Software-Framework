using Extension.Core.SystemEx;
using Extension.Struct;
using Seagull.BarTender.Print;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;


namespace Device.PrintSoftware
{
    public class BartenderHelper : IPrintSoftware
    {
        Engine engine = null;
        LabelFormatDocument format = null;

        /// <summary>
        /// 打印机列表
        /// </summary>
        public List<string> Printers { get; set; } = new  List<string> ();
        /// <summary>
        /// 打印机名称
        /// </summary>
        public string PrintName { get; set; }
        /// <summary>
        /// 标签名称
        /// </summary>
        public string LabelName { get; set; }
        /// <summary>
        /// 标签路径
        /// </summary>
        public string LabelPath { get; set; }
       
        public BartenderHelper()
        {
            Printers printers = new Printers();
            foreach (Seagull.BarTender.Print.Printer printer in printers)
            {
                Printers.Add(printer.PrinterName);
            }
        }

        /// <summary>
        /// 打开文件
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public void Open(string fileName)
        {
            ProcessEx.CloseProcess("BarTender");
            if (!fileName.Contains(".btw")) throw new FileFormatException("File Format Error");
            if (engine != null)
                engine.Stop(SaveOptions.DoNotSaveChanges);
            engine = new Engine(true);

            Printers printers = new Printers();

            if (format != null)
            {
                if (format.Status == LabelFormat.LabelStatus.Loaded)
                    format.Close(SaveOptions.DoNotSaveChanges);
            }
            LabelPath = fileName;
            format = engine.Documents.Open(fileName);
            LabelName = Path.GetFileNameWithoutExtension(fileName);
        }

        /// <summary>
        /// 打开文件 异步  
        /// </summary>
        /// <param name="pFileName">文件名</param>
        /// <returns>操作结果</returns>
        public async Task OpenAsync(string pFileName)
        {
            await Task.Run(() => Open(pFileName));
        }

        /// <summary>
        /// 关闭文件
        /// </summary>
        /// <returns></returns>
        public void Close()
        {
            
            if (format != null)
                format.Close(SaveOptions.DoNotSaveChanges);
            if (engine != null)
                engine.Stop(SaveOptions.DoNotSaveChanges);   
        }

        /// <summary>
        /// 获取标签格式中的所有键
        /// </summary>
        public List<KeyValue> GetValue()
        {
            List<KeyValue> pKeys = new List<KeyValue>();
         
            // 如果格式不为空，则遍历格式中的所有子字符串，并添加到键列表中
            if (format != null)
            {
                var datas = format.SubStrings.GetEnumerator();
                while (datas.MoveNext())
                {
                    var d = datas.Current;
                    pKeys.Add(new KeyValue(d.Name,d.Value));
                }
                return pKeys;
            }
            throw new Exception("LabelFormatDocument Is Null!!");
        }

        /// <summary>
        /// 设置标签格式中的子字符串值
        /// </summary>
        /// <param name="Value">字典参数，包含要设置的子字符串名和值</param>
        public void SetValue(List<KeyValue> Value)
        {
            // 如果值字典不为空，则遍历字典中的每一对键值，并设置到格式中
            if (Value != null)
            {
                foreach (var date in Value)
                {
                    format.SubStrings.SetSubString(date.Key, date.Value);
                }
            }
        }

        /// <summary>
        /// 预览
        /// </summary>
        /// <returns></returns>
        public Bitmap Preview()
        {
            string _bmp_path = System.Environment.CurrentDirectory + "\\preview.bmp";
            if (format != null)
            {
                format.ExportImageToFile(_bmp_path, ImageType.BMP, Seagull.BarTender.Print.ColorDepth.ColorDepth24bit, new Resolution(2048, 2048
                    ), OverwriteOptions.Overwrite);

                System.Drawing.Image image = System.Drawing.Image.FromFile(_bmp_path);
                Bitmap bitmap = new Bitmap(image);
                return bitmap;
            }
            else
            {
                throw new Exception("format is null!!");
            }
        }


        /// <summary>
        /// 预览
        /// </summary>
        /// <returns></returns>
        public async Task<Bitmap> PreviewAsync()
        {
            return await Task.Run(() => Preview());
        }

        /// <summary>
        /// 打印
        /// </summary>
        /// <returns></returns>
        public void Print()
        {
            if (format == null) throw new Exception("Please Open LabelFile!!");
            if (PrintName == null) throw new Exception("Print not Exist!!");

            string errorinfo = null;

            lock (engine)
            {
                format.PrintSetup.PrinterName = PrintName;

                Messages messages;
                int waitForCompletionTimeout = 10000; // 10 seconds

                Result result = format.Print("Print", waitForCompletionTimeout, out messages);
                string messageString = "\n\nMessages:";

                foreach (Seagull.BarTender.Print.Message message in messages)
                {
                    messageString += "\n\n" + message.Text;
                }

                if (result == Result.Failure)
                {
                    errorinfo = messageString;
                    throw new Exception(errorinfo);
                }
                else
                {
                    errorinfo = null;
                }
            }
        }

        /// <summary>
        /// 打印 异步
        /// </summary>
        /// <returns></returns>
        public async Task PrintAsync()
        {
            await Task.Run(() => Print());
        }
    }
}

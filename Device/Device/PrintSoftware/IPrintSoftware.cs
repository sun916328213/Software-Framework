using Extension.Struct;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;

namespace Device.PrintSoftware
{
    public  interface IPrintSoftware
    {
        /// <summary>
        /// 打印机名称
        /// </summary>
        string PrintName { get; set; }
        /// <summary>
        /// 标签路径
        /// </summary>
        string LabelPath { get; set; }
        /// <summary>
        /// 标签名称
        /// </summary>
        string LabelName { get; set; }
        /// <summary>
        /// 打印机集合
        /// </summary>
        List<string> Printers { get; set; }
        /// <summary>
        ///  打开打印文件
        /// </summary>
        /// <param name="fileName">文件路径</param>
        void Open(string fileName);
        /// <summary>
        /// 打开打印文件 异步
        /// </summary>
        /// <param name="">文件路径</param>
        Task OpenAsync(string fileName);
        /// <summary>
        /// 关闭
        /// </summary>
        void Close();
        /// <summary>
        /// 获取标签信息字段
        /// </summary>
        List<KeyValue> GetValue();
        /// <summary>
        /// 设置标签字段数据
        /// </summary>
        void SetValue(List<KeyValue> value);
        /// <summary>
        /// 图像预览
        /// </summary>
        /// <returns></returns>
        Bitmap Preview();
        /// 图像预览 异步
        /// </summary>
        /// <returns></returns>
        Task<Bitmap> PreviewAsync();
        /// <summary>
        /// 打印
        /// </summary>
        void Print();
        /// <summary>
        /// 打印 异步
        /// </summary>
        Task PrintAsync();
    }
}

// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：ImageQueryService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、Extension.UI.Controls、Extension.IO.Files、Extension.IO.Csv、Extension.IO.Retention、Extension.Core.Models。导入不代表所有类型都会在运行时加载。
using WPFControls;
using Extension.UI.Controls;
using Extension.IO.Files;
using Extension.IO.Csv;
using Extension.IO.Retention;
using Extension.Core.Models;
using Extension.Imaging.Models;
using Extension.Automation.Device;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WPFControls.Controls.Diagnostics;
using WPFControls.ProgramControl.Models;
using WPFCustomControls.Window.Views;

namespace WPFControls.ProgramControl.Services
{
    /// <summary>
    /// ImageQueryService 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// </summary>
    public class ImageQueryService
    {
        private readonly TryCatchHelper _errors;

       
        /// <summary>
        /// 初始化 ImageQueryService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public ImageQueryService(IContainerProvider container, TryCatchHelper? errors = null)
        {
            _errors = errors ?? new();

            
        }

        public ImageQueryModel Model { get; set; } = new ImageQueryModel();

        /// <summary>
        /// 查询图像信息
        /// </summary>
        /// <returns></returns>
        public void Query_ImageInfo()
        {
            Func<IEnumerable<ImageInfo>> func = () =>
            {
                List<ImageInfo> lists = new List<ImageInfo>();
                for (DateTime date = Model.DateTime_Start.Date; date <= Model.DateTime_End.Date; date = date.AddDays(1))
                {
                    // 构造日期路径: pPath//Year//Month//Day
                    string datePath = System.IO.Path.Combine(Model.ImageSavePath, DirectoryEx.GetPath(date));
                    //获取查询的文件信息
                    var d = DirectoryEx.ReadFile(datePath);
                    if (d != null)
                    {
                        List<ImageInfo> fileInfos = new List<ImageInfo>();
                        foreach (var item in d)
                        {
                            ImageInfo imageInfo = new ImageInfo();
                            imageInfo.ID = item.Key;
                            imageInfo.ImagePath = item.Value.Select(x => x.FullName).ToList();
                            fileInfos.Add(imageInfo);
                        }
                        lists.AddRange(fileInfos);
                    }
                }
                Model.ImageInfoDatas = new ObservableCollection<ImageInfo>(lists);

                return Model.ImageInfoDatas.ToList();
            };

            _errors.TryExecute(() => func.UIBeginInvoke());
        }


        /// <summary>
        /// 筛选图像信息
        /// </summary>
        /// <returns></returns>
        public void Filter_ImageInfo()
        {
            Func<List<ImageInfo>> action = () =>
             {
                 List<ImageInfo> d1 = new List<ImageInfo>();
                 if (!string.IsNullOrWhiteSpace(Model.ID))
                 {
                     foreach (var item in Model.ImageInfoDatas)
                     {
                         if (item.ID == Model.ID)
                             d1.Add(item);
                     }
                     Model.ImageInfoDatas = new ObservableCollection<ImageInfo>(d1);
                 }
                 return Model.ImageInfoDatas.ToList();
             };

            _errors.TryExecute(() => action.UIBeginInvoke());
        }

        /// <summary>
        /// 保存图像
        /// </summary>
        public async Task Save()
        {
            FileLoading fileLoading = new FileLoading();
            await _errors.TryExecuteAsync(async () =>
           {
               var ret = FileDialogEx.SaveFile();
               if (ret != null)
               {
                   fileLoading.Text = "文件保存中......";
                   fileLoading.Show();
                   if (Model.ImagePaths != null && Model.ImagePaths.Count != 0)
                   {
                       var path = DirectoryEx.GetParent(Model.ImagePaths[0]);
                       await DirectoryEx.DirCopy(path, ret);
                   }
                   else
                   {
                       foreach (var item in Model.ImageInfoDatas)
                       {
                           var path = DirectoryEx.GetParent(item.ImagePath[0]);
                           var desc = Path.Combine(ret, DirectoryEx.GetDirName(path));
                           await DirectoryEx.DirCopy(path, desc);
                       }
                   }
                   await Task.Delay(500);
                   fileLoading.Close();
               }
               else
               {
                   MessagePrompt messagePrompt = new MessagePrompt("提示", "未选择存储路径，请选择！！");
                   messagePrompt.ShowDialog();
               }
           }, (ex) => fileLoading.Close() );
        }
    }
}

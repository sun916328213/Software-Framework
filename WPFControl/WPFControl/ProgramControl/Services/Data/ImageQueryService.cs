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
using WPFControl.Helper;
using WPFControl.ProgramControl.Models;
using WPFCustomControls.Window.Views;

namespace WPFControl.ProgramControl.Services
{
    public class ImageQueryService
    {
       
        public ImageQueryService(IContainerProvider container)
        {
            
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

            TryCatchHelper.TryExecute(() => func.UIBeginInvoke());
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

            TryCatchHelper.TryExecute(() => action.UIBeginInvoke());
        }

        /// <summary>
        /// 保存图像
        /// </summary>
        public async Task Save()
        {
            FileLoading fileLoading = new FileLoading();
            await TryCatchHelper.TryExecuteAsync(async () =>
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

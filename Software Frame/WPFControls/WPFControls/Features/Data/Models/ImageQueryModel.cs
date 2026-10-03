// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：ImageQueryModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel、Extension.Core.Models、Extension.Imaging.Models、Extension.Automation.Device、Mapster。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using Extension.Core.Models;
using Extension.Imaging.Models;
using Extension.Automation.Device;
using Mapster;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace WPFControls.ProgramControl.Models
{
    /// <summary>
    /// ImageQueryModel 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// 继承或实现的契约：ObservableObject。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class ImageQueryModel : ObservableObject
    {
        string iImageSavePath;

        /// <summary>
        /// 查询图像存储路径
        /// </summary>
        public string ImageSavePath
        {
            get => iImageSavePath;
            set => SetProperty(ref iImageSavePath, value);
        }

        string id;
        /// <summary>
        /// 筛选ID
        /// </summary>
        public string ID
        {
            get => id;
            set => SetProperty(ref id, value);
        }


        DateTime starttime = DateTime.Now, endtime = DateTime.Now;
        /// <summary>
        /// 开始时间
        /// </summary>
        public DateTime DateTime_Start
        {
            get => starttime;
            set => SetProperty(ref starttime, value);
        }

        /// <summary>
        /// 结束时间
        /// </summary>
        public DateTime DateTime_End
        {
            get => endtime;
            set => SetProperty(ref endtime, value);
        }

        List<string> imagePaths = new List<string>();
        /// <summary>
        /// 选中图像信息的文件路径
        /// </summary>
        public List<string> ImagePaths
        {

            get => imagePaths;
            set => SetProperty(ref imagePaths, value);
        }

        private ImageInfoDTO _selectedItem;
        /// <summary>
        /// 选中的图像信息
        /// </summary>
        public ImageInfoDTO SelectedItem
        {
            get => _selectedItem;
            set
            {
                SetProperty(ref _selectedItem, value);
                // 当选中项改变时，更新ImagePaths
                if (_selectedItem.ID != string.Empty)
                {
                    var imageInfo = ImageInfoDatas.FirstOrDefault(info => info.ID == _selectedItem.ID);
                    if (imageInfo.ImagePath != null)
                        ImagePaths = imageInfo.ImagePath;
                }
            }
        }


        public ObservableCollection<ImageInfo> imageInfoDatas = new ObservableCollection<ImageInfo>();

        /// <summary>
        /// 所有图像信息集合
        /// </summary>
        public ObservableCollection<ImageInfo> ImageInfoDatas
        {
            get => imageInfoDatas;
            set
            {
                SetProperty(ref imageInfoDatas, value);
                OnImageInfoDatasCollectionChanged(null, null);
            }
        }


        ObservableCollection<ImageInfoDTO> imageInfoDatasDTO = new ObservableCollection<ImageInfoDTO>();

        /// <summary>
        /// 所有图像信息集合 DTO
        /// </summary>
        public ObservableCollection<ImageInfoDTO> ImageInfoDatasDTO
        {
            get => imageInfoDatasDTO;
            set => SetProperty(ref imageInfoDatasDTO, value);
        }



        /// <summary>
        /// 初始化 ImageQueryModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public ImageQueryModel()
        {
            // 订阅集合变化事件
            ImageInfoDatas.CollectionChanged += OnImageInfoDatasCollectionChanged;
        }

        /// <summary>
        /// 集合变化事件处理
        /// </summary>
        private void OnImageInfoDatasCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            ImageInfoDatasDTO = ImageInfoDatas.Adapt<ObservableCollection<ImageInfoDTO>>();
        }

    }
}
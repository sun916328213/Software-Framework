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

namespace WPFControl.ProgramControl.Models
{
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
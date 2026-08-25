using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace WPFControl.CustomControl.Models
{
    public class ManagerModel<T> : ObservableObject
    {
        string filepath, filename, devicename;
        /// <summary>
        /// 路径
        /// </summary>
        public string FilePath
        {
            get => filepath;
            set => SetProperty(ref filepath, value);
        }
        /// <summary>
        /// 用户信息文件路径
        /// </summary>
        public string FileName
        {
            get => filename;
            set => SetProperty(ref filename, value);
        }

        /// <summary>
        /// 设备名称
        /// </summary>
        public string Name
        {
            get => devicename;
            set => SetProperty(ref devicename, value);
        }

        private ObservableCollection<T> dataCollection = new ObservableCollection<T>();

        /// <summary>
        /// 数据集合
        /// </summary>
        public ObservableCollection<T> DataCollection
        {
            get => dataCollection;
            set => SetProperty(ref dataCollection, value);
        }
    }

}
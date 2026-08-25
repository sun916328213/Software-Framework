using Device.Camera.Gige.Definition;
using Device.Light.Definition;
using Extension.Core.ConvertEx;
using Extension.DesignPattern.ObserverPattern.Examples;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WPFControl.Helper;
using WPFControl.ProgramControl.Models;

namespace WPFControl.ProgramControl.Services
{
    public class VisionInspectionItemService
    {
        public VisionInspectionItemModel Model { get; set; } = new VisionInspectionItemModel();
 
        private CamManagerService camManagerService;

        public VisionInspectionItemService(IContainerProvider container)
        {
            camManagerService = container.Resolve<CamManagerService>();

            Load();
        }

        /// <summary>
        /// 根据名称获取检测项名称
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public VisionInspectionItem GetItemByName(string name)
        {
            return Model.DataCollection.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        }


        /// <summary>
        /// 加载相机列表
        /// </summary>
        public void Load()
        {
              TryCatchHelper.TryExecute (  () =>
            {
                // 读取新数据并更新现有集合，而不是替换引用
                var newData = JsonEx.Read<ObservableCollection<VisionInspectionItem>>(Model.FilePath, Model.FileName);
                if (newData != null)
                    Model.DataCollection = newData;
            });
        }

        public void Save()
        {
            TryCatchHelper.TryExecute(() =>
            {
                JsonEx.Save(Model.DataCollection, Model.FilePath, Model.FileName);
            });
        }

    
    }
}

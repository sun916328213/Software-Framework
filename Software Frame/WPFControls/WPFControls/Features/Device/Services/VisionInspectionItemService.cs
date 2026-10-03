// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：VisionInspectionItemService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、Extension.Core.Conversion、System、System.Collections.Generic、System.Collections.ObjectModel、System.Linq。导入不代表所有类型都会在运行时加载。
using WPFControls;
using Extension.Core.Conversion;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WPFControls.Controls.Diagnostics;
using WPFControls.ProgramControl.Models;

namespace WPFControls.ProgramControl.Services
{
    /// <summary>
    /// VisionInspectionItemService 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// </summary>
    public class VisionInspectionItemService
    {
        private readonly TryCatchHelper _errors;

        public VisionInspectionItemModel Model { get; set; } = new VisionInspectionItemModel();
 


        /// <summary>
        /// 初始化 VisionInspectionItemService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Load；具体条件和先后次序见方法体。
        /// </summary>
        public VisionInspectionItemService(TryCatchHelper? errors = null)
        {
            _errors = errors ?? new();



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
              _errors.TryExecute (  () =>
            {
                // 读取新数据并更新现有集合，而不是替换引用
                var newData = JsonEx.Read<ObservableCollection<VisionInspectionItem>>(Model.FilePath, Model.FileName);
                if (newData != null)
                    Model.DataCollection = newData;
            });
        }

        /// <summary>
        /// 执行保存操作；对应入口为 Save。
        /// 正文中的关键协作调用包括 TryExecute、Save；具体条件和先后次序见方法体。
        /// </summary>
        public void Save()
        {
            _errors.TryExecute(() =>
            {
                JsonEx.Save(Model.DataCollection, Model.FilePath, Model.FileName);
            });
        }

    
    }
}


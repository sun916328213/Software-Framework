// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：ControlService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、WPFControls.Controls.Diagnostics、WPFControls.ProgramControl.Models、System、System.Collections.Generic、System.Collections.ObjectModel。导入不代表所有类型都会在运行时加载。
using WPFControls;
using WPFControls.Controls.Diagnostics;
using WPFControls.ProgramControl.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WPFControls.ProgramControl.Services
{
    /// <summary>
    /// 控制服务类
    /// 负责处理控制菜单的业务逻辑
    /// </summary>
    public class ControlService
    {
        private readonly TryCatchHelper _errors;
        /// <summary>
        /// 初始化 ControlService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public ControlService(TryCatchHelper? errors = null) => _errors = errors ?? new();

        /// <summary>
        /// 数据模型
        /// </summary>
        internal ControlModel Model { get; private set; } = new ControlModel();

        /// <summary>
        /// 控制菜单集合（提供对Model中ControlItems的访问）
        /// </summary>
        internal ObservableCollection<CControlItem> ControlItems => Model.ControlItems;

        /// <summary>
        /// 添加控制菜单项
        /// </summary>
        /// <param name="pItem">要添加的菜单项</param>
        public void Add(CControlItem pItem)
        {
            _errors.TryExecute(() =>
            {
                Model.ControlItems.Add(pItem);
            });
        }

        /// <summary>
        /// 批量添加控制菜单操作
        /// </summary>
        /// <param name="pActions">要添加的操作列表</param>
        public void AddAction(List<Action> pActions)
        {
            _errors.TryExecute(() =>
            {
                for (int i = 0; i < pActions.Count; i++)
                {
                    if (i < Model.ControlItems.Count)
                    {
                        Model.ControlItems[i].Action = pActions[i];
                    }
                }
            });
        }

        /// <summary>
        /// 修改指定索引位置的控制菜单操作
        /// </summary>
        /// <param name="pIndex">索引位置</param>
        /// <param name="pAction">新的操作</param>
        public void ModifyAction(int pIndex, Action pAction)
        {
            _errors.TryExecute(() =>
            {
                if (pIndex >= 0 && pIndex < Model.ControlItems.Count)
                {
                    Model.ControlItems[pIndex].Action = pAction;
                }
            });
        }

        /// <summary>
        /// 批量添加控制菜单项
        /// </summary>
        /// <param name="pItem">要添加的菜单项列表</param>
        public void AddRange(List<CControlItem> pItem)
        {
            _errors.TryExecute(() =>
            {
                foreach (var item in pItem)
                {
                    Model.ControlItems.Add(item);
                }
            });
        }

        /// <summary>
        /// 清空所有控制菜单项
        /// </summary>
        public void Clear()
        {
            _errors.TryExecute(() =>
            {
                Model.ControlItems.Clear();
            });
        }    
    }
}
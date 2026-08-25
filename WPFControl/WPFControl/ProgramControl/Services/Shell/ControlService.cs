using WPFControl.Helper;
using WPFControl.ProgramControl.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WPFControl.ProgramControl.Services
{
    /// <summary>
    /// 控制服务类
    /// 负责处理控制菜单的业务逻辑
    /// </summary>
    public class ControlService
    {
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
            TryCatchHelper.TryExecute(() =>
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
            TryCatchHelper.TryExecute(() =>
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
            TryCatchHelper.TryExecute(() =>
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
            TryCatchHelper.TryExecute(() =>
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
            TryCatchHelper.TryExecute(() =>
            {
                Model.ControlItems.Clear();
            });
        }    
    }
}
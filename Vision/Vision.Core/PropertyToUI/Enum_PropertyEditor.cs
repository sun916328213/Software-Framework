using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vision.Core
{
    public enum E_PropertyEditorType
    {
        /// <summary>
        /// 字符串
        /// </summary>
        Text,
        /// <summary>
        /// 整形数据
        /// </summary>
        Int,
        /// <summary>
        /// 浮点形数据
        /// </summary>
        Double,
        /// <summary>
        /// 单选
        /// </summary>
        SingleSelect,
        /// <summary>
        /// 多选
        /// </summary>
        MultiSelect,
        /// <summary>
        /// 选择
        /// </summary>
        Checkbox,
        /// <summary>
        /// 用于命令操作的按钮，类中需要实现处理动作
        /// </summary>
        Button,
        /// <summary>
        /// 用于列表展示的数据
        /// </summary>
        DataGridList,
        /// <summary>
        /// 用于列表展示带数据的集合类
        /// </summary>
        DataClassList,
    }
}

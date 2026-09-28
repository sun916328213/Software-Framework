using Extension.Core.Collections;
using System.Collections.Generic;

namespace Secs.Variable
{

    interface IData<T>
    {
 
        /// <summary>
        /// 文件路径
        /// </summary>
        string FilePath { get; set; }

        /// <summary>
        /// 文件名称
        /// </summary>
        string FileName { get; set; }

        /// <summary>
        ///  数据集合
        /// </summary>
        ConcurrentList<T> Datas { get; set; }

        /// <summary>
        /// 加载数据
        /// </summary>
        void Load();

        /// <summary>
        /// 保存数据到文件
        /// </summary>
        void Save();

        /// <summary>
        /// 获取数据集合
        /// </summary>
        /// <returns></returns>
        List<T> GetDatas();

    }
}

// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：ActiveReceiveInfo、ActiveReceive。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Runtime、SecsGem.Runtime.Variable、Extension.Core.Collections、Prism.Ioc、System。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Runtime;
using SecsGem.Runtime.Variable;
using Extension.Core.Collections;
using Prism.Ioc;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecsGem.Runtime.Variable.Data
{
    /// <summary>  
    /// 主动接收数据结构体  目前只是S2F41使用
    /// </summary>  
    public class   ActiveReceiveInfo
    {
        /// <summary>
        /// 数据指令
        /// </summary>
        public string Command { get; set; }
        /// <summary>
        /// RCMD名称
        /// </summary>
        public string RCMD { get; set; }
        /// <summary>
        ///  命令参数集合
        /// </summary>
        public List<string> CommandParam { get; set; }
        /// <summary>
        /// 初始化 ActiveReceiveInfo 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public ActiveReceiveInfo() { }
        /// <summary>
        /// 初始化 ActiveReceiveInfo 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public ActiveReceiveInfo(string command, string rCMD, List<string> commandParam)
        {
            Command = command;
            RCMD = rCMD;
            CommandParam = commandParam;
        }
    }

    /// <summary>
    /// ActiveReceive 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
    /// 继承或实现的契约：DataBase&lt;ActiveReceiveInfo&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class ActiveReceive:DataBase<ActiveReceiveInfo>
    {
        /// <summary>
        /// 初始化 ActiveReceive 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public ActiveReceive()
        {
        }

        public override string FilePath { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override string FileName { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override ConcurrentList<ActiveReceiveInfo> Datas { get; set; }=new ConcurrentList<ActiveReceiveInfo>();

        /// <summary>
        /// 获取并删除数据
        /// </summary>
        /// <param name="command">指令名称</param>
        /// <returns></returns>
        public ActiveReceiveInfo GetandRemoveData(string command, string rcmd)
        {
          var data= GetData(command,rcmd);
            if (data != null)
            {
                Datas.Remove(data);
            }
            return data;
        }

        /// <summary>
        /// 根据Command获取数据
        /// </summary>
        /// <param name="command">指令名称</param>
        /// <returns></returns>
        public ActiveReceiveInfo GetData(string command)
        {
            return Datas.FirstOrDefault(x => x.Command == command);
        }

        /// <summary>
        /// 根据Command获取数据
        /// </summary>
        /// <param name="command">指令名称</param>
        /// <param name="rcmd">RCMD</param>
        /// <returns></returns>
        public ActiveReceiveInfo GetData(string command,string rcmd)
        {
            return Datas.FirstOrDefault(x => x.Command == command&&x.RCMD== rcmd);
        }

        /// <summary>
        /// 添加数据
        /// </summary>
        /// <param name="data"></param>
        public void AddData(ActiveReceiveInfo data)
        {
            Datas.Add(data);
        }
 

        /// <summary>
        /// 执行加载操作；对应入口为 Load。
        /// </summary>
        public override void Load()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 执行保存操作；对应入口为 Save。
        /// </summary>
        public override void Save()
        {
            throw new NotImplementedException();
        }
    }
}

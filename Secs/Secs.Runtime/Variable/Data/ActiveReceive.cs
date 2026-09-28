using Extension.Core.Collections;
using Prism.Ioc;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Secs.Variable.Data
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
        public ActiveReceiveInfo() { }
        public ActiveReceiveInfo(string command, string rCMD, List<string> commandParam)
        {
            Command = command;
            RCMD = rCMD;
            CommandParam = commandParam;
        }
    }

    public class ActiveReceive:DataBase<ActiveReceiveInfo>
    {
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
 

        public override void Load()
        {
            throw new NotImplementedException();
        }

        public override void Save()
        {
            throw new NotImplementedException();
        }
    }
}

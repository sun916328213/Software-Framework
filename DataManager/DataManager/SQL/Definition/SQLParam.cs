using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataManager.SQL.Definition
{
    public class SQLParam
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 数据库服务器IP
        /// </summary>
        public string ServerIP { get; set; }
        /// <summary>
        /// 数据库端口
        /// </summary>
        public string Port { get; set; }
        /// <summary>
        /// 数据库名称
        /// </summary>
        public string DBName { get; set; }
        /// <summary>
        /// 数据库用户名
        /// </summary>
        public string UserName { get; set; }
        /// <summary>
        /// 数据库密码
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// 数据库类型
        /// </summary>
        public ESQLType DBType { get; set; }= ESQLType.PostgreSQL;
        /// <summary>
        /// 数据库工具类型
        /// </summary>
        public ESQLToolType ToolType { get; set; }= ESQLToolType.EFCore;
    }
}

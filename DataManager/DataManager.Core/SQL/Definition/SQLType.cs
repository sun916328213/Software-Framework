using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataManager.SQL.Definition
{
    public enum ESQLType
    {
        MySQL,
        PostgreSQL,
        SQLite,
        SQL_Server
    }

    public enum ESQLToolType
    {
        EFCore,
        SqlSugar,
        Dapper,
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EditorDemo
{
    public class Data
    { 
        public int ID { get; set; }

        public string Name { get; set; }
    }

    
    public  class DataList
    {
        public List<Data> Datas { get; set; } = new List<Data>();

        public DataList()
        {
            Data data1 = new Data() { ID = 1, Name = "S1" };
            Data data2 = new Data() { ID = 2, Name = "S2" };
            Data data3 = new Data() { ID = 3, Name = "S3" };

            Datas.Add(data1);
            Datas.Add(data2);
            Datas.Add(data3);
                
        }

        public int GetCount()
        {
            return Datas.Count;
        }
    }
}

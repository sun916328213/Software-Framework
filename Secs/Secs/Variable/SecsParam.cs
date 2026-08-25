using Extension.Core.FileEx;
using Extension.Core.ConvertEx;
using System.IO;

namespace Secs.Variable
{
    /// <summary>
    /// secs连接参数
    /// </summary>
    public class CSecsParam
    {
        public bool IsActiveMode { get; set; }=true;
        public string IpAddress { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 5000;
        public int DeviceId { get; set; } = 0;
        public int BufferSize { get; set; } = 65535;
    }
    public class SecsParam
    {
        internal  CSecsParam _SecsParam ;

        /// <summary>
        /// 文件路径
        /// </summary>
        internal string FilePath = "Config//SecsJson";

        /// <summary>
        /// 文件名
        /// </summary>
        internal string FileName= "SecsParam.json";

        public SecsParam()
        {
            _SecsParam= _SecsParam ??  Load();
        }

        public CSecsParam Load()
        {
            var data = FileEx.Read(FilePath, FileName);
            return data.FromJson<CSecsParam>();
        }

        public void Save()
        {
            string data = _SecsParam.ToJson();
            data.Write(FilePath, FileName);
        }
    }
}

using Extension.IO.Files;
using Extension.IO.Csv;
using Extension.IO.Retention;
using Extension.Core.Conversion;
using System.IO;

namespace Secs.Variable
{
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

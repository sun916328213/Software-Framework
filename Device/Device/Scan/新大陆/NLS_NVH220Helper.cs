using System;
using Device.Scan.Definition;

namespace Device.Scan.新大陆
{
    internal class NLS_NVH220Helper : ScanBase
    {


        /// <summary>
        /// 连接器的类型
        /// </summary>
        public override ConnectorType Connector => ConnectorType.USB;



        public override void Trigger()
        {
            throw new Exception($"当前扫码枪 型号:NLS_NVH220;  不支持软件触发。");
        }

    }
}

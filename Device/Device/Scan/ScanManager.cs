using Device.Scan.Definition;
using Device.Scan.新大陆;
using Device.Scan;
using System;
using System.Collections.Generic;

namespace Device.Scan
{
    public class ScanManager : ManagerBase<IScan, ScanParam>
    {
        public override void Config(List<ScanParam> param)
        {
            Devices.Clear();
            foreach (var item in param)
                Create(item);
        }

        public override IScan Create(ScanParam param)
        {
            switch (param.ScanType)
            {
                case EScanType.新大陆:
                    var xdl = new NLS_NVH220Helper();
                    xdl.ScanParam= param;
                    Devices.Add(xdl);
                    return xdl;
                default: throw new NotSupportedException($"{param.ScanType} not support.");
            }
        }
    }
}
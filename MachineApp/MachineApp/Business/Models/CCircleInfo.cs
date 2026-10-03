// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：CCircleInfo。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：HalconDotNet、System.Windows。导入不代表所有类型都会在运行时加载。
using HalconDotNet;
using System.Windows;

namespace MachineApp.Business.Models;

/// <summary>
/// CCircleInfo 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
/// </summary>
public class CCircleInfo
{
    public Point Center { get; set; }
    public double R { get; set; }
    public HObject Xld { get; set; }
}

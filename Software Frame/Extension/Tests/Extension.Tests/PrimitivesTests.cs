// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：PrimitivesTests、DeviceState。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.ComponentModel、System.Linq、Extension.Core.Comparison、Extension.Core.Conversion、Extension.Core.Enums。导入不代表所有类型都会在运行时加载。
using System;
using System.ComponentModel;
using System.Linq;
using Extension.Core.Comparison;
using Extension.Core.Conversion;
using Extension.Core.Enums;
using Xunit;

namespace Extension.Tests
{
    /// <summary>
    /// 首批测试只覆盖纯逻辑（无 IO、无 vendor SDK、无 UI），无需 seam 即可运行。
    /// 需要 IO / 原生 SDK 的类型（FileEx、ProcessEx、ImageEx、ThemeService 等）
    /// 必须先抽出 IFileSystem / IProcessRunner 这类 seam 才能测，本工程暂不覆盖。
    /// </summary>
    public class PrimitivesTests
    {
        /// <summary>
        /// DeviceState 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        private enum DeviceState
        {
            [Description("空闲")]
            Idle = 0,
            [Description("运行中")]
            Running = 1,
            // 无 Description：按实现应回退到枚举名
            Fault = 2,
        }

        // ---------- StringEx.PaddingLength ----------

        /// <summary>
        /// 测试相关入口 PaddingLength_短字符串左补零；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、PaddingLength；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void PaddingLength_短字符串左补零()
        {
            Assert.Equal("00123", "123".PaddingLength(5));
        }

        /// <summary>
        /// 测试相关入口 PaddingLength_长字符串截尾保留右对齐；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、PaddingLength；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void PaddingLength_长字符串截尾保留右对齐()
        {
            Assert.Equal("345", "12345".PaddingLength(3));
        }

        /// <summary>
        /// 测试相关入口 PaddingLength_长度为零时保持原样；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、PaddingLength；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void PaddingLength_长度为零时保持原样()
        {
            Assert.Equal("123", "123".PaddingLength());
            Assert.Equal("123", "123".PaddingLength(0));
        }

        // ---------- EnumEx ----------

        /// <summary>
        /// 测试相关入口 GetDescription_有Description返回描述；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、GetDescription；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void GetDescription_有Description返回描述()
        {
            Assert.Equal("空闲", DeviceState.Idle.GetDescription());
        }

        /// <summary>
        /// 测试相关入口 GetDescription_无Description回退枚举名；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、GetDescription；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void GetDescription_无Description回退枚举名()
        {
            Assert.Equal("Fault", DeviceState.Fault.GetDescription());
        }

        /// <summary>
        /// 测试相关入口 GetDescription_null返回空串；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、GetDescription；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void GetDescription_null返回空串()
        {
            Assert.Equal(string.Empty, EnumEx.GetDescription((Enum)null));
        }

        /// <summary>
        /// 测试相关入口 GetName_返回全部枚举名；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、GetName；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void GetName_返回全部枚举名()
        {
            Assert.Equal(new[] { "Idle", "Running", "Fault" }, EnumEx.GetName<DeviceState>());
        }

        /// <summary>
        /// 测试相关入口 GetEnum_按名解析；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、GetEnum；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void GetEnum_按名解析()
        {
            Assert.Equal(DeviceState.Running, "Running".GetEnum<DeviceState>());
        }

        /// <summary>
        /// 测试相关入口 GetAllDescriptions_值与描述一一对应；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 GetAllDescriptions、Equal、First；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void GetAllDescriptions_值与描述一一对应()
        {
            var all = EnumEx.GetAllDescriptions<DeviceState>();
            Assert.Equal(3, all.Length);
            Assert.Equal("空闲", all.First(x => x.Value == DeviceState.Idle).Description);
            Assert.Equal("Fault", all.First(x => x.Value == DeviceState.Fault).Description);
        }

        // ---------- BitOperationEx.Compare_Ptr(byte[]) ----------

        /// <summary>
        /// 测试相关入口 Compare_Ptr_相同数组返回空；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Empty、Compare_Ptr；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Compare_Ptr_相同数组返回空()
        {
            var a = new byte[] { 1, 2, 3, 4 };
            var b = new byte[] { 1, 2, 3, 4 };
            Assert.Empty(BitOperationEx.Compare_Ptr(a, b));
        }

        /// <summary>
        /// 测试相关入口 Compare_Ptr_双边沿记录差异点及前后值；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Compare_Ptr、Single、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Compare_Ptr_双边沿记录差异点及前后值()
        {
            var a = new byte[] { 1, 2, 3, 4 };
            var b = new byte[] { 1, 9, 3, 4 };
            var result = BitOperationEx.Compare_Ptr(a, b, EComparePolarity.BothEdges);
            var single = Assert.Single(result);
            Assert.Equal(1, single.Index);
            Assert.Equal((object)(byte)2, single.Value_Current); // b1 的值
            Assert.Equal((object)(byte)9, single.Value_Last);    // b2 的值
        }

        /// <summary>
        /// 测试相关入口 Compare_Ptr_上升沿只记录变大点；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Compare_Ptr、Single、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Compare_Ptr_上升沿只记录变大点()
        {
            var a = new byte[] { 5, 1 };
            var b = new byte[] { 1, 9 };
            var result = BitOperationEx.Compare_Ptr(a, b, EComparePolarity.RisingEdge);
            var single = Assert.Single(result);
            Assert.Equal(0, single.Index);
        }

        /// <summary>
        /// 测试相关入口 Compare_Ptr_空数组抛异常；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Throws、Compare_Ptr；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Compare_Ptr_空数组抛异常()
        {
            Assert.Throws<Exception>(() => BitOperationEx.Compare_Ptr(null, new byte[] { 1 }));
        }

        /// <summary>
        /// 测试相关入口 Compare_Ptr_长度不等抛异常；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Throws、Compare_Ptr；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Compare_Ptr_长度不等抛异常()
        {
            Assert.Throws<Exception>(() => BitOperationEx.Compare_Ptr(
                new byte[] { 1, 2 }, new byte[] { 1 }));
        }
    }
}

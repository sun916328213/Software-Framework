// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ContractsTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.Linq、Extension.Core.Models、Extension.Imaging.Models、Extension.Integration.Mapster。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.Linq;
using Extension.Core.Models;
using Extension.Imaging.Models;
using Extension.Integration.Mapster;
using Extension.Automation.Device;
using Extension.Automation.Secs;
using Mapster;
using Xunit;

namespace Extension.Tests
{
    /// <summary>
    /// <c>Contracts/</c> 的 T0 层特征测试（2026-09-18 补测第四轮）。
    /// </summary>
    /// <remarks>
    /// 本目录 3 个文件的分级并不整齐：`DeviceStatus.cs`（纯枚举）和 `KeyValue.cs` 是 T0，
    /// 而 `ImageInfo.cs` 因为 `using Mapster` 是 **T2**（见 FILE-TIERING.md §2）。
    /// 这里把它们放在同一个测试文件里，是因为它们都是"跨模块契约"这一职责的成员 ——
    /// 测试文件按**职责**归类，不按级别归类（与 TierGuardTests 的分级是两件事）。
    ///
    /// 本文件同时钉住 §5.4 / §5.5 记的两个反模式：
    /// `ImageInfo` 用可变 `struct` 承载契约、以及 `Contracts` ↔ `Runtime/Mapping` 的双向耦合。
    /// </remarks>
    public class ContractsTests
    {
        // ------------------------------------------------------------ EDeviceStatus

        /// <summary>
        /// 验证 EDeviceStatus_取值固定_不得随意重排 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、GetValues；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void EDeviceStatus_取值固定_不得随意重排()
        {
            // 这些数值会被写进日志/PLC 报文，重排等于破坏兼容性 —— 钉住。
            Assert.Equal(0, (int)EDeviceStatus.Idle);
            Assert.Equal(1, (int)EDeviceStatus.Ready);
            Assert.Equal(2, (int)EDeviceStatus.Running);
            Assert.Equal(3, (int)EDeviceStatus.Paused);
            Assert.Equal(4, (int)EDeviceStatus.Error);
            Assert.Equal(5, Enum.GetValues<EDeviceStatus>().Length);
        }

        /// <summary>
        /// 验证 ESecsMode_取值固定 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、GetValues；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ESecsMode_取值固定()
        {
            Assert.Equal(0, (int)ESecsMode.Offline);
            Assert.Equal(1, (int)ESecsMode.Online_Local);
            Assert.Equal(2, (int)ESecsMode.Online_Remote);
            Assert.Equal(3, Enum.GetValues<ESecsMode>().Length);
        }

        /// <summary>
        /// 验证 ESecsMode_成员名用了下划线_不是常见命名风格 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Contains、GetNames；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ESecsMode_成员名用了下划线_不是常见命名风格()
        {
            // 只记录事实：`Online_Local` / `Online_Remote` 带下划线，
            // 与 `EDeviceStatus` 的 `Idle` / `Ready` 风格不一致。改名会破坏下游，故仅钉住。
            Assert.Contains("Online_Local", Enum.GetNames<ESecsMode>());
            Assert.Contains("Online_Remote", Enum.GetNames<ESecsMode>());
        }

        // ---------------------------------------------------------------- KeyValue

        /// <summary>
        /// 验证 KeyValue_无参构造_两个属性都是null 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Null；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void KeyValue_无参构造_两个属性都是null()
        {
            var kv = new KeyValue();

            Assert.Null(kv.Key);
            Assert.Null(kv.Value);
        }

        /// <summary>
        /// 验证 KeyValue_有参构造_按位置赋值 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void KeyValue_有参构造_按位置赋值()
        {
            var kv = new KeyValue("k", "v");

            Assert.Equal("k", kv.Key);
            Assert.Equal("v", kv.Value);
        }

        /// <summary>
        /// 验证 KeyValue_是引用类型_赋值共享同一个实例 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Same、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void KeyValue_是引用类型_赋值共享同一个实例()
        {
            var a = new KeyValue("k", "v");
            var b = a;

            b.Key = "changed";

            Assert.Same(a, b);
            Assert.Equal("changed", a.Key); // 改 b 就是改 a
        }

        /// <summary>
        /// 验证 KeyValue_没有重写Equals_内容相同也不相等 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 NotEqual、False、Equals、Equal、Count；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void KeyValue_没有重写Equals_内容相同也不相等()
        {
            // ⛔ 缺陷（只记录 + 钉住，未改产品代码）：
            // `KeyValue` 名字上是个"值对象"，却没重写 Equals/GetHashCode，
            // 于是 `Contains` / `Distinct` / `Dictionary` 比较全按**引用**走。
            // 下游若拿它做配置项比对（"这两份配置一样吗"），会得到反直觉的 false。
            var a = new KeyValue("k", "v");
            var b = new KeyValue("k", "v");

            Assert.NotEqual(a, b);
            Assert.False(a.Equals(b));
            Assert.Equal(2, new List<KeyValue> { a, b }.Distinct().Count());
        }

        // --------------------------------------------------------------- ImageInfo

        /// <summary>
        /// 验证 ImageInfo_是结构体_赋值是拷贝 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ImageInfo_是结构体_赋值是拷贝()
        {
            var a = new ImageInfo { ID = "x", ImagePath = new List<string> { "p" } };
            var b = a;

            b.ID = "y";

            Assert.Equal("x", a.ID); // struct ⇒ 值拷贝，互不影响
            Assert.Equal("y", b.ID);
        }

        /// <summary>
        /// 验证 ImageInfo_结构体里的引用成员_浅拷贝共享同一个List 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Add、Equal、Same；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ImageInfo_结构体里的引用成员_浅拷贝共享同一个List()
        {
            // ⛔ 反模式（§5.4，只记录 + 钉住，未改产品代码）：
            // 契约是 struct（看起来"值语义、可安全复制"），却带一个可变 List 成员 ⇒
            // 复制出来的实例与原实例**共享同一个 List**，改一个动两个。
            // 这类 bug 极难定位：从 `var b = a;` 完全看不出 a 会被影响。
            var a = new ImageInfo { ID = "x", ImagePath = new List<string> { "p" } };
            var b = a;

            b.ImagePath.Add("q");

            Assert.Equal(2, a.ImagePath.Count);
            Assert.Same(a.ImagePath, b.ImagePath);
        }

        /// <summary>
        /// 验证 ImageInfo_默认值_成员为null 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Null；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ImageInfo_默认值_成员为null()
        {
            ImageInfo d = default;

            Assert.Null(d.ID);
            Assert.Null(d.ImagePath);
        }

        /// <summary>
        /// 验证 ImageInfoDTO_是结构体_成员是值类型加字符串 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 True、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ImageInfoDTO_是结构体_成员是值类型加字符串()
        {
            var dto = new ImageInfoDTO { ID = "x", ImageCount = 3 };

            Assert.True(typeof(ImageInfoDTO).IsValueType);
            Assert.Equal("x", dto.ID);
            Assert.Equal(3, dto.ImageCount);
        }

        // --------------------------------------------------------- ImageInfoMapping

        /// <summary>
        /// 验证 ImageInfoMapping_正向映射_ImageCount取路径条数 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Configure、Adapt、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ImageInfoMapping_正向映射_ImageCount取路径条数()
        {
            var config = new TypeAdapterConfig();
            ImageInfoMapping.Configure(config);

            var src = new ImageInfo { ID = "img", ImagePath = new List<string> { "a", "b", "c" } };

            var dto = src.Adapt<ImageInfoDTO>(config);

            Assert.Equal("img", dto.ID);
            Assert.Equal(3, dto.ImageCount);
        }

        /// <summary>
        /// 验证 ImageInfoMapping_正向映射_路径为null时计数为0不抛 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Configure、Adapt、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ImageInfoMapping_正向映射_路径为null时计数为0不抛()
        {
            var config = new TypeAdapterConfig();
            ImageInfoMapping.Configure(config);

            var dto = new ImageInfo { ID = "img", ImagePath = null }.Adapt<ImageInfoDTO>(config);

            Assert.Equal(0, dto.ImageCount);
        }

        /// <summary>
        /// 验证 ImageInfoMapping_反向映射_ImagePath被忽略 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Configure、Adapt、Equal、Null；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ImageInfoMapping_反向映射_ImagePath被忽略()
        {
            var config = new TypeAdapterConfig();
            ImageInfoMapping.Configure(config);

            var dto = new ImageInfoDTO { ID = "img", ImageCount = 7 };

            var back = dto.Adapt<ImageInfo>(config);

            Assert.Equal("img", back.ID);
            Assert.Null(back.ImagePath); // Ignore(dest => dest.ImagePath)
        }

        /// <summary>
        /// 验证 ImageInfoMapping_是T2依赖_这个文件靠Mapster才能编译 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 GetName、NotNull、Contains；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ImageInfoMapping_是T2依赖_这个文件靠Mapster才能编译()
        {
            // 钉住分级事实：`Contracts/` 里只有 ImageInfo.cs 需要第三方包，
            // 也正是它让 `Contracts` 整体不能算纯 T0（见 TierGuardTests.Allowed 的登记）。
            var mapsterAssembly = typeof(TypeAdapterConfig).Assembly.GetName().Name;

            Assert.NotNull(mapsterAssembly);
            Assert.Contains("Mapster", mapsterAssembly, StringComparison.OrdinalIgnoreCase);
        }
    }
}

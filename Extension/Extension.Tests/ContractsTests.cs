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

        [Fact]
        public void ESecsMode_取值固定()
        {
            Assert.Equal(0, (int)ESecsMode.Offline);
            Assert.Equal(1, (int)ESecsMode.Online_Local);
            Assert.Equal(2, (int)ESecsMode.Online_Remote);
            Assert.Equal(3, Enum.GetValues<ESecsMode>().Length);
        }

        [Fact]
        public void ESecsMode_成员名用了下划线_不是常见命名风格()
        {
            // 只记录事实：`Online_Local` / `Online_Remote` 带下划线，
            // 与 `EDeviceStatus` 的 `Idle` / `Ready` 风格不一致。改名会破坏下游，故仅钉住。
            Assert.Contains("Online_Local", Enum.GetNames<ESecsMode>());
            Assert.Contains("Online_Remote", Enum.GetNames<ESecsMode>());
        }

        // ---------------------------------------------------------------- KeyValue

        [Fact]
        public void KeyValue_无参构造_两个属性都是null()
        {
            var kv = new KeyValue();

            Assert.Null(kv.Key);
            Assert.Null(kv.Value);
        }

        [Fact]
        public void KeyValue_有参构造_按位置赋值()
        {
            var kv = new KeyValue("k", "v");

            Assert.Equal("k", kv.Key);
            Assert.Equal("v", kv.Value);
        }

        [Fact]
        public void KeyValue_是引用类型_赋值共享同一个实例()
        {
            var a = new KeyValue("k", "v");
            var b = a;

            b.Key = "changed";

            Assert.Same(a, b);
            Assert.Equal("changed", a.Key); // 改 b 就是改 a
        }

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

        [Fact]
        public void ImageInfo_是结构体_赋值是拷贝()
        {
            var a = new ImageInfo { ID = "x", ImagePath = new List<string> { "p" } };
            var b = a;

            b.ID = "y";

            Assert.Equal("x", a.ID); // struct ⇒ 值拷贝，互不影响
            Assert.Equal("y", b.ID);
        }

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

        [Fact]
        public void ImageInfo_默认值_成员为null()
        {
            ImageInfo d = default;

            Assert.Null(d.ID);
            Assert.Null(d.ImagePath);
        }

        [Fact]
        public void ImageInfoDTO_是结构体_成员是值类型加字符串()
        {
            var dto = new ImageInfoDTO { ID = "x", ImageCount = 3 };

            Assert.True(typeof(ImageInfoDTO).IsValueType);
            Assert.Equal("x", dto.ID);
            Assert.Equal(3, dto.ImageCount);
        }

        // --------------------------------------------------------- ImageInfoMapping

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

        [Fact]
        public void ImageInfoMapping_正向映射_路径为null时计数为0不抛()
        {
            var config = new TypeAdapterConfig();
            ImageInfoMapping.Configure(config);

            var dto = new ImageInfo { ID = "img", ImagePath = null }.Adapt<ImageInfoDTO>(config);

            Assert.Equal(0, dto.ImageCount);
        }

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

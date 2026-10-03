// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：LegacyDeviceConfigImportResult、LegacyDeviceConfigImporter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Controls、System、System.Collections.Generic、System.Globalization、System.IO、System.Text.Json。导入不代表所有类型都会在运行时加载。
using Device.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Device.Controls.Models;
using Device.Core.Cameras;
using Extension.Core.Conversion;

namespace Device.Controls.Configuration
{
    /// <summary>
    /// 一次迁移的结果：生成了哪些新配置、跳过了哪些、哪些条目需要人工确认。
    /// </summary>
    public sealed class LegacyDeviceConfigImportResult
    {
        /// <summary>本次实际生成的新配置文件名。</summary>
        public List<string> Imported { get; } = [];

        /// <summary>因新文件已存在（或旧文件不存在）而跳过的文件名。</summary>
        public List<string> Skipped { get; } = [];

        /// <summary>需要人工确认的条目说明；非空表示迁移结果不完整。</summary>
        public List<string> Warnings { get; } = [];

        /// <summary>是否存在需要人工确认的条目。</summary>
        public bool HasWarnings => Warnings.Count > 0;
    }

    /// <summary>
    /// 旧 Device 配置 → 新 Device 栈配置的一次性、幂等、非破坏式迁移器。
    ///
    /// 背景（P8 计划 P8-3）：旧页面读 <c>Config/CamParam.json</c> 等文件，新页面读
    /// <c>Config/Cameras.json</c> 等文件。若不迁移，现场切到新页面后设备列表会是空的
    /// ——「编译成功但全部回默认值」。
    ///
    /// 三条硬原则：
    /// <list type="number">
    /// <item>只有「新文件不存在 + 旧文件存在」时才迁移；</item>
    /// <item>只生成新文件，<b>绝不覆盖或删除旧文件</b>（旧 Device 栈仍在被 Packing Machine 使用）；</item>
    /// <item>重复启动结果一致（幂等）；语义不明确的旧值<b>不猜</b>，一律记入
    /// <see cref="LegacyDeviceConfigImportResult.Warnings"/> 交人工确认。</item>
    /// </list>
    ///
    /// 本类刻意<b>不引用旧 Device 程序集</b>：旧 JSON 用 <see cref="JsonNode"/> 弱类型读取，
    /// 新配置用 <see cref="Device.Controls.Models"/> 的档案类型写回。这样即使日后旧 Device
    /// 引用被摘掉，迁移器仍然可用。
    /// </summary>
    public static class LegacyDeviceConfigImporter
    {
        private const string CameraLegacyFile = "CamParam.json";
        private const string CameraFile = "Cameras.json";
        private const string LightLegacyFile = "LightControlParam.json";
        private const string LightFile = "Lights.json";
        private const string RfidLegacyFile = "RFIDParam.json";
        private const string RfidFile = "Rfids.json";
        private const string ScannerLegacyFile = "ScanParam.json";
        private const string ScannerFile = "Scanners.json";
        private const string LabelPrintFile = "LabelPrinters.json";

        /// <summary>BarTender 标签打印引擎的稳定驱动标识（与 <c>LabelPrintEngineFactory</c> 的注册值一致）。</summary>
        private const string BarTenderDriverId = "label-print.bartender.sdk";

        /// <summary>旧品牌名 → 新稳定驱动标识。Xenics 与未知品牌不在表内 ⇒ 拦截，不猜。</summary>
        private static readonly Dictionary<string, string> CameraDriverByBrand =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Hik"] = "camera.hik.gige",
                ["DaHua"] = "camera.dahua.gige",
                ["Basler"] = "camera.basler.gige",
                ["I_Tek"] = "camera.itek.gige",
            };

        /// <summary>旧光源类型 → 新稳定驱动标识。旧枚举无 JsonStringEnumConverter，序列化后是 0/1。</summary>
        private static readonly Dictionary<string, string> LightDriverByType =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["CST"] = "light.cst.serial",
                ["0"] = "light.cst.serial",
                ["OPT"] = "light.opt.serial",
                ["1"] = "light.opt.serial",
            };

        /// <summary>旧 RFID 类型 → 新稳定驱动标识（枚举名以旧源码为准）。</summary>
        private static readonly Dictionary<string, string> RfidDriverByType =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["晨控S650"] = "rfid.ck.s650.modbus-rtu",
                ["椹诺XK15AR01"] = "rfid.xinuo.xk15a-r01.serial",
            };

        /// <summary>旧扫码器类型 → 新稳定驱动标识。旧枚举无 JsonStringEnumConverter，序列化后是 0。</summary>
        private static readonly Dictionary<string, string> ScannerDriverByType =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["新大陆"] = "scanner.newland.nvh220.serial",
                ["0"] = "scanner.newland.nvh220.serial",
            };

        /// <summary>默认配置目录，与 <c>DevicePageModelBase</c> 的约定一致（&lt;BaseDirectory&gt;/Config）。</summary>
        public static string DefaultConfigDirectory =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");

        /// <summary>在默认配置目录执行一次迁移。</summary>
        /// <returns>迁移结果；调用方应把 <see cref="LegacyDeviceConfigImportResult.Warnings"/> 记录到日志。</returns>
        public static LegacyDeviceConfigImportResult Import() => Import(DefaultConfigDirectory);

        /// <summary>在指定目录执行一次迁移（幂等；测试用这个重载）。</summary>
        /// <param name="configDirectory">配置目录；不存在时不会被创建，只读旧文件。</param>
        /// <returns>迁移结果。</returns>
        /// <exception cref="ArgumentException">目录为空或仅空白时抛出。</exception>
        public static LegacyDeviceConfigImportResult Import(string configDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(configDirectory);

            var result = new LegacyDeviceConfigImportResult();
            ImportCameras(configDirectory, result);
            ImportLights(configDirectory, result);
            ImportRfids(configDirectory, result);
            ImportScanners(configDirectory, result);
            EnsureLabelPrinterDefault(configDirectory, result);
            return result;
        }

        // ---------------------------------------------------------------- 各类别迁移

        private static void ImportCameras(string configDirectory, LegacyDeviceConfigImportResult result)
        {
            if (!CanWriteNewFile(configDirectory, CameraFile, result)) return;
            if (!TryLoadLegacyArray(configDirectory, CameraLegacyFile, result, out var array)) return;

            var profiles = new List<CameraProfile>();
            foreach (var item in array!)
            {
                if (item is not JsonObject obj)
                    continue;

                var name = ReadString(obj, "Name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    result.Warnings.Add($"{CameraLegacyFile}：存在缺少 Name 的条目，已跳过。");
                    continue;
                }

                // Xenics 与未知品牌在新栈没有对应驱动 —— 不猜一个近似值，直接拦截。
                var brand = ReadToken(obj, "CamBrand");
                if (brand is null || !CameraDriverByBrand.TryGetValue(brand, out var driverId))
                {
                    result.Warnings.Add(
                        $"相机「{name}」的品牌 {Describe(brand)} 在新 Device 栈没有对应驱动，已跳过，请人工确认。");
                    continue;
                }

                // 新栈只有 GigE 驱动；Camlink 采集卡不在本轮迁移范围。
                var camInterface = ReadToken(obj, "CamInterface");
                if (camInterface is not null &&
                    !string.Equals(camInterface, "Gige", StringComparison.OrdinalIgnoreCase))
                {
                    result.Warnings.Add(
                        $"相机「{name}」的接口 {camInterface} 不在新栈支持范围（仅 GigE），已跳过，请人工确认。");
                    continue;
                }

                var trigger = ReadToken(obj, "TriggerMode");
                var mapped = MapTrigger(trigger);
                if (mapped is null)
                {
                    // Free / Frame_Free / Frame_Encoder 的旧驱动行为尚未核对 ⇒ 不自动映射，
                    // 退到「触发关闭」这个语义安全值，并显式告警（不允许静默成功）。
                    result.Warnings.Add(
                        $"相机「{name}」的触发方式 {Describe(trigger)} 语义不明确，已按「触发关闭」迁移，请人工确认。");
                }

                profiles.Add(new CameraProfile
                {
                    Name = name,
                    DriverId = driverId,
                    IpAddress = ReadString(obj, "IP") ?? "127.0.0.1",
                    ExposureMilliseconds = ReadInt(obj, "Exposure", 20),
                    Gain = ReadDouble(obj, "Gain", 0),
                    TimeoutMilliseconds = ReadInt(obj, "TimeOut", 1_000),
                    FrameTriggerMode = mapped?.Mode ?? CameraTriggerMode.Off,
                    FrameTriggerSource = mapped?.Source ?? CameraTriggerSources.Software,
                });
            }

            if (profiles.Count == 0)
            {
                result.Warnings.Add($"{CameraLegacyFile} 中没有可迁移的相机条目，未生成 {CameraFile}。");
                return;
            }

            JsonEx.Save(profiles, Path.Combine(configDirectory, CameraFile));
            result.Imported.Add(CameraFile);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ImportLights。
        /// 正文中的关键协作调用包括 CanWriteNewFile、TryLoadLegacyArray、ReadString、IsNullOrWhiteSpace、Add；具体条件和先后次序见方法体。
        /// </summary>
        private static void ImportLights(string configDirectory, LegacyDeviceConfigImportResult result)
        {
            if (!CanWriteNewFile(configDirectory, LightFile, result)) return;
            if (!TryLoadLegacyArray(configDirectory, LightLegacyFile, result, out var array)) return;

            var profiles = new List<LightProfile>();
            foreach (var item in array!)
            {
                if (item is not JsonObject obj)
                    continue;

                var name = ReadString(obj, "Name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    result.Warnings.Add($"{LightLegacyFile}：存在缺少 Name 的条目，已跳过。");
                    continue;
                }

                var lightType = ReadToken(obj, "LightType");
                if (lightType is null || !LightDriverByType.TryGetValue(lightType, out var driverId))
                {
                    result.Warnings.Add(
                        $"光源「{name}」的类型 {Describe(lightType)} 在新 Device 栈没有对应驱动，已跳过，请人工确认。");
                    continue;
                }

                profiles.Add(new LightProfile
                {
                    Name = name,
                    DriverId = driverId,
                    PortName = ReadString(obj, "ComName") ?? "COM1",
                    // 旧配置没有通道数与最大亮度 ⇒ 用新栈的明确默认值，并记录迁移提示。
                    ChannelCount = 4,
                    MaximumIntensity = 255,
                });
            }

            if (profiles.Count == 0)
            {
                result.Warnings.Add($"{LightLegacyFile} 中没有可迁移的光源条目，未生成 {LightFile}。");
                return;
            }

            result.Warnings.Add(
                $"{LightFile}：通道数与最大亮度使用默认值（4 / 255）—— 旧配置没有这两项，请人工核对现场硬件。");
            JsonEx.Save(profiles, Path.Combine(configDirectory, LightFile));
            result.Imported.Add(LightFile);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ImportRfids。
        /// 正文中的关键协作调用包括 CanWriteNewFile、TryLoadLegacyArray、ReadString、IsNullOrWhiteSpace、Add；具体条件和先后次序见方法体。
        /// </summary>
        private static void ImportRfids(string configDirectory, LegacyDeviceConfigImportResult result)
        {
            if (!CanWriteNewFile(configDirectory, RfidFile, result)) return;
            if (!TryLoadLegacyArray(configDirectory, RfidLegacyFile, result, out var array)) return;

            var profiles = new List<RfidProfile>();
            foreach (var item in array!)
            {
                if (item is not JsonObject obj)
                    continue;

                var name = ReadString(obj, "Name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    result.Warnings.Add($"{RfidLegacyFile}：存在缺少 Name 的条目，已跳过。");
                    continue;
                }

                var rfidType = ReadToken(obj, "RFIDType");
                if (rfidType is null || !RfidDriverByType.TryGetValue(rfidType, out var driverId))
                {
                    result.Warnings.Add(
                        $"RFID「{name}」的类型 {Describe(rfidType)} 在新 Device 栈没有对应驱动，已跳过，请人工确认。");
                    continue;
                }

                profiles.Add(new RfidProfile
                {
                    Name = name,
                    DriverId = driverId,
                    PortName = ReadString(obj, "PortName") ?? "COM1",
                    BaudRate = ReadInt(obj, "BaudRate", 9_600),
                    DataBits = ReadInt(obj, "DataBits", 8),
                    StopBits = MapStopBits(ReadInt(obj, "StopBits", 1), $"RFID「{name}」", result),
                    Parity = MapParity(ReadToken(obj, "Parity")),
                    SlaveId = MapSlaveId(obj, name, result),
                });
            }

            if (profiles.Count == 0)
            {
                result.Warnings.Add($"{RfidLegacyFile} 中没有可迁移的 RFID 条目，未生成 {RfidFile}。");
                return;
            }

            JsonEx.Save(profiles, Path.Combine(configDirectory, RfidFile));
            result.Imported.Add(RfidFile);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ImportScanners。
        /// 正文中的关键协作调用包括 CanWriteNewFile、TryLoadLegacyArray、ReadString、IsNullOrWhiteSpace、Add；具体条件和先后次序见方法体。
        /// </summary>
        private static void ImportScanners(string configDirectory, LegacyDeviceConfigImportResult result)
        {
            if (!CanWriteNewFile(configDirectory, ScannerFile, result)) return;
            if (!TryLoadLegacyArray(configDirectory, ScannerLegacyFile, result, out var array)) return;

            var profiles = new List<ScannerProfile>();
            foreach (var item in array!)
            {
                if (item is not JsonObject obj)
                    continue;

                var name = ReadString(obj, "Name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    result.Warnings.Add($"{ScannerLegacyFile}：存在缺少 Name 的条目，已跳过。");
                    continue;
                }

                var scanType = ReadToken(obj, "ScanType");
                if (scanType is null || !ScannerDriverByType.TryGetValue(scanType, out var driverId))
                {
                    result.Warnings.Add(
                        $"扫码器「{name}」的类型 {Describe(scanType)} 在新 Device 栈没有对应驱动，已跳过，请人工确认。");
                    continue;
                }

                profiles.Add(new ScannerProfile
                {
                    Name = name,
                    DriverId = driverId,
                    PortName = ReadString(obj, "PortName") ?? "COM1",
                    BaudRate = ReadInt(obj, "BaudRate", 9_600),
                    DataBits = ReadInt(obj, "DataBits", 8),
                    StopBits = MapStopBits(ReadInt(obj, "StopBits", 1), $"扫码器「{name}」", result),
                    Parity = MapParity(ReadToken(obj, "Parity")),
                });
            }

            if (profiles.Count == 0)
            {
                result.Warnings.Add($"{ScannerLegacyFile} 中没有可迁移的扫码器条目，未生成 {ScannerFile}。");
                return;
            }

            JsonEx.Save(profiles, Path.Combine(configDirectory, ScannerFile));
            result.Imported.Add(ScannerFile);
        }

        /// <summary>
        /// 标签打印：旧 <c>PrintManagerModel</c> 的列表只存在于运行内存，从来没有落盘文件
        /// ⇒ 不伪造历史条目，只建立一个 BarTender 默认档案，模板与打印机由新页面选择。
        /// </summary>
        private static void EnsureLabelPrinterDefault(string configDirectory, LegacyDeviceConfigImportResult result)
        {
            if (!CanWriteNewFile(configDirectory, LabelPrintFile, result)) return;

            var profiles = new List<LabelPrintProfile>
            {
                new() { Name = "标签打印01", DriverId = BarTenderDriverId },
            };
            JsonEx.Save(profiles, Path.Combine(configDirectory, LabelPrintFile));
            result.Imported.Add(LabelPrintFile);
        }

        // ---------------------------------------------------------------- 值映射

        /// <summary>旧触发方式 → 新栈的（帧触发模式 + 触发源）；返回 null 表示语义不明确、需人工确认。</summary>
        private static (CameraTriggerMode Mode, string Source)? MapTrigger(string? legacyTrigger) =>
            legacyTrigger switch
            {
                "OFF" => (CameraTriggerMode.Off, CameraTriggerSources.Software),
                "Software" => (CameraTriggerMode.On, CameraTriggerSources.Software),
                "Frame_Line1" => (CameraTriggerMode.On, CameraTriggerSources.Line1),
                "Frame_Line2" => (CameraTriggerMode.On, CameraTriggerSources.Line2),
                _ => null,
            };

        /// <summary>串口校验位：旧枚举无 JsonStringEnumConverter，序列化后是 0..4。</summary>
        private static string MapParity(string? token) => token switch
        {
            "1" or "Odd" => "odd",
            "2" or "Even" => "even",
            "3" or "Mark" => "mark",
            "4" or "Space" => "space",
            _ => "none",
        };

        /// <summary>停止位：新栈只支持 1 或 2；None(0) / OnePointFive(3) 退到 1 并告警。</summary>
        private static int MapStopBits(int raw, string deviceLabel, LegacyDeviceConfigImportResult result)
        {
            if (raw is 1 or 2)
                return raw;

            result.Warnings.Add($"{deviceLabel} 的停止位 {raw} 不在新栈支持范围（1 或 2），已按 1 迁移，请人工确认。");
            return 1;
        }

        /// <summary>从站号：旧类同时有 byte 的 SlaveId 与 int 的 SlaveID（兼容属性），优先取显式的 SlaveID。</summary>
        private static byte MapSlaveId(JsonObject obj, string name, LegacyDeviceConfigImportResult result)
        {
            var raw = ReadInt(obj, "SlaveID", -1);
            if (raw < 0)
                raw = ReadInt(obj, "SlaveId", 1);

            if (raw is < 0 or > 255)
            {
                result.Warnings.Add($"RFID「{name}」的从站号 {raw} 超出 byte 范围，已按 1 迁移，请人工确认。");
                return 1;
            }

            return (byte)raw;
        }

        // ---------------------------------------------------------------- 读取辅助

        /// <summary>「新文件已存在」是幂等闸门：存在即跳过，绝不覆盖。</summary>
        private static bool CanWriteNewFile(
            string configDirectory,
            string newFile,
            LegacyDeviceConfigImportResult result)
        {
            if (File.Exists(Path.Combine(configDirectory, newFile)))
            {
                result.Skipped.Add(newFile);
                return false;
            }

            return true;
        }

        /// <summary>读取旧配置数组；文件不存在返回 false（不是错误）。</summary>
        private static bool TryLoadLegacyArray(
            string configDirectory,
            string legacyFile,
            LegacyDeviceConfigImportResult result,
            out JsonArray? array)
        {
            array = null;
            var path = Path.Combine(configDirectory, legacyFile);
            if (!File.Exists(path))
                return false;

            try
            {
                array = JsonNode.Parse(File.ReadAllText(path)) as JsonArray;
            }
            catch (Exception exception)
            {
                result.Warnings.Add($"{legacyFile} 解析失败，已跳过迁移：{exception.Message}");
                return false;
            }

            if (array is null)
            {
                result.Warnings.Add($"{legacyFile} 的根节点不是 JSON 数组，已跳过迁移。");
                return false;
            }

            return true;
        }

        /// <summary>读取字符串字段；数字等其它标量按其 JSON 文本返回。</summary>
        private static string? ReadString(JsonObject obj, string propertyName)
        {
            if (!obj.TryGetPropertyValue(propertyName, out var node) || node is null)
                return null;

            return node.GetValueKind() == JsonValueKind.String
                ? node.GetValue<string>()
                : node.ToJsonString();
        }

        /// <summary>读取枚举字段的原始记号（字符串枚举名或数字值）。</summary>
        private static string? ReadToken(JsonObject obj, string propertyName)
        {
            if (!obj.TryGetPropertyValue(propertyName, out var node) || node is null)
                return null;

            return node.GetValueKind() switch
            {
                JsonValueKind.String => node.GetValue<string>(),
                JsonValueKind.Number => node.GetValue<double>().ToString(CultureInfo.InvariantCulture),
                _ => null,
            };
        }

        /// <summary>
        /// 读取整数：旧文件由 <c>JsonEx</c> 落盘，其 <c>JsonNumberHandling.WriteAsString</c>
        /// 会把数字写成字符串 ⇒ 两种形态都要接受。
        /// </summary>
        private static int ReadInt(JsonObject obj, string propertyName, int fallback)
        {
            if (!obj.TryGetPropertyValue(propertyName, out var node) || node is null)
                return fallback;

            return node.GetValueKind() switch
            {
                JsonValueKind.Number => (int)node.GetValue<double>(),
                JsonValueKind.String when int.TryParse(
                    node.GetValue<string>(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                    => parsed,
                _ => fallback,
            };
        }

        /// <summary>读取浮点数，接受数字与字符串两种形态。</summary>
        private static double ReadDouble(JsonObject obj, string propertyName, double fallback)
        {
            if (!obj.TryGetPropertyValue(propertyName, out var node) || node is null)
                return fallback;

            return node.GetValueKind() switch
            {
                JsonValueKind.Number => node.GetValue<double>(),
                JsonValueKind.String when double.TryParse(
                    node.GetValue<string>(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                    => parsed,
                _ => fallback,
            };
        }

        /// <summary>把可能为空的记号渲染成告警文案里的可读形式。</summary>
        private static string Describe(string? token) =>
            string.IsNullOrWhiteSpace(token) ? "(空)" : token;
    }
}

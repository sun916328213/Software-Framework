using System;
using System.Collections.Generic;
using System.IO;
using Device.Controls.Models;
using Device.Core.Cameras;
using Extension.Core.Conversion;
using WPFControl.Integration.Device;
using Xunit;

namespace WPFControl.Tests
{
    /// <summary>
    /// 旧 Device 配置迁移守卫（P8 计划 P8-5 第 15 条）。
    ///
    /// 覆盖四条硬契约：
    /// <list type="number">
    /// <item><b>幂等</b> —— 重复执行结果一致（第二次不再写文件）；</item>
    /// <item><b>非破坏</b> —— 旧文件永远不被覆盖或删除；</item>
    /// <item><b>不覆盖</b> —— 新文件已存在即跳过；</item>
    /// <item><b>不猜</b> —— 未知品牌 / 非 GigE 接口 / 语义不明确的触发组合必须产生告警。</item>
    /// </list>
    ///
    /// 旧 JSON 的字面量刻意按**真实现场文件的形态**写：<c>JsonEx</c> 落盘时带
    /// <c>JsonNumberHandling.WriteAsString</c>，所以数字会写成字符串；而无
    /// <c>JsonStringEnumConverter</c> 的旧枚举（<c>ELightType</c> / <c>EScanType</c> /
    /// <c>StopBits</c> / <c>Parity</c>）落盘是**数字**。迁移器两种形态都要吃下。
    /// </summary>
    public sealed class LegacyDeviceConfigMigrationTests : IDisposable
    {
        private readonly string _directory;

        public LegacyDeviceConfigMigrationTests()
        {
            _directory = Path.Combine(
                Path.GetTempPath(),
                "LegacyDeviceConfigMigrationTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_directory, true);
            }
            catch
            {
                // 测试临时目录清理失败不应让用例失败。
            }
        }

        // ---------------------------------------------------------------- 相机

        [Fact]
        public void Import_MapsCameraFieldsAndStableDriverId()
        {
            WriteLegacy(
                "CamParam.json",
                """
                [
                  {
                    "Name": "相机01",
                    "IP": "192.168.1.10",
                    "Exposure": "15",
                    "Gain": "2.5",
                    "TriggerMode": "Software",
                    "TimeOut": "800",
                    "ImageType": "Gray8",
                    "CamBrand": "Hik",
                    "CamInterface": "Gige"
                  }
                ]
                """);

            var result = LegacyDeviceConfigImporter.Import(_directory);

            Assert.Contains("Cameras.json", result.Imported);
            var camera = Assert.Single(ReadNew<List<CameraProfile>>("Cameras.json")!);
            Assert.Equal("相机01", camera.Name);
            Assert.Equal("camera.hik.gige", camera.DriverId);
            Assert.Equal("192.168.1.10", camera.IpAddress);
            Assert.Equal(15, camera.ExposureMilliseconds);
            Assert.Equal(2.5, camera.Gain);
            Assert.Equal(800, camera.TimeoutMilliseconds);
            Assert.Equal(CameraTriggerMode.On, camera.FrameTriggerMode);
            Assert.Equal(CameraTriggerSources.Software, camera.FrameTriggerSource);
        }

        [Fact]
        public void Import_MapsDeterministicTriggerCombinations()
        {
            WriteLegacy(
                "CamParam.json",
                """
                [
                  { "Name": "关", "CamBrand": "Hik", "CamInterface": "Gige", "TriggerMode": "OFF" },
                  { "Name": "软", "CamBrand": "Basler", "CamInterface": "Gige", "TriggerMode": "Software" },
                  { "Name": "线1", "CamBrand": "DaHua", "CamInterface": "Gige", "TriggerMode": "Frame_Line1" },
                  { "Name": "线2", "CamBrand": "I_Tek", "CamInterface": "Gige", "TriggerMode": "Frame_Line2" }
                ]
                """);

            var result = LegacyDeviceConfigImporter.Import(_directory);

            Assert.False(result.HasWarnings);
            var cameras = ReadNew<List<CameraProfile>>("Cameras.json")!;
            Assert.Equal(4, cameras.Count);

            Assert.Equal(CameraTriggerMode.Off, cameras[0].FrameTriggerMode);
            Assert.Equal(CameraTriggerMode.On, cameras[1].FrameTriggerMode);
            Assert.Equal(CameraTriggerSources.Software, cameras[1].FrameTriggerSource);
            Assert.Equal(CameraTriggerSources.Line1, cameras[2].FrameTriggerSource);
            Assert.Equal(CameraTriggerSources.Line2, cameras[3].FrameTriggerSource);
            Assert.Equal("camera.basler.gige", cameras[1].DriverId);
            Assert.Equal("camera.dahua.gige", cameras[2].DriverId);
            Assert.Equal("camera.itek.gige", cameras[3].DriverId);
        }

        [Fact]
        public void Import_SkipsUnsupportedCameraBrandAndWarns()
        {
            WriteLegacy(
                "CamParam.json",
                """
                [
                  { "Name": "Xenics相机", "CamBrand": "Xenics", "CamInterface": "Gige", "TriggerMode": "OFF" },
                  { "Name": "正常相机", "CamBrand": "Hik", "CamInterface": "Gige", "TriggerMode": "OFF" }
                ]
                """);

            var result = LegacyDeviceConfigImporter.Import(_directory);

            // Xenics 在新栈没有驱动 ⇒ 拦截该条并告警，绝不猜一个近似驱动。
            Assert.Contains(result.Warnings, w => w.Contains("Xenics"));
            var camera = Assert.Single(ReadNew<List<CameraProfile>>("Cameras.json")!);
            Assert.Equal("正常相机", camera.Name);
        }

        [Fact]
        public void Import_SkipsNonGigeCameraInterfaceAndWarns()
        {
            WriteLegacy(
                "CamParam.json",
                """
                [
                  { "Name": "采集卡相机", "CamBrand": "Basler", "CamInterface": "Camlink", "TriggerMode": "OFF" }
                ]
                """);

            var result = LegacyDeviceConfigImporter.Import(_directory);

            Assert.Contains(result.Warnings, w => w.Contains("Camlink"));
            // 没有可迁移条目 ⇒ 不生成新文件（避免生成空配置把现场覆盖成"设备全没了"）。
            Assert.DoesNotContain("Cameras.json", result.Imported);
            Assert.False(File.Exists(Path.Combine(_directory, "Cameras.json")));
        }

        [Theory]
        [InlineData("Free")]
        [InlineData("Frame_Free")]
        [InlineData("Frame_Encoder")]
        public void Import_WarnsOnAmbiguousTriggerAndFallsBackToOff(string triggerMode)
        {
            WriteLegacy(
                "CamParam.json",
                $$"""
                [
                  { "Name": "相机01", "CamBrand": "Hik", "CamInterface": "Gige", "TriggerMode": "{{triggerMode}}" }
                ]
                """);

            var result = LegacyDeviceConfigImporter.Import(_directory);

            Assert.Contains(result.Warnings, w => w.Contains(triggerMode));
            var camera = Assert.Single(ReadNew<List<CameraProfile>>("Cameras.json")!);
            Assert.Equal(CameraTriggerMode.Off, camera.FrameTriggerMode);
        }

        // ---------------------------------------------------------------- 光源 / RFID / 扫码

        [Fact]
        public void Import_MapsLightWithNumericLegacyEnum()
        {
            WriteLegacy(
                "LightControlParam.json",
                """
                [
                  { "Name": "光源CST", "ComName": "COM3", "LightType": 0 },
                  { "Name": "光源OPT", "ComName": "COM4", "LightType": 1 }
                ]
                """);

            var result = LegacyDeviceConfigImporter.Import(_directory);

            Assert.Contains("Lights.json", result.Imported);
            var lights = ReadNew<List<LightProfile>>("Lights.json")!;
            Assert.Equal("light.cst.serial", lights[0].DriverId);
            Assert.Equal("COM3", lights[0].PortName);
            Assert.Equal("light.opt.serial", lights[1].DriverId);

            // 通道数与最大亮度旧配置没有 ⇒ 用明确默认值并留下迁移提示。
            Assert.Contains(result.Warnings, w => w.Contains("通道数"));
        }

        [Fact]
        public void Import_MapsRfidSerialAndModbusFields()
        {
            WriteLegacy(
                "RFIDParam.json",
                """
                [
                  {
                    "Name": "RFID01",
                    "RFIDType": "晨控S650",
                    "PortName": "COM5",
                    "BaudRate": 9600,
                    "DataBits": 8,
                    "StopBits": 1,
                    "Parity": 2,
                    "SlaveId": 7
                  }
                ]
                """);

            var result = LegacyDeviceConfigImporter.Import(_directory);

            Assert.Contains("Rfids.json", result.Imported);
            var rfid = Assert.Single(ReadNew<List<RfidProfile>>("Rfids.json")!);
            Assert.Equal("rfid.ck.s650.modbus-rtu", rfid.DriverId);
            Assert.Equal("COM5", rfid.PortName);
            Assert.Equal(9600, rfid.BaudRate);
            Assert.Equal(1, rfid.StopBits);
            Assert.Equal("even", rfid.Parity);
            Assert.Equal((byte)7, rfid.SlaveId);
        }

        [Fact]
        public void Import_MapsScannerSerialFields()
        {
            WriteLegacy(
                "ScanParam.json",
                """
                [
                  {
                    "Name": "扫码枪01",
                    "ScanType": 0,
                    "PortName": "COM7",
                    "BaudRate": 115200,
                    "DataBits": 8,
                    "StopBits": 1,
                    "Parity": 0,
                    "Delimiter": "\r\n"
                  }
                ]
                """);

            var result = LegacyDeviceConfigImporter.Import(_directory);

            Assert.Contains("Scanners.json", result.Imported);
            var scanner = Assert.Single(ReadNew<List<ScannerProfile>>("Scanners.json")!);
            Assert.Equal("scanner.newland.nvh220.serial", scanner.DriverId);
            Assert.Equal("COM7", scanner.PortName);
            Assert.Equal(115200, scanner.BaudRate);
            Assert.Equal("none", scanner.Parity);
        }

        [Fact]
        public void Import_WarnsOnUnsupportedSerialStopBits()
        {
            WriteLegacy(
                "ScanParam.json",
                """
                [ { "Name": "扫码枪01", "ScanType": 0, "StopBits": 3 } ]
                """);

            var result = LegacyDeviceConfigImporter.Import(_directory);

            Assert.Contains(result.Warnings, w => w.Contains("停止位"));
            Assert.Equal(1, Assert.Single(ReadNew<List<ScannerProfile>>("Scanners.json")!).StopBits);
        }

        // ---------------------------------------------------------------- 标签打印

        [Fact]
        public void Import_CreatesBarTenderDefaultWhenNoLegacyFilesExist()
        {
            var result = LegacyDeviceConfigImporter.Import(_directory);

            // 旧 PrintManager 列表只在内存里，从不落盘 ⇒ 不伪造历史条目，只建默认档案。
            Assert.Contains("LabelPrinters.json", result.Imported);
            var label = Assert.Single(ReadNew<List<LabelPrintProfile>>("LabelPrinters.json")!);
            Assert.Equal("label-print.bartender.sdk", label.DriverId);
        }

        // ---------------------------------------------------------------- 幂等 / 非破坏 / 不覆盖

        [Fact]
        public void Import_IsIdempotent()
        {
            WriteLegacy(
                "CamParam.json",
                """
                [ { "Name": "相机01", "CamBrand": "Hik", "CamInterface": "Gige", "TriggerMode": "Software" } ]
                """);

            LegacyDeviceConfigImporter.Import(_directory);
            var first = File.ReadAllText(Path.Combine(_directory, "Cameras.json"));

            var second = LegacyDeviceConfigImporter.Import(_directory);

            Assert.Equal(first, File.ReadAllText(Path.Combine(_directory, "Cameras.json")));
            // 第二次所有目标文件都已存在 ⇒ 全部走 Skipped 分支。
            Assert.Contains("Cameras.json", second.Skipped);
            Assert.Contains("LabelPrinters.json", second.Skipped);
            Assert.Empty(second.Imported);
        }

        [Fact]
        public void Import_DoesNotOverwriteExistingNewFile()
        {
            WriteLegacy(
                "CamParam.json",
                """
                [ { "Name": "旧相机", "CamBrand": "Hik", "CamInterface": "Gige", "TriggerMode": "OFF" } ]
                """);
            WriteLegacy(
                "Cameras.json",
                """
                [ { "Name": "现场已改好的相机", "DriverId": "camera.basler.gige" } ]
                """);

            var result = LegacyDeviceConfigImporter.Import(_directory);

            Assert.Contains("Cameras.json", result.Skipped);
            var camera = Assert.Single(ReadNew<List<CameraProfile>>("Cameras.json")!);
            Assert.Equal("现场已改好的相机", camera.Name);
        }

        [Fact]
        public void Import_NeverTouchesLegacyFiles()
        {
            const string legacyJson =
                """
                [ { "Name": "相机01", "CamBrand": "Hik", "CamInterface": "Gige", "TriggerMode": "Software" } ]
                """;
            WriteLegacy("CamParam.json", legacyJson);
            var legacyPath = Path.Combine(_directory, "CamParam.json");
            var before = File.GetLastWriteTimeUtc(legacyPath);

            LegacyDeviceConfigImporter.Import(_directory);

            // 旧文件必须原样保留：旧 Device 栈（Packing Machine）仍在读它。
            Assert.True(File.Exists(legacyPath));
            Assert.Equal(legacyJson, File.ReadAllText(legacyPath));
            Assert.Equal(before, File.GetLastWriteTimeUtc(legacyPath));
        }

        [Fact]
        public void Import_ReportsWarningInsteadOfThrowingOnMalformedLegacyFile()
        {
            WriteLegacy("CamParam.json", "{ 这不是合法的 JSON 数组 ");

            var result = LegacyDeviceConfigImporter.Import(_directory);

            Assert.Contains(result.Warnings, w => w.Contains("CamParam.json"));
            Assert.DoesNotContain("Cameras.json", result.Imported);
            // 其它类别不受影响：坏文件只影响它自己那一类。
            Assert.Contains("LabelPrinters.json", result.Imported);
        }

        // ---------------------------------------------------------------- 辅助

        private void WriteLegacy(string fileName, string json) =>
            File.WriteAllText(Path.Combine(_directory, fileName), json);

        /// <summary>按新页面的读取方式读回新配置文件 —— 证明生成的文件确实是新栈能消费的格式。</summary>
        private T? ReadNew<T>(string fileName) =>
            JsonEx.TryRead<T>(Path.Combine(_directory, fileName));
    }
}

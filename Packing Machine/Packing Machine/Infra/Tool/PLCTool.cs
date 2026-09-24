using System.Globalization;
using Extension.UI.Controls;
using DataManager.Log.Definition;
using Packing_Machine.Business.Order;
using Packing_Machine.Business.Station;
using Packing_Machine.Data;
using Packing_Machine.Infra.Plc;
using Prism.Ioc;
using Secs;
using WPFControl.CustomControl.Models;
using WPFControl.ProgramControl.Services;

namespace Packing_Machine.Infra.Tool
{
    // P5：本门面保持同步签名（8 个 Station 调用方式不变），内部以 sync-over-async 调用 McClientDevice。
    // 全链路 await 均 ConfigureAwait(false)，不回 UI 同步上下文；VSTHRD002 在此为已知可接受模式。
#pragma warning disable VSTHRD002
    public static class PLCAddressOffest
    {
        public static int Trigger = 0;
        public static int Reply = 1;
        public static int Result = 2;
        public static int Function = 3;
        public static int Count = 4;
        public static int CountReply = 5;

        public static int ID = 10;
    }

    /// <summary>
    /// PLC 业务入口（门面，签名保持，8 个 Station 与 AutoProcess 调用方式不变）。
    /// <para>
    /// P5：底层已由 <see cref="PlcService"/>（McClientDevice 新通信栈）实现；
    /// 不恢复已删除的 CommunicationManager / MCHelper。
    /// 所有同步方法内部以 sync-over-async 调用设备（全链路 ConfigureAwait(false)，
    /// 不回 UI 线程）；<see cref="TriggerAsync"/> 本身即异步。
    /// </para>
    /// </summary>
    public class PLCTool
    {
        readonly IContainerProvider _container;
        readonly PlcService _plc;
        readonly PlcTagCatalog _catalog;
        LogInfoService _LogInfoService => _container.Resolve<LogInfoService>();
        readonly SecsControl _SecsControl;

        public PLCTool(IContainerProvider container, PlcService plc, PlcTagCatalog catalog)
        {
            _container = container;
            _plc = plc;
            _catalog = catalog;
            _SecsControl = container.Resolve<SecsControl>();
            _plc.ConnectionChanged += online =>
            {
                try
                {
                    AddLog("PLC", online ? "Online" : "Offline", string.Empty, 0, string.Empty);
                }
                catch
                {
                    // 日志通道异常不能破坏 PLC 调用。
                }
            };
        }

        /// <summary>当前是否在线（PlcService 最后已知状态）。</summary>
        public bool IsOnline => _plc.IsConnected;

        /// <summary>
        /// 刷新任务：幂等连接 + 启动 Online 心跳；不恢复旧 1000-word 缓存。
        /// 每秒由 AutoProcess 调用，必须非阻塞、不抛错。
        /// </summary>
        public void Refresh()
        {
            try
            {
                _plc.StartHeartbeat();
                _ = _plc.ConnectAsync().ContinueWith(
                    t =>
                    {
                        if (t.IsFaulted)
                        {
                            try
                            {
                                _LogInfoService.AddRunInfo("PLC Refresh 连接失败：" + t.Exception?.GetBaseException().Message);
                            }
                            catch
                            {
                            }
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                try
                {
                    _LogInfoService.AddRunInfo("PLC Refresh 启动失败：" + ex.Message);
                }
                catch
                {
                }
            }
        }

        public void StopRefresh()
        {
            try
            {
                _plc.StopHeartbeat();
                _plc.DisconnectAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                try
                {
                    _LogInfoService.AddRunInfo("PLC StopRefresh 失败：" + ex.Message);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// 触发回复：读取 Trigger；清 Result；Reply=1；读取 Count 并镜像到 CountReply。
        /// </summary>
        /// <param name="stationName"></param>
        /// <param name="pPLCOrder"></param>
        public void TriggerReply(string stationName, string pPLCOrder)
        {
            _catalog.Resolve(pPLCOrder);
            ReadInt16(pPLCOrder, PLCAddressOffest.Trigger);
            WriteInt16(pPLCOrder, PLCAddressOffest.Result, 0);
            WriteInt16(pPLCOrder, PLCAddressOffest.Reply, 1);
            var count = ReadInt16(pPLCOrder, PLCAddressOffest.Count);
            WriteInt16(pPLCOrder, PLCAddressOffest.CountReply, count);
            AddLog(stationName, "TriggerReply", pPLCOrder, 0, string.Empty);
        }

        /// <summary>
        /// 发送数据：按基点位类型编码并直接写入 base+offset。
        /// 字符串值写入数值点位时先按目标类型解析，解析失败则按 ASCII 动态写入（兼容 RFID 等旧行为）。
        /// </summary>
        /// <param name="sationName"></param>
        /// <param name="pPLCOrder"></param>
        /// <param name="OffestAdd"></param>
        /// <param name="info"></param>
        public void SendData(string sationName, string pPLCOrder, int OffestAdd, object info)
        {
            if (info is null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            var tag = _catalog.Resolve(pPLCOrder);
            var address = _catalog.ResolveAddress(pPLCOrder, OffestAdd);
            var text = Convert.ToString(info, CultureInfo.InvariantCulture) ?? string.Empty;
            if (tag.Type == PlcTagType.String)
            {
                var length = PlcTagCatalog.GetRegisterCount(tag);
                if (length.HasValue)
                {
                    _plc.WriteStringAsync(address, text, length.Value).GetAwaiter().GetResult();
                }
                else
                {
                    _plc.WriteDynamicStringAsync(address, text).GetAwaiter().GetResult();
                }
            }
            else if (info is string textValue)
            {
                WriteNumericOrAscii(address, tag.Type, textValue);
            }
            else
            {
                WriteNumeric(address, tag.Type, info);
            }

            AddLog(sationName, "SendData", pPLCOrder, OffestAdd, text);
        }

        /// <summary>
        /// 发送结果：把 EPLCResult 的数值写到 Result 偏移。
        /// </summary>
        /// <param name="pPLCOrder"></param>
        /// <param name="info"></param>
        public void SendResult(string sationName, string pPLCOrder, EPLCResult info)
        {
            WriteInt16(pPLCOrder, PLCAddressOffest.Result, checked((short)(int)info));
            AddLog(sationName, "SendResult", pPLCOrder, PLCAddressOffest.Result, info.ToString());
        }

        /// <summary>
        /// 流程结束：把 Reply 偏移清 0。
        /// </summary>
        /// <param name="sationName"></param>
        /// <param name="pPLCOrder"></param>
        /// <param name="info"></param>
        public void Complete(string sationName, string pPLCOrder)
        {
            WriteInt16(pPLCOrder, PLCAddressOffest.Reply, 0);
            AddLog(sationName, "Complete", pPLCOrder, PLCAddressOffest.Reply, "0");
        }

        /// <summary>
        /// 所有流程结束：从基础地址开始连续清零 length 个寄存器。
        /// </summary>
        /// <param name="sationName"></param>
        /// <param name="pPLCOrder"></param>
        /// <param name="length"></param>
        public void CompleteAll(string sationName, string pPLCOrder, int length)
        {
            var address = _catalog.ResolveAddress(pPLCOrder, 0);
            _plc.ResetAsync(address, length).GetAwaiter().GetResult();
            AddLog(sationName, "CompleteAll", pPLCOrder, 0, length.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// 读取数据：目录解析 tag+寄存器偏移 → PlcService 直接类型化读取 → ToString 返回。
        /// </summary>
        /// <param name="sationName"></param>
        /// <param name="pPLCOrder"></param>
        /// <param name="OffestAdd"></param>
        /// <returns></returns>
        public string ReadData(string sationName, string pPLCOrder, int OffestAdd = 0, bool islog = true)
        {
            var tag = _catalog.Resolve(pPLCOrder);
            var address = _catalog.ResolveAddress(pPLCOrder, OffestAdd);
            string text = tag.Type switch
            {
                PlcTagType.Short => _plc.ReadInt16Async(address).GetAwaiter().GetResult()
                    .ToString(CultureInfo.InvariantCulture),
                PlcTagType.Int => _plc.ReadInt32Async(address).GetAwaiter().GetResult()
                    .ToString(CultureInfo.InvariantCulture),
                PlcTagType.Float => _plc.ReadSingleAsync(address).GetAwaiter().GetResult()
                    .ToString(CultureInfo.InvariantCulture),
                PlcTagType.Double => _plc.ReadDoubleAsync(address).GetAwaiter().GetResult()
                    .ToString(CultureInfo.InvariantCulture),
                PlcTagType.String => ReadTagString(tag, address),
                _ => throw new ArgumentOutOfRangeException(nameof(pPLCOrder)),
            };

            AddLog(sationName, "ReadData", pPLCOrder, OffestAdd, text, islog);
            return text;
        }

        /// <summary>
        /// 写入用户信息：先清 PC_UserName 对应 10 个寄存器（保持旧行为），再写用户名和 PC_Level。
        /// </summary>
        /// <param name="pPLCOrder"></param>
        /// <param name="value"></param>
        public void TriggerUserInfo(UserInfo value)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var userNameTag = _catalog.Resolve(PLCSystem.PC_UserName);
            var userNameLength = PlcTagCatalog.GetRegisterCount(userNameTag)
                ?? throw new InvalidOperationException("PC_UserName 为动态长度，无法写入固定用户名。");
            _plc.ResetAsync(userNameTag.Address, 10).GetAwaiter().GetResult();
            _plc.WriteStringAsync(userNameTag.Address, value.Name ?? string.Empty, userNameLength).GetAwaiter().GetResult();
            var levelTag = _catalog.Resolve(PLCSystem.PC_Level);
            _plc.WriteInt16Async(levelTag.Address, checked((short)(int)value.Level)).GetAwaiter().GetResult();
            AddLog("System", "TriggerUserInfo", PLCSystem.PC_UserName, 0, value.Name ?? string.Empty);
        }

        /// <summary>
        /// 写入报警信息：直接写对应 Short 点。
        /// </summary>
        /// <param name="pPLCOrder"></param>
        /// <param name="value"></param>
        public void TriggerAlarm(string pPLCOrder, short value)
        {
            var tag = _catalog.Resolve(pPLCOrder);
            if (tag.Type != PlcTagType.Short)
            {
                throw new InvalidOperationException($"TriggerAlarm 只支持 Short 点位，'{pPLCOrder}' 为 {tag.Type}。");
            }

            _plc.WriteInt16Async(tag.Address, value).GetAwaiter().GetResult();
            AddLog("System", "TriggerAlarm", pPLCOrder, 0, value.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// 触发PLC：写 Count / Data / Trigger；等待 Reply=1，再等待 Result 非 0 并映射；
        /// 超时可配置（PLC.json HandshakeTimeoutMs）；finally 始终清 Trigger。
        /// </summary>
        /// <param name="stationName"></param>
        /// <param name="pPLCOrder"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task<EPLCResult> TriggerAsync(string stationName, string pPLCOrder, string data)
        {
            _catalog.Resolve(pPLCOrder);
            var timeoutMs = Math.Max(50, _plc.HandshakeTimeoutMs);
            using var cts = new CancellationTokenSource(timeoutMs);
            var ct = cts.Token;

            // P5 假设（待实机确认）：Count 写 1 表示单次事务；data 写 ID 偏移（短整可解析则写数值，否则 ASCII 动态写）。
            WriteInt16(pPLCOrder, PLCAddressOffest.Count, 1);
            WriteAsyncData(pPLCOrder, data);
            WriteInt16(pPLCOrder, PLCAddressOffest.Trigger, 1);
            AddLog(stationName, "TriggerAsync", pPLCOrder, 0, data ?? string.Empty);

            try
            {
                if (!await WaitUntilAsync(() => ReadInt16Raw(pPLCOrder, PLCAddressOffest.Reply), 1, ct).ConfigureAwait(false))
                {
                    return EPLCResult.ERROR;
                }

                var result = 0;
                var got = await WaitUntilAsync(async () =>
                {
                    result = await ReadInt16Raw(pPLCOrder, PLCAddressOffest.Result).ConfigureAwait(false);
                    return result != 0;
                }, ct).ConfigureAwait(false);

                if (!got)
                {
                    return EPLCResult.ERROR;
                }

                return result switch
                {
                    1 => EPLCResult.OK,
                    2 => EPLCResult.NG,
                    3 => EPLCResult.ERROR,
                    _ => EPLCResult.ERROR,
                };
            }
            finally
            {
                try
                {
                    WriteInt16(pPLCOrder, PLCAddressOffest.Trigger, 0);
                }
                catch
                {
                    // finally 清 Trigger 失败不掩盖主结果，由心跳/下次握手恢复。
                }
            }
        }

        private static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (await condition().ConfigureAwait(false))
                {
                    return true;
                }

                await Task.Delay(20, CancellationToken.None).ConfigureAwait(false);
            }

            return false;
        }

        private static async Task<bool> WaitUntilAsync(Func<Task<short>> read, short expected, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (await read().ConfigureAwait(false) == expected)
                {
                    return true;
                }

                await Task.Delay(20, CancellationToken.None).ConfigureAwait(false);
            }

            return false;
        }

        private short ReadInt16(string baseName, int offset)
        {
            var address = _catalog.ResolveAddress(baseName, offset);
            return _plc.ReadInt16Async(address).GetAwaiter().GetResult();
        }

        private Task<short> ReadInt16Raw(string baseName, int offset)
        {
            var address = _catalog.ResolveAddress(baseName, offset);
            return _plc.ReadInt16Async(address);
        }

        private void WriteInt16(string baseName, int offset, short value)
        {
            var address = _catalog.ResolveAddress(baseName, offset);
            _plc.WriteInt16Async(address, value).GetAwaiter().GetResult();
        }

        private string ReadTagString(PlcTagDefinition tag, string address)
        {
            var length = PlcTagCatalog.GetRegisterCount(tag);
            if (!length.HasValue)
            {
                throw new InvalidOperationException(
                    $"点位 '{tag.Name}' 为动态长度 String，读取需要显式长度（当前 ReadData 调用未提供）。");
            }

            return _plc.ReadStringAsync(address, length.Value).GetAwaiter().GetResult();
        }

        private void WriteNumericOrAscii(string address, PlcTagType type, string text)
        {
            switch (type)
            {
                case PlcTagType.Short:
                    if (short.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var s))
                    {
                        _plc.WriteInt16Async(address, s).GetAwaiter().GetResult();
                    }
                    else
                    {
                        _plc.WriteDynamicStringAsync(address, text).GetAwaiter().GetResult();
                    }

                    break;
                case PlcTagType.Int:
                    if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                    {
                        _plc.WriteInt32Async(address, i).GetAwaiter().GetResult();
                    }
                    else
                    {
                        _plc.WriteDynamicStringAsync(address, text).GetAwaiter().GetResult();
                    }

                    break;
                case PlcTagType.Float:
                    if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                    {
                        _plc.WriteSingleAsync(address, f).GetAwaiter().GetResult();
                    }
                    else
                    {
                        _plc.WriteDynamicStringAsync(address, text).GetAwaiter().GetResult();
                    }

                    break;
                case PlcTagType.Double:
                    if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                    {
                        _plc.WriteDoubleAsync(address, d).GetAwaiter().GetResult();
                    }
                    else
                    {
                        _plc.WriteDynamicStringAsync(address, text).GetAwaiter().GetResult();
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        private void WriteNumeric(string address, PlcTagType type, object value)
        {
            switch (type)
            {
                case PlcTagType.Short:
                    _plc.WriteInt16Async(address, Convert.ToInt16(value, CultureInfo.InvariantCulture)).GetAwaiter().GetResult();
                    break;
                case PlcTagType.Int:
                    _plc.WriteInt32Async(address, Convert.ToInt32(value, CultureInfo.InvariantCulture)).GetAwaiter().GetResult();
                    break;
                case PlcTagType.Float:
                    _plc.WriteSingleAsync(address, Convert.ToSingle(value, CultureInfo.InvariantCulture)).GetAwaiter().GetResult();
                    break;
                case PlcTagType.Double:
                    _plc.WriteDoubleAsync(address, Convert.ToDouble(value, CultureInfo.InvariantCulture)).GetAwaiter().GetResult();
                    break;
                default:
                    // String 点位由调用方统一走文本路径，此处仅防御未知枚举。
                    throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        private void WriteAsyncData(string baseName, string? data)
        {
            var text = data ?? string.Empty;
            var address = _catalog.ResolveAddress(baseName, PLCAddressOffest.ID);
            if (short.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
            {
                _plc.WriteInt16Async(address, numeric).GetAwaiter().GetResult();
            }
            else
            {
                _plc.WriteDynamicStringAsync(address, text).GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// 添加日志。保留原格式：PLC 读写日志通道与旧通信栈无关，
        /// 迁移后的 PlcService 复用同一格式。
        /// </summary>
        /// <param name="stationName"></param>
        /// <param name="fun"></param>
        /// <param name="pPLCOrder"></param>
        /// <param name="offestAdd"></param>
        /// <param name="value"></param>
        private void AddLog(string stationName, string fun, string pPLCOrder, int offestAdd, string value, bool islog = true)
        {
            if (!islog)
            {
                return;
            }

            stationName = stationName.Replace(":", "").Trim() + ": ";
            _LogInfoService.AddRunInfo(stationName + fun + " " + "地址:" + pPLCOrder + " " + "偏移:" + offestAdd + " " + "数据:" + value);
        }
    }
}

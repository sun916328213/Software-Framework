// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：TraceManager。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Runtime、SecsGem.Runtime.Order.S2、SecsGem.Runtime.Order.S6、SecsGem.Runtime.Protocol.Building、Prism.Ioc、Secs4Net、System、System.Collections.Concurrent、System.Collections.Generic、System.Globalization、System.Threading、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using SecsGem.Runtime;
using SecsGem.Runtime.Order.S2;
using SecsGem.Runtime.Order.S6;
using SecsGem.Runtime.Protocol.Building;
using Prism.Ioc;
using Secs4Net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace SecsGem.Runtime.Manager
{
    /// <summary>
    /// Trace 的管理类（S2F23 定义 + S6F1 周期上报）。
    /// <para>
    /// 原实现是有名无实的：<c>Send</c> 用 <c>Convert.ToInt32(DSPER)</c> 把 DSPER 当毫秒，
    /// 真正发 S6F1 的那行（<c>GetData</c> 的方法体）被注释掉，<c>Register</c> 也没人调用——
    /// 结果 Host 发 S2F23 只会拿到一个空应答，一条 S6F1 都收不到。
    /// 现在 S2F23 处理器直接调 <see cref="Start"/>：校验请求 → 起后台采样循环 → 按 REPGSZ 分组发 S6F1。
    /// </para>
    /// </summary>
    public class TraceManager : ManagerBase
    {
        /// <summary>TRID → 该条 Trace 的取消源。同 TRID 重复下发视为按新参数重新定义（标准语义）。</summary>
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _traces
            = new ConcurrentDictionary<string, CancellationTokenSource>(StringComparer.Ordinal);

        /// <summary>
        /// 初始化 TraceManager 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public TraceManager(IContainerProvider container) : base(container)
        {
        }

        /// <summary>
        /// 处理一条 S2F23（TIS）：校验并启动 / 终止 Trace，返回 S2F24 的 TIAACK。
        /// 取值表：0=接受、1=SVID 数量超额、2=不允许更多 Trace、3=周期非法、4=SVID 未知、5=REPGSZ 非法。
        /// 本实现不做 Trace 数量上限，所以 1/2 不会出现。
        /// </summary>
        public byte Start(S2F23Info pRequest)
        {
            if (!int.TryParse(pRequest.TOTSMP, out int total) || total < 0)
                return 3;   // TOTSMP 非法按周期非法处理（TIAACK 没有单独给它的码）

            var trid = pRequest.TRID?.Trim();

            // 没有 TRID 挂不上定义；只有 TOTSMP=0 的"终止全部"在这种形状下才有意义。
            if (string.IsNullOrEmpty(trid))
            {
                if (total == 0) { CancelAll(); return 0; }
                return 3;
            }

            // TOTSMP=0：终止该 TRID 的 Trace，不产生 S6F1（E5 的终止方式）。
            if (total == 0) { CancelTrace(trid); return 0; }

            if (!TryParseDsper(pRequest.DSPER, out int intervalMs) || intervalMs <= 0)
                return 3;

            if (!int.TryParse(pRequest.REPGSZ, out int groupSize) || groupSize < 0)
                return 5;   // REPGSZ 非法

            var svids = pRequest.VID ?? new List<string>();
            if (svids.Count == 0) return 4;

            foreach (var id in svids)
            {
                if (VIDManager?.GetVIDByID(id) == null)
                    return 4;   // SVID 未知
            }

            // 同 TRID 重复下发：先停掉旧定义，再按新参数起（覆盖语义）。
            CancelTrace(trid);

            var cts = new CancellationTokenSource();
            _traces[trid] = cts;

            // 0 = 每个采样各发一条 S6F1。
            int reportGroupSize = groupSize == 0 ? 1 : groupSize;
            _ = RunTraceAsync(trid, svids, intervalMs, total, reportGroupSize, pRequest.TRIDType, cts);
            return 0;
        }

        /// <summary>
        /// 解析 S2F23 的 DSPER。标准是 <c>A[6] "hhmmss"</c>（可选 <c>A[8] "hhmmsscc"</c>），
        /// 同时兼容历史上按毫秒下发的纯数字写法（旧实现就是把 DSPER 当毫秒用的）。
        /// </summary>
        /// <param name="pDsper">DSPER 原文</param>
        /// <param name="pMilliseconds">解析出的采样周期（毫秒）</param>
        public static bool TryParseDsper(string pDsper, out int pMilliseconds)
        {
            pMilliseconds = 0;
            var text = pDsper?.Trim();
            if (string.IsNullOrEmpty(text)) return false;

            if (text.Length is 6 or 8 && IsAllDigits(text))
            {
                if (!int.TryParse(text.Substring(0, 2), out int hour) || hour > 23) return false;
                if (!int.TryParse(text.Substring(2, 2), out int minute) || minute > 59) return false;
                if (!int.TryParse(text.Substring(4, 2), out int second) || second > 59) return false;

                int ms = ((hour * 60 + minute) * 60 + second) * 1000;
                if (text.Length == 8)
                {
                    if (!int.TryParse(text.Substring(6, 2), out int centisecond) || centisecond > 99) return false;
                    ms += centisecond * 10;
                }

                pMilliseconds = ms;
                return true;
            }

            // 不是 hhmmss 形状：按毫秒数值解析（兼容旧写法）。
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out pMilliseconds)
                && pMilliseconds > 0;
        }

        /// <summary>
        /// 采样循环：每 intervalMs 采一次，攒够 reportGroupSize 个采样发一条 S6F1
        /// （最后一个采样不满一组也要发，否则末尾数据永远留在缓冲区里）。
        /// </summary>
        private async Task RunTraceAsync(
            string pTrid,
            List<string> pSvids,
            int pIntervalMs,
            int pTotalSamples,
            int pReportGroupSize,
            SecsFormat pTridType,
            CancellationTokenSource pCts)
        {
            var token = pCts.Token;
            var buffer = new List<Item>(pReportGroupSize * pSvids.Count);

            try
            {
                for (int sample = 1; sample <= pTotalSamples; sample++)
                {
                    await Task.Delay(pIntervalMs, token).ConfigureAwait(false);

                    buffer.AddRange(Snapshot(pSvids));

                    if (sample % pReportGroupSize == 0 || sample == pTotalSamples)
                    {
                        await SendTraceDataAsync(pTrid, sample, buffer, pTridType, token).ConfigureAwait(false);
                        buffer.Clear();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 被新的定义 / TOTSMP=0 / Dispose 取消：正常收尾，不报错。
            }
            catch (Exception ex)
            {
                SecsGemRuntime?.RaiseErrorPublic($"Trid {pTrid} 上报 S6F1 失败: {ex.Message}");
            }
            finally
            {
                // 只有自己仍是当前定义时才摘掉登记；同 TRID 的新定义不能被旧的清掉。
                if (_traces.TryGetValue(pTrid, out var current) && ReferenceEquals(current, pCts))
                    _traces.TryRemove(pTrid, out _);

                pCts.Dispose();
            }
        }

        /// <summary>取一组 SVID 的当前值快照（在采样那一刻取，避免组包时值已被改写）。</summary>
        private List<Item> Snapshot(List<string> pSvids)
        {
            var values = new List<Item>(pSvids.Count);
            foreach (var id in pSvids)
            {
                var vid = VIDManager?.GetVIDByID(id);
                values.Add(vid == null
                    ? Item.A(string.Empty)
                    : SecsItemBuilder.Build(vid.Value, vid.DataType) ?? Item.A(string.Empty));
            }

            return values;
        }

        /// <summary>
        /// 发一条 S6F1。Trace 是周期推送，不能等 Host 的 S6F2（wait=false）——
        /// Host 不回就把整条 Trace 拖停，比丢一条 S6F2 严重得多。
        /// </summary>
        private async Task SendTraceDataAsync(
            string pTrid,
            int pSample,
            List<Item> pValues,
            SecsFormat pTridType,
            CancellationToken pToken)
        {
            pToken.ThrowIfCancellationRequested();

            var runtime = SecsGemRuntime;
            if (runtime == null) return;

            var data = new SS6F1
            {
                TRID = pTrid,
                SMPLN = pSample.ToString(CultureInfo.InvariantCulture),
                STIME = DateTime.Now.ToString("yyyyMMddHHmmssff"),
                SV = new List<Item>(pValues),
                TRIDType = pTridType,
            };

            await runtime.SendItem("S6F1", false, data, VIDManager).ConfigureAwait(false);
        }

        /// <summary>停掉某个 TRID 的 Trace。取消源由采样循环的 finally 释放，这里只 Cancel。</summary>
        private void CancelTrace(string pTrid)
        {
            if (_traces.TryRemove(pTrid, out var cts))
                cts.Cancel();
        }

        /// <summary>停掉所有 Trace（TRID 为空且 TOTSMP=0 时）。</summary>
        private void CancelAll()
        {
            foreach (var trid in _traces.Keys)
                CancelTrace(trid);
        }

        private static bool IsAllDigits(string pText)
        {
            foreach (var c in pText)
            {
                if (c < '0' || c > '9') return false;
            }

            return true;
        }

        /// <summary>
        /// 释放时停掉所有 Trace：否则运行时都 Dispose 了，后台循环还在按周期往一个已关闭的连接发 S6F1。
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing) CancelAll();
            base.Dispose(disposing);
        }
    }
}

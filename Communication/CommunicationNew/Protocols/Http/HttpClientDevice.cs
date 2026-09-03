﻿﻿﻿using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using CommunicationNew.Core;

namespace CommunicationNew.Protocols.Http
{
    /// <summary>
    /// HTTP 客户端设备
    /// 地址格式：API路径 (如 "/api/sensor/read")
    /// ReadAsync → GET 请求，返回响应体字节数组
    /// WriteAsync → POST 请求，发送请求体
    /// </summary>
    public class HttpClientDevice : CommunicationDeviceBase
    {
        private new HttpParam Param => (HttpParam)base.Param;
        private HttpClient? _httpClient;

        public HttpClientDevice(HttpParam param) : base(param)
        {
            ProtocolType = Core.ProtocolType.Http;
        }
        public override DeviceRole Role => DeviceRole.Client;

        public override Task<bool> ConnectAsync(CancellationToken ct = default)
        {
            try
            {
                var handler = new HttpClientHandler();
                if (Param.IgnoreSslErrors)
                {
                    handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
                }

                _httpClient = new HttpClient(handler)
                {
                    BaseAddress = new Uri(Param.BaseUrl),
                    Timeout = TimeSpan.FromMilliseconds(TimeoutMs)
                };

                foreach (var header in Param.Headers)
                    _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);

                IsConnected = true;
                OnConnectionStateChanged(true, $"HTTP客户端就绪: {Param.BaseUrl}");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex);
                OnConnectionStateChanged(false, ex.Message);
                return Task.FromResult(false);
            }
        }

        public override Task DisconnectAsync(CancellationToken ct = default)
        {
            _httpClient?.Dispose();
            _httpClient = null;
            IsConnected = false;
            OnConnectionStateChanged(false, "HTTP客户端已关闭");
            return Task.CompletedTask;
        }

        /// <summary>
        /// GET 请求。address = API路径，length 被忽略
        /// </summary>
        public override async Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default)
        {
            if (_httpClient == null) throw new InvalidOperationException("设备未连接");

            return await ExecuteWithRetryAsync(async () =>
            {
                var response = await _httpClient.GetAsync(address, ct);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsByteArrayAsync(ct);
            }, ct);
        }

        /// <summary>
        /// POST 请求。address = API路径，data = 请求体
        /// </summary>
        public override async Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default)
        {
            if (_httpClient == null) throw new InvalidOperationException("设备未连接");

            return await ExecuteWithRetryAsync(async () =>
            {
                using var content = new ByteArrayContent(data);
                content.Headers.ContentType = new MediaTypeHeaderValue(Param.ContentType);

                var response = await _httpClient.PostAsync(address, content, ct);
                response.EnsureSuccessStatusCode();
                return true;
            }, ct);
        }

        /// <summary>
        /// 发送自定义HTTP请求并返回完整响应
        /// </summary>
        public override async Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default)
        {
            // data 为 "METHOD /path\r\n\r\nbody" 格式的原始请求
            // 或者直接视为 POST body
            if (_httpClient == null) throw new InvalidOperationException("设备未连接");

            using var content = new ByteArrayContent(data);
            content.Headers.ContentType = new MediaTypeHeaderValue(Param.ContentType);

            var response = await _httpClient.PostAsync("", content, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(ct);
        }

        public override Task<bool> SendAsync(byte[] data, CancellationToken ct = default)
        {
            return WriteAsync("", data, ct);
        }

        /// <summary>发送自定义请求</summary>
        public async Task<string> RequestAsync(string method, string path, string? body = null, CancellationToken ct = default)
        {
            if (_httpClient == null) throw new InvalidOperationException("设备未连接");

            var m = new HttpMethod(method.ToUpperInvariant());
            var request = new HttpRequestMessage(m, path);
            if (body != null)
            {
                request.Content = new StringContent(body, Encoding.UTF8, Param.ContentType);
            }

            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(ct);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _httpClient?.Dispose();
            base.Dispose(disposing);
        }
    }
}

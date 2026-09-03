﻿﻿﻿using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CommunicationNew.Core;

namespace CommunicationNew.Protocols.WebApi
{
    /// <summary>
    /// WebApi 客户端设备
    /// 面向RESTful API，自动JSON序列化/反序列化
    /// 地址格式：API路径 (如 "/users/123")
    /// ReadAsync → GET，返回JSON字节数组
    /// WriteAsync → POST/PUT，发送JSON字节数据
    /// </summary>
    public class WebApiClientDevice : CommunicationDeviceBase
    {
        private new WebApiParam Param => (WebApiParam)base.Param;
        private HttpClient? _httpClient;

        public WebApiClientDevice(WebApiParam param) : base(param)
        {
            ProtocolType = Core.ProtocolType.WebApi;
        }
        public override DeviceRole Role => DeviceRole.Client;

        public override Task<bool> ConnectAsync(CancellationToken ct = default)
        {
            try
            {
                var handler = new HttpClientHandler();
                if (Param.IgnoreSslErrors)
                    handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;

                _httpClient = new HttpClient(handler)
                {
                    BaseAddress = new Uri(Param.BaseUrl),
                    Timeout = TimeSpan.FromMilliseconds(Param.OperationTimeoutMs)
                };

                _httpClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                foreach (var header in Param.Headers)
                    _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);

                if (!string.IsNullOrEmpty(Param.BearerToken))
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", Param.BearerToken);

                if (!string.IsNullOrEmpty(Param.ApiKey))
                    _httpClient.DefaultRequestHeaders.Add(Param.ApiKeyHeaderName, Param.ApiKey);

                IsConnected = true;
                OnConnectionStateChanged(true, $"WebApi客户端就绪: {Param.BaseUrl}{Param.ApiPrefix}");
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
            OnConnectionStateChanged(false, "WebApi客户端已关闭");
            return Task.CompletedTask;
        }

        /// <summary>GET 请求，返回JSON响应体</summary>
        public override async Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default)
        {
            if (_httpClient == null) throw new InvalidOperationException("设备未连接");
            var path = BuildPath(address);

            return await ExecuteWithRetryAsync(async () =>
            {
                var response = await _httpClient.GetAsync(path, ct);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsByteArrayAsync(ct);
            }, ct);
        }

        /// <summary>POST/PUT 请求，发送JSON数据</summary>
        public override async Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default)
        {
            if (_httpClient == null) throw new InvalidOperationException("设备未连接");
            var path = BuildPath(address);

            return await ExecuteWithRetryAsync(async () =>
            {
                using var content = new ByteArrayContent(data);
                content.Headers.ContentType = new MediaTypeHeaderValue(Param.ContentType);
                var response = await _httpClient.PostAsync(path, content, ct);
                response.EnsureSuccessStatusCode();
                return true;
            }, ct);
        }

        public override async Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default)
        {
            if (_httpClient == null) throw new InvalidOperationException("设备未连接");

            using var content = new ByteArrayContent(data);
            content.Headers.ContentType = new MediaTypeHeaderValue(Param.ContentType);
            var response = await _httpClient.PostAsync(Param.ApiPrefix, content, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(ct);
        }

        public override Task<bool> SendAsync(byte[] data, CancellationToken ct = default)
            => WriteAsync("", data, ct);

        // ===== 高级泛型API =====

        /// <summary>GET 请求并反序列化为指定类型</summary>
        public async Task<T?> GetAsync<T>(string path, CancellationToken ct = default)
        {
            var bytes = await ReadAsync(path, 0, ct);
            return JsonSerializer.Deserialize<T>(bytes);
        }

        /// <summary>POST 请求：序列化对象为JSON并发送</summary>
        public async Task<TResponse?> PostAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken ct = default)
        {
            if (_httpClient == null) throw new InvalidOperationException("设备未连接");
            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, Param.ContentType);
            var fullPath = BuildPath(path);
            var response = await _httpClient.PostAsync(fullPath, content, ct);
            response.EnsureSuccessStatusCode();
            var respBytes = await response.Content.ReadAsByteArrayAsync(ct);
            return JsonSerializer.Deserialize<TResponse>(respBytes);
        }

        /// <summary>PUT 请求</summary>
        public async Task<TResponse?> PutAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken ct = default)
        {
            if (_httpClient == null) throw new InvalidOperationException("设备未连接");
            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, Param.ContentType);
            var fullPath = BuildPath(path);
            var response = await _httpClient.PutAsync(fullPath, content, ct);
            response.EnsureSuccessStatusCode();
            var respBytes = await response.Content.ReadAsByteArrayAsync(ct);
            return JsonSerializer.Deserialize<TResponse>(respBytes);
        }

        /// <summary>DELETE 请求</summary>
        public async Task<bool> DeleteAsync(string path, CancellationToken ct = default)
        {
            if (_httpClient == null) throw new InvalidOperationException("设备未连接");
            var fullPath = BuildPath(path);
            var response = await _httpClient.DeleteAsync(fullPath, ct);
            return response.IsSuccessStatusCode;
        }

        private string BuildPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return Param.ApiPrefix;
            if (path.StartsWith("/")) return $"{Param.ApiPrefix}{path}";
            return $"{Param.ApiPrefix}/{path}";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _httpClient?.Dispose();
            base.Dispose(disposing);
        }
    }
}

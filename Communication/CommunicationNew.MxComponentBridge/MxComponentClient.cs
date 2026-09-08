using System;
using System.Runtime.InteropServices;
using ActSupportMsg64Lib;
using ActUtlType64Lib;
using CommunicationNew.Protocols.Mitsubishi;

namespace CommunicationNew.MxComponentBridge;

/// <summary>
/// 封装 MX Component V5 64 位逻辑站控件。
/// 所有方法保留厂商返回码，由桥接协议统一转换为主进程可识别的失败。
/// </summary>
internal sealed class MxComponentClient : IDisposable
{
    private ActUtlType64Class? _control;
    private ActSupportMsg64Class? _supportMessages;
    private bool _opened;

    public int Open(int logicalStationNumber, string password)
    {
        if (_opened)
        {
            return 0;
        }

        try
        {
            // 延迟创建 COM 对象，使管道先建立；未安装 MX 时主进程可立即收到明确 HRESULT。
            _control ??= new ActUtlType64Class();
            _control.ActLogicalStationNumber = logicalStationNumber;
            _control.ActPassword = password ?? string.Empty;
            var result = _control.Open();
            _opened = result == 0;
            return result;
        }
        catch (COMException ex)
        {
            ReleaseControl();
            return ex.ErrorCode;
        }
    }

    public int Close()
    {
        if (!_opened)
        {
            return 0;
        }

        try
        {
            var result = _control!.Close();
            if (result == 0)
            {
                _opened = false;
            }
            return result;
        }
        catch (COMException ex)
        {
            // Close 失败后不再复用当前 COM 实例，避免下一次 Open 继承未知状态。
            ReleaseControl();
            return ex.ErrorCode;
        }
    }

    public int Read(string prefix, int start, bool isBit, int quantity, out byte[] data)
    {
        if (!_opened || _control is null)
        {
            data = Array.Empty<byte>();
            return unchecked((int)0xF0000004);
        }

        try
        {
            if (isBit)
            {
                return ReadBitBlock(prefix, start, quantity, out data);
            }

            var words = new short[quantity];
            var readResult = ReadDeviceBlock2(prefix, start, words);
            if (readResult != 0)
            {
                data = Array.Empty<byte>();
                return readResult;
            }

            data = MxComponentWordCodec.ToPayload(words);
            return 0;
        }
        catch (COMException ex)
        {
            data = Array.Empty<byte>();
            return ex.ErrorCode;
        }
    }

    public int Write(string prefix, int start, bool isBit, int quantity, byte[] payload)
    {
        if (!_opened || _control is null)
        {
            return unchecked((int)0xF0000004);
        }

        try
        {
            if (isBit)
            {
                return WriteBitBlock(prefix, start, quantity, payload);
            }

            var words = MxComponentWordCodec.FromPayload(payload);
            return WriteDeviceBlock2(prefix, start, words);
        }
        catch (COMException ex)
        {
            return ex.ErrorCode;
        }
    }

    /// <summary>调用 MX Component 的 ReadDeviceBlock2 读取连续设备字。</summary>
    private int ReadDeviceBlock2(string prefix, int start, short[] words)
    {
        return _control!.ReadDeviceBlock2(
            FormatAddress(prefix, start),
            words.Length,
            out words[0]);
    }

    /// <summary>调用 MX Component 的 WriteDeviceBlock2 写入连续设备字。</summary>
    private int WriteDeviceBlock2(string prefix, int start, short[] words)
    {
        return _control!.WriteDeviceBlock2(
            FormatAddress(prefix, start),
            words.Length,
            ref words[0]);
    }

    private int ReadBitBlock(string prefix, int start, int quantity, out byte[] data)
    {
        // 参考 CM1 的 MCHelper：MX 按字读取位设备，再将每个字按低位优先展开。
        // 起始点先向下对齐到 16 点，既支持任意位地址，也避免逐点 COM 往返。
        var window = MxComponentBitCodec.CreateReadWindow(start, quantity);
        var bitWords = new short[window.WordCount];
        var result = ReadDeviceBlock2(prefix, window.AlignedStart, bitWords);
        if (result != 0)
        {
            data = Array.Empty<byte>();
            return result;
        }

        // IMcClientAdapter 使用统一的 MC 二进制位格式：偶数点在高半字节，奇数点在低半字节。
        data = MxComponentBitCodec.PackReadWords(bitWords, window.BitOffset, quantity);
        return 0;
    }

    private int WriteBitBlock(string prefix, int start, int quantity, byte[] payload)
    {
        // 不直接使用 WriteDeviceBlock2：任意起始位的块写可能覆盖请求范围外的相邻位。
        for (var index = 0; index < quantity; index++)
        {
            var result = _control!.SetDevice2(
                FormatAddress(prefix, start + index),
                MxComponentBitCodec.GetWriteValue(payload, index));
            if (result != 0)
            {
                return result;
            }
        }
        return 0;
    }

    internal static string FormatAddress(string prefix, int number)
    {
        return McDeviceAddressFormatter.Format(prefix, number);
    }

    /// <summary>尽力把 MX 返回码转换为厂商错误文本；失败时返回空字符串并保留原错误码。</summary>
    public string GetErrorMessage(int errorCode)
    {
        try
        {
            _supportMessages ??= new ActSupportMsg64Class();
            var message = string.Empty;
            return _supportMessages.GetErrorMessage(errorCode, out message) == 0
                ? message ?? string.Empty
                : string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public void Dispose()
    {
        try
        {
            Close();
        }
        finally
        {
            ReleaseControl();
        }
    }

    private void ReleaseControl()
    {
        FinalReleaseComObject(_supportMessages);
        _supportMessages = null;
        FinalReleaseComObject(_control);
        _control = null;
        _opened = false;
    }

    private static void FinalReleaseComObject(object? instance)
    {
        if (instance is not null && Marshal.IsComObject(instance))
        {
            Marshal.FinalReleaseComObject(instance);
        }
    }
}

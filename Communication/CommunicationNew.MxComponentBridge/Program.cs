using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace CommunicationNew.MxComponentBridge;

internal static class Program
{
    private const int ConnectCommand = 1;
    private const int DisconnectCommand = 2;
    private const int ExecuteCommand = 3;

    private static int Main(string[] args)
    {
        if (!TryReadArgument(args, "--pipe", out var pipeName) ||
            !TryReadArgument(args, "--station", out var stationText) ||
            !int.TryParse(stationText, out var station))
        {
            Console.Error.WriteLine("用法: CommunicationNew.MxComponentBridge --pipe <name> --station <number>");
            return 2;
        }

        try
        {
            using var client = new MxComponentClient();
            using var pipe = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.None,
                inBufferSize: 0,
                outBufferSize: 0,
                CreateCurrentUserPipeSecurity());
            pipe.WaitForConnection();
            using var reader = new BinaryReader(pipe, System.Text.Encoding.UTF8, leaveOpen: true);
            using var writer = new BinaryWriter(pipe, System.Text.Encoding.UTF8, leaveOpen: true);

            while (pipe.IsConnected)
            {
                var command = reader.ReadInt32();
                var keepRunning = HandleCommand(command, station, client, reader, writer);
                writer.Flush();
                if (!keepRunning)
                {
                    break;
                }
            }
            return 0;
        }
        catch (EndOfStreamException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static bool HandleCommand(
        int command,
        int station,
        MxComponentClient client,
        BinaryReader reader,
        BinaryWriter writer)
    {
        if (command == ConnectCommand)
        {
            // 密码通过仅限本机的命名管道传递，不放入可被进程列表读取的命令行参数。
            var password = reader.ReadString();
            WriteStatus(writer, client, client.Open(station, password));
            return true;
        }

        if (command == DisconnectCommand)
        {
            WriteStatus(writer, client, client.Close());
            return false;
        }

        if (command != ExecuteCommand)
        {
            writer.Write(unchecked((int)0x80070057));
            writer.Write(0);
            return true;
        }

        var isWrite = reader.ReadBoolean();
        var prefix = reader.ReadString();
        var number = reader.ReadInt32();
        var isBit = reader.ReadBoolean();
        var quantity = reader.ReadInt32();
        var payloadLength = reader.ReadInt32();
        if (quantity <= 0 || payloadLength < 0 || payloadLength > 64 * 1024 * 1024)
        {
            writer.Write(unchecked((int)0x80070057));
            writer.Write(0);
            return true;
        }
        var payload = reader.ReadBytes(payloadLength);
        if (payload.Length != payloadLength ||
            (isWrite && payloadLength != (isBit ? (quantity + 1) / 2 : quantity * 2)) ||
            (!isWrite && payloadLength != 0))
        {
            writer.Write(unchecked((int)0x80070057));
            writer.Write(0);
            return true;
        }

        byte[] response;
        int result;
        if (isWrite)
        {
            result = client.Write(prefix, number, isBit, quantity, payload);
            response = Array.Empty<byte>();
        }
        else
        {
            result = client.Read(prefix, number, isBit, quantity, out response);
        }

        if (result != 0)
        {
            response = System.Text.Encoding.UTF8.GetBytes(client.GetErrorMessage(result));
        }

        writer.Write(result);
        writer.Write(response.Length);
        writer.Write(response);
        return true;
    }

    private static void WriteStatus(
        BinaryWriter writer,
        MxComponentClient client,
        int errorCode)
    {
        var message = errorCode == 0
            ? Array.Empty<byte>()
            : System.Text.Encoding.UTF8.GetBytes(client.GetErrorMessage(errorCode));
        writer.Write(errorCode);
        writer.Write(message.Length);
        writer.Write(message);
    }

    private static bool TryReadArgument(string[] args, string name, out string value)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
            {
                value = args[index + 1];
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    /// <summary>
    /// 创建只允许当前 Windows 身份读写的管道 ACL。
    /// 管道会传输 MX 密码，不能依赖随机名称作为唯一访问控制。
    /// </summary>
    private static PipeSecurity CreateCurrentUserPipeSecurity()
    {
        var currentUser = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("无法获取当前 Windows 用户 SID。");
        var security = new PipeSecurity();
        security.SetOwner(currentUser);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new PipeAccessRule(
            currentUser,
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));
        return security;
    }
}

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace NotITGBridge;

public class WebsocketServer : IDisposable
{
    private TcpListener _server;
    private readonly IPAddress _address;
    private readonly int _port;
    private readonly CancellationTokenSource _cancellationTokenSource;

    private ConcurrentQueue<SendCommand> _sendCommands;
    private TaskCompletionSource<byte[]>? _receiveTask;

    public WebsocketServer(string url, int port)
    {
        _sendCommands = new ConcurrentQueue<SendCommand>();
        _cancellationTokenSource = new CancellationTokenSource();
        _address = IPAddress.Parse(url);
        _port = port;
        ThreadPool.QueueUserWorkItem(StartServer, 
            _cancellationTokenSource.Token);
    }

    public Task SendAsync(string message)
    {
        var sCmd = new SendCommand(_cancellationTokenSource.Token, message);
        _sendCommands.Enqueue(sCmd);
        return sCmd.TaskCompletionSource.Task;
    }

    public void Send(string message)
    {
        _sendCommands.Enqueue(new SendCommand(_cancellationTokenSource.Token, message));
    }

    public Task<byte[]> ReceiveAsync(CancellationToken cancellationToken)
    {
        if (_receiveTask is null || _receiveTask.Task.IsCanceled || _receiveTask.Task.IsCompleted || _receiveTask.Task.IsFaulted)
        {
            _receiveTask = new TaskCompletionSource<byte[]>();
        }
        
        return _receiveTask.Task;
    }

    private void StartServer(object? parameter)
    {
        var handshakeDone = false;
        var cancellationToken = (CancellationToken)(parameter ?? throw new ArgumentNullException(nameof(parameter)));
        var server = new TcpListener(_address, _port);
        server.Start();
        using TcpClient client = server.AcceptTcpClient();
        
        Console.WriteLine("A client connected.");
        
        using var stream = client.GetStream();

        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Console.WriteLine("Stopping websocket server...");
                server.Stop();
                return;
            }
            
            if (stream.DataAvailable && client.Available > 3)
            {
                byte[] bytes = new byte[client.Available];
                stream.Read(bytes, 0, client.Available);
                string s = Encoding.UTF8.GetString(bytes);
            
                if (Regex.IsMatch(s, "^GET", RegexOptions.IgnoreCase)) {
                    Console.WriteLine("=====Handshaking from client=====\n{0}", s);

                    // 1. Obtain the value of the "Sec-WebSocket-Key" request header without any leading or trailing whitespace
                    // 2. Concatenate it with "258EAFA5-E914-47DA-95CA-C5AB0DC85B11" (a special GUID specified by RFC 6455)
                    // 3. Compute SHA-1 and Base64 hash of the new value
                    // 4. Write the hash back as the value of "Sec-WebSocket-Accept" response header in an HTTP response
                    string swk = Regex.Match(s, "Sec-WebSocket-Key: (.*)").Groups[1].Value.Trim();
                    string swka = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                    byte[] swkaSha1 = System.Security.Cryptography.SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swka));
                    string swkaSha1Base64 = Convert.ToBase64String(swkaSha1);

                    // HTTP/1.1 defines the sequence CR LF as the end-of-line marker
                    byte[] response = Encoding.UTF8.GetBytes(
                        "HTTP/1.1 101 Switching Protocols\r\n" +
                        "Connection: Upgrade\r\n" +
                        "Upgrade: websocket\r\n" +
                        "Sec-WebSocket-Accept: " + swkaSha1Base64 + "\r\n\r\n");

                    stream.Write(response, 0, response.Length);

                    handshakeDone = true;
                }
            }
            
            /* else {
                bool fin = (bytes[0] & 0b10000000) != 0,
                    mask = (bytes[1] & 0b10000000) != 0; // must be true, "All messages from the client to the server have this bit set"

                int opcode = bytes[0] & 0b00001111, // expecting 1 - text message
                    msglen = bytes[1] - 128, // & 0111 1111
                    offset = 2;

                if (msglen == 126) {
                    // was ToUInt16(bytes, offset) but the result is incorrect
                    msglen = BitConverter.ToUInt16(new byte[] { bytes[3], bytes[2] }, 0);
                    offset = 4;
                } else if (msglen == 127) {
                    Console.WriteLine("TODO: msglen == 127, needs qword to store msglen");
                    // i don't really know the byte order, please edit this
                    // msglen = BitConverter.ToUInt64(new byte[] { bytes[5], bytes[4], bytes[3], bytes[2], bytes[9], bytes[8], bytes[7], bytes[6] }, 0);
                    // offset = 10;
                }

                if (msglen == 0)
                    Console.WriteLine("msglen == 0");
                else if (mask) {
                    byte[] decoded = new byte[msglen];
                    byte[] masks = new byte[4] { bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3] };
                    offset += 4;

                    for (int i = 0; i < msglen; ++i)
                        decoded[i] = (byte)(bytes[offset + i] ^ masks[i % 4]);

                    string text = Encoding.UTF8.GetString(decoded);
                    Console.WriteLine("{0}", text);
                } else
                    Console.WriteLine("mask bit not set");
                
                Console.WriteLine();
            }*/
            
            
            while (handshakeDone && _sendCommands.TryDequeue(out var sendCommand))
            {
                stream.Write(CreateFrame(Encoding.UTF8.GetBytes(sendCommand.Message)));
                //sendCommand.TaskCompletionSource.SetResult();
            }
        }
    }

    private byte[] CreateFrame(byte[] message)
    {
        var frame = new List<byte>(){0b10000001, (byte)(0x0 + message.Length)};
        return frame.Concat(message).ToArray();
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        //_server.Stop();
    }
    
    private class SendCommand
    {
        public readonly TaskCompletionSource TaskCompletionSource;
        public readonly string Message;

        public SendCommand(CancellationToken token, string message)
        {
            Message = message;
            TaskCompletionSource = new TaskCompletionSource();
        }
    }
}
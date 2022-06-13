using System.Collections.Immutable;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using NotITGBridge;

public static class Program
{
    public static async Task Main()
    {
        var config = ReadConfig();
        Console.WriteLine($"config :");
        Console.WriteLine($"    - Format : {config.Format}");
        Console.WriteLine($"    - Port : {config.Port}");
        
        using var webSocketServer = new WebsocketServer("127.0.0.1", config.Port);
        using var notItgDataSource = new NotItgDataSource();
        var previousValue = "0000";
        
        //If notitg is not detected we give up and stop
        if (!notItgDataSource.StartReading())
        {
            Console.WriteLine("Stopping...");
            return;
        }
        
        while (true)
        {
            if (!notItgDataSource.TryGetMemoryContent(out var memoryContent))
            {
                Console.WriteLine("Could not read memory");
            }

            if (memoryContent.Equals(string.Empty) || memoryContent.Equals(previousValue))
            {
                continue;
            }
            
            Console.WriteLine(memoryContent);
            webSocketServer.Send(memoryContent);
            previousValue = memoryContent;
        }
    }

    public static AppSettings ReadConfig()
    {
        var configFile = File.ReadAllText("appsettings.json");
        return JsonSerializer.Deserialize<AppSettings>(configFile) ?? throw new InvalidOperationException();
    }

    public class AppSettings
    {
        public string Format { get; set; } = "{0}{1}{2}{3}";
        public int Port { get; set; } = 6210;
    }
}
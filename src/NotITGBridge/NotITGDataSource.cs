using NotITG.External;

namespace NotITGBridge;

public class NotItgDataSource : IDisposable
{
    private readonly NotITG.External.NotITG _notItgHandler;
    
    public NotItgDataSource()
    {
        _notItgHandler = new NotITG.External.NotITG();
    }

    /// <summary>
    /// Must be called first to start reading memory from NotITG
    /// </summary>
    /// <returns></returns>
    public bool StartReading()
    {
        Console.WriteLine("Checking if NotITG is detected...");
        var date = DateTime.Now;
        
        while (!_notItgHandler.Scan() && (DateTime.Now - date) < TimeSpan.FromSeconds(300))
        {
            //We retry for 30 seconds and if NotITG is not found by the end we give up
        }

        if (!_notItgHandler.Connected)
        {
            Console.WriteLine("NotItg not found :(");
            return false;
        }
        
        Console.WriteLine("NotITG found :)");
        Console.WriteLine("Starting to read memory content.");
        
        return true;
    }

    /// <summary>
    /// Try to read memory content from NotITG
    /// </summary>
    /// <param name="memoryContent"></param>
    /// <returns></returns>
    public bool TryGetMemoryContent(out string memoryContent)
    {
        memoryContent = string.Empty;
        
        if (_notItgHandler.Connected)
        {
            memoryContent = MemoryContent;
        }
        else
        {
            Console.WriteLine("NotITG not connected");
        }
        
        return !memoryContent.Equals(string.Empty);
    }

    /// <summary>
    /// Current memory content in NotITG, can throw an exception if not connected
    /// </summary>
    private string MemoryContent =>
        $"{_notItgHandler.GetExternal(0)}{_notItgHandler.GetExternal(1)}{_notItgHandler.GetExternal(2)}{_notItgHandler.GetExternal(3)}";

    public void Dispose()
    {
        _notItgHandler.Disconnect();
    }
}
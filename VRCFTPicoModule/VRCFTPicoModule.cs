using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using VRCFaceTracking;
using VRCFTPicoModule.Utils;

namespace VRCFTPicoModule;

public class VRCFTPicoModule : ExtTrackingModule
{
    private static readonly int[] Ports = [29765, 29763];
    private static readonly UdpClient[] Clients = Ports.Select(port => new UdpClient(port) { Client = { ReceiveTimeout = 100 } }).ToArray();
    private static UdpClient _udpClient = new();
    private static int _port;
    private Updater? _updater;

    public override (bool SupportsEye, bool SupportsExpression) Supported => (true, true);

    public override (bool eyeSuccess, bool expressionSuccess) Initialize(bool eyeAvailable, bool expressionAvailable)
    {
        Logger.LogInformation("Starting initialization");
        var initializationResult = InitializeAsync().GetAwaiter().GetResult();
        if (initializationResult is { eyeSuccess: true, expressionSuccess: true })
        {
            UpdateModuleInfo();
        }
        return initializationResult;
    }

    private async Task<(bool eyeSuccess, bool expressionSuccess)> InitializeAsync()
    {
        Logger.LogDebug("Initializing UDP Clients on ports: {0}", string.Join(", ", Ports));

        int portIndex = await ListenOnPorts();
        if (portIndex == -1) return (false, false);

        _port = Ports[portIndex];
        _udpClient = new UdpClient(_port);
        Logger.LogInformation("Using port: {0}", _port);

        _updater = new Updater(_udpClient, Logger, _port == Ports[1]);

        return (true, true);
    }

    private void UpdateModuleInfo()
    {
        ModuleInformation.Name = "PICO Connect";
        var stream = GetType().Assembly.GetManifestResourceStream("VRCFTPicoModule.Assets.pico.png");
        ModuleInformation.StaticImages = stream != null ? [stream] : ModuleInformation.StaticImages;
    }

    private async Task<int> ListenOnPorts()
    {
        try
        {
            var tasks = Clients.Select(client => client.ReceiveAsync()).ToArray();
        
            if (tasks.Length == 0)
            {
                return -1;
            }
        
            var completedTask = await Task.WhenAny(tasks);

            foreach (var client in Clients) client.Dispose();
        
            return Array.IndexOf(tasks, completedTask);
        }
        catch (Exception ex)
        {
            Logger.LogError("Initialization failed, exception: {0}", ex);
        }
    
        return -1;
    }

    public override void Update()
    {
        if (_updater == null)
            return;
        _updater.ModuleState = Status;
        _updater.Update();
    }

    public override void Teardown()
    {
        foreach (var client in Clients)
        {
            client.Dispose();
        }
        _udpClient.Dispose();
        _updater = null;
    }
}
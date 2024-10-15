using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using VRCFaceTracking;

namespace VRCFTPicoModule;

public partial class VRCFTPicoModule : ExtTrackingModule
{
    private static readonly int[] Ports = { 29765, 29763 };
    private static readonly UdpClient[] Clients = Ports.Select(port => new UdpClient(port) { Client = { ReceiveTimeout = 100 } }).ToArray();
    private static UdpClient udpClient = new();
    private static int Port = 0;
    private Updater? updater;

    public override (bool SupportsEye, bool SupportsExpression) Supported => (true, true);

    public override (bool eyeSuccess, bool expressionSuccess) Initialize(bool eyeAvailable, bool expressionAvailable)
    {
        Logger.LogInformation("Starting initialization");
        var initializationResult = InitializeAsync().GetAwaiter().GetResult();
        if (initializationResult.eyeSuccess && initializationResult.expressionSuccess)
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

        Port = Ports[portIndex];
        udpClient = new UdpClient(Port);
        Logger.LogInformation("Using port: {0}", Port);

        updater = new Updater(udpClient, Logger, Port == Ports[1]);

        return (true, true);
    }

    private void UpdateModuleInfo()
    {
        ModuleInformation.Name = "PICO Connect";
        var stream = GetType().Assembly.GetManifestResourceStream("VRCFTPicoModule.Assets.pico.png");
        ModuleInformation.StaticImages = stream != null ? new List<Stream> { stream } : ModuleInformation.StaticImages;
    }

    private async Task<int> ListenOnPorts()
    {
        try
        {
            var tasks = Clients.Select(client => client.ReceiveAsync()).ToArray();
            var completedTask = await Task.WhenAny(tasks);

            if (completedTask != null)
            {
                foreach (var client in Clients) client.Dispose();
                return Array.IndexOf(tasks, completedTask);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Initialization failed, exception: {0}", ex);
        }
        return -1;
    }

    public override void Update()
    {
        if (updater == null)
            return;
        updater.moduleState = Status;
        updater.Update();
    }

    public override void Teardown()
    {
        foreach (var client in Clients)
        {
            client.Dispose();
        }
        udpClient.Dispose();
        updater = null;
    }
}
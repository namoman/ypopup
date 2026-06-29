namespace Ypopup.Models;

public sealed class AppSettings
{
    public string MachineId { get; set; } = Guid.NewGuid().ToString();
    public string DisplayName { get; set; } = Environment.UserName;
    public int TcpPort { get; set; } = 50506;
    public int DiscoveryPort { get; set; } = 50505;
    public string ReceiveDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Ypopup",
        "Received");
    public bool PlayNotificationSound { get; set; } = true;
}

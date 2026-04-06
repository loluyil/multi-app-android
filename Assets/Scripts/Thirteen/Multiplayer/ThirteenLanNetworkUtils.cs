using System.Linq;
using System.Net;
using System.Net.Sockets;

public static class ThirteenLanNetworkUtils
{
    public static string GetLocalIpv4Address()
    {
        try
        {
            string address = Dns.GetHostEntry(Dns.GetHostName())
                .AddressList
                .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                ?.ToString();

            return string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address;
        }
        catch
        {
            return "127.0.0.1";
        }
    }
}

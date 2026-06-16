using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine;

public static class LocalIpProvider
{
    public static string GetLocalIP()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return ip.Address.ToString();
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[IP] Error: " + e.Message);
        }
        return "127.0.0.1";
    }
}

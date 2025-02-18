using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;

namespace Lab1;

class Program
{
    private static SemaphoreSlim semaphore;

    public static async Task ParallelPingSemaphore(byte[] adressesMasks, byte[] networks, int maxThreads)
    {
        semaphore = new SemaphoreSlim(maxThreads, maxThreads);
        List<Task> pingTasks = new List<Task>();

        for (int I = adressesMasks[0]; I <= 255; I++)
        {
            for (int II = adressesMasks[1]; II <= 255; II++)
            {
                for (int III = adressesMasks[2]; III <= 255; III++)
                {
                    for (int IV = adressesMasks[3]; IV <= 255; IV++)
                    {
                        if ((I == 255 && II == 255 && III == 255 && IV == 255) ||
                            (I == 0 && II == 0 && III == 0 && IV == 0))
                        {
                            continue;
                        }

                        string targetIP = string.Concat(I - (networks[0] ^ adressesMasks[0]), ".",
                            II - (networks[1] ^ adressesMasks[1]), ".", III - (networks[2] ^ adressesMasks[2]), ".",
                            IV - (networks[3] ^ adressesMasks[3]));

                        await semaphore.WaitAsync();
                        pingTasks.Add(Task.Run(() => PingAddressSemaphoreAsync(targetIP)));
                    }
                }
            }
        }

        await Task.WhenAll(pingTasks);
        semaphore.Dispose();
    }

    static async Task PingAddressSemaphoreAsync(string targetIP)
    {
        Ping ping = new Ping();
        Console.WriteLine($"Пингую (Thread {Environment.CurrentManagedThreadId}): {targetIP}");

        try
        {
            PingReply reply = await ping.SendPingAsync(targetIP, 1);
            if (reply.Status == IPStatus.Success)
            {
                Console.WriteLine($"Устройство найдено (Thread {Environment.CurrentManagedThreadId}): {targetIP}");
            }
        }
        catch (PingException ex)
        {
            Console.WriteLine($"Ошибка пинга для {targetIP}: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Непредвиденная ошибка для {targetIP}: {ex.Message}");
        }
        finally
        {
            ping.Dispose();
            semaphore.Release();
        }
    }
    public static async Task Main(string[] args)
    {
        
        var (ipAddress, subnetMask, networkAddress) = GetWiFiNetworkInfo();

        if (ipAddress != null && subnetMask != null && networkAddress != null)
        {
            Console.WriteLine("Your IPv4 Address: " + ipAddress);
            Console.WriteLine("Subnet Mask: " + subnetMask);
            Console.WriteLine("Network Address: " + networkAddress);
        }
        else
        {
            Console.WriteLine("Wi-Fi adapter not found or no IPv4 address available.");
        }
        byte[] adressesMasks = subnetMask!.Split('.').Select(byte.Parse).ToArray();
        byte[] networks = networkAddress!.Split('.').Select(byte.Parse).ToArray();
        //string targetIp = string.Concat((byte)(~adressesMasks[0] + networks[0]), ".", (byte)(~adressesMasks[1] + networks[1]), ".", (byte)(~adressesMasks[2] + networks[2]), ".", (byte)(~adressesMasks[3] + networks[3]));
        //ViewArpCache();

        /*for (int I = adressesMasks[0]; I <= 255; I++)
        {
            for (int II = adressesMasks[1]; II <= 255; II++)
            {
                for (int III = adressesMasks[2]; III <= 255; III++)
                {
                    for (int IV = adressesMasks[3]; IV <= 255; IV++)
                    {
                        if ((I == 255 && II == 255 && III == 255 && IV == 255) ||
                            (I == 0 && II == 0 && III == 0 && IV == 0))
                        {
                            continue;
                        }

                        Ping ping = new Ping();
                        string targetIP = string.Concat(I - (networks[0] ^ adressesMasks[0]), ".",
                            II - (networks[1] ^ adressesMasks[1]), ".", III - (networks[2] ^ adressesMasks[2]), ".",
                            IV - (networks[3] ^ adressesMasks[3]));
                        Console.WriteLine(targetIP);
                        PingReply reply = ping.Send(targetIP, 1);
                        if (reply.Status == IPStatus.Success)
                        {
                            Console.WriteLine($"Device found: {targetIP}");
                            //ViewArpCache();
                        }

                    }
                }
            }
        }*/
        int maxThreads = Environment.ProcessorCount * 2;
        Console.WriteLine($"Начинаем параллельное сканирование с SemaphoreSlim, макс. потоков: {maxThreads}...");
        await ParallelPingSemaphore(adressesMasks, networks, maxThreads);
        Console.WriteLine("Параллельное сканирование завершено.");
        ViewArpCache();
    }
        
    static (string? ipAddress, string? subnetMask, string? networkAddress) GetWiFiNetworkInfo()
    {
        var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

        foreach (var networkInterface in networkInterfaces)
        {
            if (networkInterface.OperationalStatus == OperationalStatus.Up &&
                networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
            {
                var ipProperties = networkInterface.GetIPProperties();
                var unicastAddresses = ipProperties.UnicastAddresses;

                foreach (var unicastAddress in unicastAddresses)
                {
                    if (unicastAddress.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        string ipAddress = unicastAddress.Address.ToString();
                        string subnetMask = unicastAddress.IPv4Mask.ToString();
                        string networkAddress = CalculateNetworkAddress(ipAddress, subnetMask);

                        return (ipAddress, subnetMask, networkAddress);
                    }
                }
            }
        }

        return (null, null, null);
    }

    static void ViewArpCache()
    {
        ProcessStartInfo psi = new ProcessStartInfo("arp", "-a")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process process = new Process { StartInfo = psi };
        process.Start();

        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        Console.WriteLine("ARP Cache:");
        Console.WriteLine(output);
    }
    
    static string CalculateNetworkAddress(string ipAddress, string subnetMask)
    {
        byte[] ipBytes = IPAddress.Parse(ipAddress).GetAddressBytes();
        byte[] maskBytes = IPAddress.Parse(subnetMask).GetAddressBytes();
        
        byte[] networkBytes = new byte[ipBytes.Length];
        for (int i = 0; i < ipBytes.Length; i++)
        {
            networkBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);
        }
        return new IPAddress(networkBytes).ToString();
    }
}
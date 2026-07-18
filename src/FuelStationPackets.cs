using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib.Utils;

namespace FuelStationRefill
{
    /// <summary>
    /// Packet: host -> all clients. Full fuel state for all zones.
    /// </summary>
    public class FuelStationSyncPacket : INetSerializable
    {
        public int ZoneCount;
        public float[] CurrentFuels;
        public float[] MaxFuels;
        public bool[] IsActive;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(ZoneCount);
            for (int i = 0; i < ZoneCount; i++)
            {
                writer.Put(CurrentFuels != null && i < CurrentFuels.Length ? CurrentFuels[i] : 0f);
                writer.Put(MaxFuels != null && i < MaxFuels.Length ? MaxFuels[i] : 0f);
                writer.Put(IsActive != null && i < IsActive.Length && IsActive[i]);
            }
        }

        public void Deserialize(NetDataReader reader)
        {
            ZoneCount = reader.GetInt();
            CurrentFuels = new float[ZoneCount];
            MaxFuels = new float[ZoneCount];
            IsActive = new bool[ZoneCount];
            for (int i = 0; i < ZoneCount; i++)
            {
                CurrentFuels[i] = reader.GetFloat();
                MaxFuels[i] = reader.GetFloat();
                IsActive[i] = reader.GetBool();
            }
        }
    }

    /// <summary>
    /// Packet: client -> host. Reports fuel consumed from a zone.
    /// </summary>
    public class FuelStationConsumePacket : INetSerializable
    {
        public int ZoneIndex;
        public float AmountConsumed;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(ZoneIndex);
            writer.Put(AmountConsumed);
        }

        public void Deserialize(NetDataReader reader)
        {
            ZoneIndex = reader.GetInt();
            AmountConsumed = reader.GetFloat();
        }
    }
}

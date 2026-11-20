using System;
using Unity.Collections;
using Unity.Netcode;

public struct PlayerInfo : INetworkSerializable, IEquatable<PlayerInfo>
{
    public ulong ClientId;
    public ushort PingMs;
    public FixedString64Bytes PlayerId;
    public FixedString64Bytes Name;

    public PlayerInfo(ulong clientId, string playerId, string name)
    {
        ClientId = clientId;
        PingMs = 0;
        PlayerId = playerId;
        Name = name;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref PlayerId);
        serializer.SerializeValue(ref Name);
        serializer.SerializeValue(ref PingMs);
    }

    public bool Equals(PlayerInfo other)
    {
        return ClientId == other.ClientId &&
               PlayerId.Equals(other.PlayerId) &&
               Name.Equals(other.Name) &&
               PingMs == other.PingMs;
    }
}

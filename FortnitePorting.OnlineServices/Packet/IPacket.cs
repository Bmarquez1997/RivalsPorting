using FortnitePorting.OnlineServices.Models;

namespace FortnitePorting.OnlineServices.Packet;

public interface IPacket : IDualSerialize
{
    public EPacketType PacketType { get; }
}

public enum EPacketType
{
    // General
    Connect,
    Disconnect,
    Ping,
    Permissions,
    
    // Chat
    Message,
    Reaction,
    OnlineUsers,
    Export,
    SetRole,
    MessageHistory,
    
    // Place
    PlaceCanvasInfo,
    PlacePixel,
    PlaceStatusInfo,
}
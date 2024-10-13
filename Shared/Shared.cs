

using System;
using System.IO;
using System.Net;

#if FEZCLIENT
using ActionType = FezGame.Structure.ActionType;
using HorizontalDirection = FezEngine.HorizontalDirection;
using Viewpoint = FezEngine.Viewpoint;
using Vector3 = Microsoft.Xna.Framework.Vector3;
#else
using ActionType = System.Int32;
using HorizontalDirection = System.Int32;
using Viewpoint = System.Int32;
public struct Vector3
{
    public float X, Y, Z;
    public Vector3(float x, float y, float z)
    {
        X = x; Y = y; Z = z;
    }
}
#endif
namespace FezGame.MultiplayerMod
{
    class SharedConstants
    {
        public static readonly int DefaultPort = 7777;
    }

    [Serializable]
    public struct PlayerMetadata//TODO separate into PlayerMetadata and ServerPlayerMetadata
    {
        public IPEndPoint Endpoint;
        public readonly Guid Uuid;
        public string PlayerName;
        public string CurrentLevelName;
        public Vector3 Position;
        public ActionType Action;
        public int AnimFrame;
        /// <summary>
        /// Only used so we only keep the latest data on the client
        /// </summary>
        public long LastUpdateTimestamp;
        public HorizontalDirection LookingDirection;
        public Viewpoint CameraViewpoint;
        /// <summary>
        /// for auto-disposing, since LastUpdateTimestamp shouldn't be used for that because the system clocks of the two protocols could be different
        /// </summary>
        public long LastUpdateLocalTimestamp;

        public PlayerMetadata(IPEndPoint Endpoint, Guid Uuid, string PlayerName, string CurrentLevelName, Vector3 Position, Viewpoint CameraViewpoint, ActionType Action, int AnimFrame, HorizontalDirection LookingDirection, long LastUpdateTimestamp, long LastUpdateLocalTimestamp)
        {
            this.Endpoint = Endpoint;
            this.Uuid = Uuid;
            this.PlayerName = PlayerName;
            this.CurrentLevelName = CurrentLevelName;
            this.Position = Position;
            this.Action = Action;
            this.AnimFrame = AnimFrame;
            this.LookingDirection = LookingDirection;
            this.LastUpdateTimestamp = LastUpdateTimestamp;
            this.CameraViewpoint = CameraViewpoint;
            this.LastUpdateLocalTimestamp = LastUpdateLocalTimestamp;
        }
    }
    public enum PacketType
    {
        //arbitrary values
        PlayerInfo = 1,
        Notice = 3,//currently unused
        Disconnect = 7,
        Message = 9,//currently unused
    }
    public static class MyExtensions
    {
        public static Guid ReadGuid(this BinaryReader reader)
        {
            return new Guid(reader.ReadInt32(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
        }
        public static void Write(this BinaryWriter writer, Guid guid)
        {
            writer.Write(guid.ToByteArray());
        }
    }
}
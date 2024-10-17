

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;

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
    public class PlayerAppearance
    {
        public string PlayerName;
        public object CustomCharacterAppearance;
    }
    [Serializable]
    public class PlayerMetadata
    {
        public readonly Guid Uuid;
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

        public PlayerMetadata(Guid Uuid, string CurrentLevelName, Vector3 Position, Viewpoint CameraViewpoint, ActionType Action, int AnimFrame, HorizontalDirection LookingDirection, long LastUpdateTimestamp)
        {
            this.Uuid = Uuid;
            this.CurrentLevelName = CurrentLevelName;
            this.Position = Position;
            this.Action = Action;
            this.AnimFrame = AnimFrame;
            this.LookingDirection = LookingDirection;
            this.LastUpdateTimestamp = LastUpdateTimestamp;
            this.CameraViewpoint = CameraViewpoint;
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

    public abstract class SharedNetcode
    {
        #region network packet stuff
        protected const string ProtocolSignature = "FezMultiplayer";// Do not change
        public const string ProtocolVersion = "quince";//Update this ever time you change something that affect the packets

        public volatile string ErrorMessage = null;//Note: this gets updated in the listenerThread
        /// <summary>
        /// If not null, contains a fatal exception that was thrown on a child Thread
        /// </summary>
        public volatile Exception FatalException = null;

        public abstract ConcurrentDictionary<Guid, PlayerMetadata> Players { get; }
        public ConcurrentDictionary<Guid, PlayerAppearance> PlayerAppearance = new ConcurrentDictionary<Guid, PlayerAppearance>();

        //TODO make these packet things more extensible somehow?

        protected static byte[] SerializeDisconnect(Guid uuid)
        {
            using (MemoryStream m = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(m))
                {
                    /* 
                     * Note: these pragma things are here because I feel 
                     * it's not clear which Read method should be called
                     * when all the Write methods have the same name.
                     */
#pragma warning disable IDE0004
#pragma warning disable IDE0049
                    writer.Write((String)ProtocolSignature);
                    writer.Write((String)ProtocolVersion);
                    writer.Write((Byte)PacketType.Disconnect);
                    writer.Write((Int64)DateTime.UtcNow.Ticks);
                    writer.Write((Guid)uuid);
#pragma warning restore IDE0004
#pragma warning restore IDE0049
                    writer.Flush();
                    return m.ToArray();
                }
            }
        }

        protected static byte[] Serialize(PlayerMetadata p)
        {
            using (MemoryStream m = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(m))
                {
                    /* 
                     * Note: these pragma things are here because I feel 
                     * it's not clear which Read method should be called
                     * when all the Write methods have the same name.
                     */
#pragma warning disable IDE0004
#pragma warning disable IDE0049
                    writer.Write((String)ProtocolSignature);
                    writer.Write((String)ProtocolVersion);
                    writer.Write((Byte)PacketType.PlayerInfo);
                    writer.Write((Int64)p.LastUpdateTimestamp);
                    writer.Write((Guid)p.Uuid);
                    writer.Write((String)p.CurrentLevelName ?? "");
                    writer.Write((Single)p.Position.X);
                    writer.Write((Single)p.Position.Y);
                    writer.Write((Single)p.Position.Z);
                    writer.Write((Int32)p.CameraViewpoint);
                    writer.Write((Int32)p.Action);
                    writer.Write((Int32)p.AnimFrame);
                    writer.Write((Int32)p.LookingDirection);
#pragma warning restore IDE0004
#pragma warning restore IDE0049
                    writer.Flush();
                    return m.ToArray();
                }
            }
        }

        //
        // For player names and whatnot
        // should be an inclusive list of all characters supported by all of the languages' game fonts
        // probably should restrict certain characters for internal use (like punctuation and symbols)
        // also need to figure out how to handle people using characters that are not in this list
        //
        // Complete list of common chars: (?<=")[A-Za-z0-9 !"#$%&'()*+,\-./:;<>=?@\[\]\\^_`{}|~]+(?=")
        //
        // Common punctuation characters: [ !"#$%&'()*+,\-./:;<>=?@\[\]^_`{}|~]
        // Potential reserved characters: [ #$&\\`]
        //
        // TODO add a universal font?
        // Note the hanzi/kanji/hanja (Chinese characters) look slightly different in Chinese vs Japanese vs Korean;
        //     Might be worth making a system that can write the player name using multiple fonts

        //unused
        //public static readonly System.Text.RegularExpressions.Regex commonCharRegex = new System.Text.RegularExpressions.Regex(@"[^\x20-\x7E]");

        //more strict so we can potentially add features (such as colors and effects) using special characters later
        public static readonly System.Text.RegularExpressions.Regex nameInvalidCharRegex = new System.Text.RegularExpressions.Regex(@"[^0-9A-Za-z]");

        protected const int maxplayernamelength = 32;

        protected void ProcessDatagram(byte[] data)
        {
            using (MemoryStream m = new MemoryStream(data))
            {
                using (BinaryReader reader = new BinaryReader(m))
                {
                    if (!ProtocolSignature.Equals(reader.ReadString()))
                    {
                        //Not a FezMultiplayer packet
                        return;
                    }
                    if (!ProtocolVersion.Equals(reader.ReadString()))
                    {
                        //Not the right version of the FezMultiplayer protocol
                        //TODO notify the user?
                        return;
                    }

                    PacketType packetType = (PacketType)reader.ReadByte();
                    long timestamp = reader.ReadInt64();

                    switch (packetType)
                    {
                    case PacketType.PlayerInfo:
                        {
                            IPEndPoint endpoint;
                            try
                            {
                                string endip = reader.ReadString();
                                int endport = reader.ReadInt32();
                                // Note: the above reads from the binary reader are there to ensure they both get called so the reader doesn't skip a value
                                endpoint = new IPEndPoint(IPAddress.Parse(endip), endport);
                            }
                            catch (Exception)//catches exceptions for when the IP endpoint received is invalid
                                             // probably should be changed so it doesn't catch the exceptions thrown by the reader, but it's probably fine
                            {
                                endpoint = new IPEndPoint(IPAddress.None, 0);
                            }
                            Guid uuid = reader.ReadGuid();
                            string playername = reader.ReadString();
                            playername = nameInvalidCharRegex.Replace(playername.Length > maxplayernamelength ? playername.Substring(0, maxplayernamelength) : playername, "");
                            string lvl = reader.ReadString();
                            Vector3 pos = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                            Viewpoint vp = (Viewpoint)reader.ReadInt32();
                            ActionType act = (ActionType)reader.ReadInt32();
                            int frame = reader.ReadInt32();
                            HorizontalDirection lookdir = (HorizontalDirection)reader.ReadInt32();

                            PlayerMetadata p = Players.GetOrAdd(uuid, (guid) =>
                            {
                                var np = new PlayerMetadata(
                                    Uuid: guid,
                                    CurrentLevelName: lvl,
                                    Position: pos,
                                    CameraViewpoint: vp,
                                    Action: act,
                                    AnimFrame: frame,
                                    LookingDirection: lookdir,
                                    LastUpdateTimestamp: timestamp
                                );
                                return np;
                            });
                            if (timestamp > p.LastUpdateTimestamp)//Ensure we're not saving old data
                            {
                                //update player
                                p.CurrentLevelName = lvl;
                                p.Position = pos;
                                p.CameraViewpoint = vp;
                                p.Action = act;
                                p.AnimFrame = frame;
                                p.LookingDirection = lookdir;
                                p.LastUpdateTimestamp = timestamp;
                            }
                            Players[uuid] = p;
                            break;
                        }
                    case PacketType.Disconnect:
                        {
                            try
                            {
                                Guid puid = reader.ReadGuid();
                                ProcessDisconnect(puid);
                            }
                            catch (InvalidOperationException) { }
                            catch (KeyNotFoundException) { } //this can happen if an item is removed by another thread while this thread is iterating over the items

                            break;
                        }
                    case PacketType.Message:
                        {
                            //TBD
                            break;
                        }
                    case PacketType.Notice:
                        {
                            //TBD
                            break;
                        }
                    default:
                        {
                            //Unsupported packet type
                            ErrorMessage = "Unsupported PacketType: " + packetType;
                            return;
                        }
                    }
                }
            }
        }

        protected void SendTcp(byte[] msg, NetworkStream stream)
        {
            stream.Write(msg);//TODO
            return;
        }
        protected void ReadTcp(byte[] msg, NetworkStream stream)
        {
            stream.Read();//TODO
            return;
        }
        protected abstract void ProcessDisconnect(Guid puid);
        #endregion
    }
}
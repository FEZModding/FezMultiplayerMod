

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Diagnostics;

#if FEZCLIENT
using ActionType = FezGame.Structure.ActionType;
using ActorType = FezEngine.Structure.ActorType;
using TrileEmplacement = FezEngine.Structure.TrileEmplacement;
using HorizontalDirection = FezEngine.HorizontalDirection;
using Viewpoint = FezEngine.Viewpoint;
using Vector3 = Microsoft.Xna.Framework.Vector3;
#else
using ActionType = FezMultiplayerDedicatedServer.ActionType;
using ActorType = FezMultiplayerDedicatedServer.ActorType;
using HorizontalDirection = FezMultiplayerDedicatedServer.HorizontalDirection;
using Viewpoint = FezMultiplayerDedicatedServer.Viewpoint;
using Vector3 = FezMultiplayerDedicatedServer.Vector3;
using TrileEmplacement = FezMultiplayerDedicatedServer.TrileEmplacement;
#endif
namespace FezSharedTools
{
    internal static class SharedTools
    {
        public static void LogWarning(string ComponentName, string message, int LogSeverity = 1)
        {
            string ThreadName = System.Threading.Thread.CurrentThread.Name;
            if (!string.IsNullOrEmpty(ThreadName))
            {
                message = $"({ThreadName}) " + message;
            }
            LogSeverity = Math.Max(0, Math.Min(LogSeverity, 2));
#if FEZCLIENT
            Common.Logger.Log(ComponentName, (Common.LogSeverity)LogSeverity, message);
#endif
            string msgType;
            switch(LogSeverity)
            {
            case 0:
                msgType = "Information";
                break;
            case 1:
                msgType = "Warning";
                break;
            case 2:
            default:
                msgType = "Error";
                break;
            }
#if FEZCLIENT
            message = $"{msgType}: {message}";
#else
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            message = $"{timestamp} [{msgType}] {ComponentName} - {message}";
#endif
            Console.WriteLine(message);
            System.Diagnostics.Debug.WriteLine(message);
        }
        public static void ForceDisconnect(this System.Net.Sockets.Socket socket)
        {
            socket.Shutdown(System.Net.Sockets.SocketShutdown.Both);
            socket.Close();
        }
    }
    internal static class SharedConstants
    {
        public static readonly int DefaultPort = 7777;
        /// <summary>
        /// The IP address on which local multiplayer servers broadcast their existence.
        /// TODO implement this as per <a href="https://github.com/FEZModding/FezMultiplayerMod/issues/5">#5</a>
        /// </summary>
        public static readonly IPEndPoint MulticastAddress = new IPEndPoint(IPAddress.Parse("239.255.254.26"), 1900);
        /// <summary>
        /// The IPv6 address on which local multiplayer servers broadcast their existence.
        /// TODO decide on a link-local multicast IPv6 address; see https://www.iana.org/assignments/ipv6-multicast-addresses/ipv6-multicast-addresses.xhtml#link-local
        /// TODO implement this as per https://github.com/FEZModding/FezMultiplayerMod/issues/5
        /// </summary>
        //public static readonly IPAddress MulticastAddressV6 = IPAddress.Parse("ff02::idk");

        /// <summary>
        /// </summary>
        public static readonly string ServerDiscoveryEntrySeparator = "=";
    }

    [Serializable]
    public class PlayerMetadata
    {
        public Guid Uuid;
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
        public void CopyValuesFrom(PlayerMetadata m)
        {
            this.CurrentLevelName = m.CurrentLevelName;
            this.Position = m.Position;
            this.Action = m.Action;
            this.AnimFrame = m.AnimFrame;
            this.LookingDirection = m.LookingDirection;
            this.LastUpdateTimestamp = m.LastUpdateTimestamp;
            this.CameraViewpoint = m.CameraViewpoint;
        }
    }
    [Serializable]
    public struct PlayerAppearance
    {
        public string PlayerName;
        public object CustomCharacterAppearance;

        public PlayerAppearance(string playerName, object appearance)
        {
            PlayerName = playerName;
            CustomCharacterAppearance = appearance;
        }
    }
    public enum TreasureType
    {
        Ao, Map, Trile
    }
    public enum TreasureSource
    {
        Forced, Chest, Map, Trile
    }
    public struct TreasureCollectionData
    {
        public readonly TreasureType Type;
        public readonly TreasureSource Source;
        public readonly string TreasureMapName;
        public readonly ActorType TreasureActorType;
        public readonly int? ArtObjectId;
        public readonly TrileEmplacement? TrileEmplacement;
        public readonly bool? TrileIsForeign;
        public TreasureCollectionData(TreasureType type,
                                      TreasureSource source,
                                      string treasureMapName,
                                      ActorType treasureActorType,
                                      int? artObjectId,
                                      TrileEmplacement? trileEmplacement,
                                      bool? trileIsForeign)
        {
            Type = type;
            Source = source;
            TreasureMapName = treasureMapName;
            TreasureActorType = treasureActorType;
            ArtObjectId = artObjectId;
            TrileEmplacement = trileEmplacement;
            TrileIsForeign = trileIsForeign;
        }
        public override string ToString()
        {
            return $"Type: {Type}\n"
                            + $"Source: {Source}\n"
                            + $"TreasureMapName: {TreasureMapName}\n"
                            + $"ActorType: {TreasureActorType}\n"
                            + $"ArtObjectId: {ArtObjectId}\n"
                            + $"TrileEmplacement: {TrileEmplacement}\n"
                            + $"TrileIsForeign: {TrileIsForeign}\n";
        }
    }
    [Serializable]
    public struct SaveDataUpdate
    {
        public int TODO;
        //TODO not yet implemented

        public SaveDataUpdate(int TODO)
        {
            this.TODO = TODO;
        }
    }
    [Serializable]
    public struct ActiveLevelState
    {
        public int TODO;
        //TODO not yet implemented

        public ActiveLevelState(int TODO)
        {
            this.TODO = TODO;
        }
    }

    public static class FezMultiplayerBinaryIOExtensions
    {
        public static readonly int MaxPlayerNameLength = 32;
        public static readonly int MaxLevelNameLength = 256;

        /// <summary>
        ///     Reads a string from the given <see cref="BinaryNetworkReader"/> as a byte array with an explicit length,
        ///     throwing an <see cref="ArgumentOutOfRangeException"/> if the string length is larger than <paramref name="maxLength"/>.
        ///     <br /> 
        ///     Note: do NOT use <see cref="BinaryNetworkReader.ReadString()"/> for network data, as the string length can be maliciously manipulated to hog network traffic.
        ///     <br /> See <a href="https://cwe.mitre.org/data/definitions/130.html">CWE-130</a> and <a href="https://cwe.mitre.org/data/definitions/400.html">CWE-400</a>
        /// </summary>
        /// <param name="reader">
        ///     The <see cref="BinaryNetworkReader"/> from which to read the string.
        /// </param>
        /// <param name="maxLength">
        ///     The maximum allowable length for the string. Any length greater than this will result in an exception.
        /// </param>
        /// <returns>
        ///     A string read from the binary stream, decoded using UTF-8.
        /// </returns>
        /// <exception cref="InvalidDataException">
        ///     Thrown when the length of the string read (specified by the first 4 bytes) is outside the allowed range of 0 to <paramref name="maxLength"/>.
        ///     This exception is raised to prevent the application from processing excessively long data, which could lead to denial of service or allocate undue resources.
        /// </exception>
        /// <remarks>
        ///     See also: <seealso cref="WriteStringAsByteArrayWithLength"/>
        /// </remarks>
        public static string ReadStringAsByteArrayWithLength(this BinaryNetworkReader reader, int maxLength)
        {
            const int minLength = 0;
            int length = reader.ReadInt32();
            if (length > maxLength || length < 0)
            {
                throw new InvalidDataException($"The length {length} is outside the allowed range of {minLength} to {maxLength}.");
            }
            else
            {
                return Encoding.UTF8.GetString(reader.ReadBytes(length));
            }
        }
        /// <summary>
        ///     Writes the string to the writer as a byte array, preceded by the array length.
        /// </summary>
        /// <param name="writer">
        ///     The writer to write to
        /// </param>
        /// <param name="str">
        ///     The string to send as a byte array
        /// </param>
        /// <remarks>
        ///     See also: <seealso cref="ReadStringAsByteArrayWithLength"/>
        /// </remarks>
        public static void WriteStringAsByteArrayWithLength(this BinaryNetworkWriter writer, string str)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(str);
            writer.Write((Int32)bytes.Length);
            writer.Write(bytes);
        }

        public static Guid ReadGuid(this BinaryNetworkReader reader)
        {
            return new Guid(reader.ReadInt32(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
        }
        public static void Write(this BinaryNetworkWriter writer, Guid guid)
        {
            writer.Write(guid.ToByteArray());
        }

        public static PlayerMetadata ReadPlayerMetadata(this BinaryNetworkReader reader)
        {
            Guid uuid = reader.ReadGuid();
            string lvl = reader.ReadStringAsByteArrayWithLength(MaxLevelNameLength);
            Vector3 pos = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            Viewpoint vp = (Viewpoint)reader.ReadInt32();
            ActionType act = (ActionType)reader.ReadInt32();
            int frame = reader.ReadInt32();
            HorizontalDirection lookdir = (HorizontalDirection)reader.ReadInt32();
            long timestamp = reader.ReadInt64();
            return new PlayerMetadata(uuid, lvl, pos, vp, act, frame, lookdir, timestamp);
        }
        public static void Write(this BinaryNetworkWriter writer, PlayerMetadata playerMetadata)
        {
            writer.Write((Guid)playerMetadata.Uuid);
            writer.WriteStringAsByteArrayWithLength((String)playerMetadata.CurrentLevelName ?? "");
            writer.Write((Single)playerMetadata.Position.X);
            writer.Write((Single)playerMetadata.Position.Y);
            writer.Write((Single)playerMetadata.Position.Z);
            writer.Write((Int32)playerMetadata.CameraViewpoint);
            writer.Write((Int32)playerMetadata.Action);
            writer.Write((Int32)playerMetadata.AnimFrame);
            writer.Write((Int32)playerMetadata.LookingDirection);
            writer.Write((Int64)playerMetadata.LastUpdateTimestamp);
        }

        public static PlayerAppearance ReadPlayerAppearance(this BinaryNetworkReader reader)
        {
            string name = reader.ReadStringAsByteArrayWithLength(MaxPlayerNameLength);
            object appearance = null;//TODO appearance format TBD
            return new PlayerAppearance(name, appearance);
        }
        public static void Write(this BinaryNetworkWriter writer, PlayerAppearance playerAppearance)
        {
            writer.WriteStringAsByteArrayWithLength(playerAppearance.PlayerName);
            //writer.Write(playerAppearance.CustomCharacterAppearance);//TODO appearance format TBD
        }

        public static SaveDataUpdate ReadSaveDataUpdate(this BinaryNetworkReader reader)
        {
            //TODO not yet implemented
            throw new NotImplementedException();
        }
        public static void Write(this BinaryNetworkWriter writer, SaveDataUpdate saveDataUpdate)
        {
            //TODO not yet implemented
            throw new NotImplementedException();
        }

        public static ActiveLevelState ReadActiveLevelState(this BinaryNetworkReader reader)
        {
            //TODO not yet implemented
            throw new NotImplementedException();
        }
        public static void Write(this BinaryNetworkWriter writer, ActiveLevelState activeLevelState)
        {
            //TODO not yet implemented
            throw new NotImplementedException();
        }
    }

    public sealed class VersionMismatchException : Exception
    {
        public string ExpectedVersion { get; }
        public string ReceivedVersion { get; }

        public VersionMismatchException(string expectedVersion, string receivedVersion)
            : base($"Protocol version mismatch: Expected '{expectedVersion}', but received '{receivedVersion}'.")
        {
            ExpectedVersion = expectedVersion;
            ReceivedVersion = receivedVersion;
        }
    }
    public abstract class SharedNetcode<P> where P : PlayerMetadata
    {
        #region network packet stuff
        private const int MaxProtocolVersionLength = 32;
        public const string ProtocolSignature = "FezMultiplayer";// Do not change
        public static readonly string ProtocolVersion = "eightteen";//Update this ever time you change something that affect the packets

        public volatile string ErrorMessage = null;//Note: this gets updated in the listenerThread
        /// <summary>
        /// If not null, contains a fatal exception that was thrown on a child Thread
        /// </summary>
        public volatile Exception FatalException = null;

        public abstract ConcurrentDictionary<Guid, P> Players { get; }
        public ConcurrentDictionary<Guid, PlayerAppearance> PlayerAppearances = new ConcurrentDictionary<Guid, PlayerAppearance>();
        /// <summary>
        /// Note: only the Keys of this dictionary are used. Optimally, we'd use a concurrent hashset, but .NET doesn't have that.
        /// </summary>
        protected ConcurrentDictionary<Guid, bool> UnknownPlayerAppearanceGuids = new ConcurrentDictionary<Guid, bool>();

        public string GetPlayerName(Guid playerUuid)
        {
            if (PlayerAppearances.TryGetValue(playerUuid, out PlayerAppearance appearance))
            {
                _ = UnknownPlayerAppearanceGuids.TryRemove(playerUuid, out var _);
                return appearance.PlayerName;
            }
            else
            {
                UnknownPlayerAppearanceGuids.TryAdd(playerUuid, true);
                return "Unknown";
            }
        }

        protected static void ValidateProcotolAndVersion(string protocolSignature, string protocolVersion)
        {
            if (!ProtocolSignature.Equals(protocolSignature))
            {
                throw new InvalidDataException($"Invalid Protocol Signature: Expected '{ProtocolSignature}', but received '{protocolSignature}'.");
            }
            if (!ProtocolVersion.Equals(protocolVersion))
            {
                throw new VersionMismatchException(ProtocolVersion, protocolVersion);
            }
        }

        public struct MiscClientData
        {
            public PlayerMetadata Metadata;
            public bool Disconnecting;
            public ICollection<Guid> RequestedAppearances;

            public const int MaxRequestedAppearancesSize = 10;

            public MiscClientData(PlayerMetadata Metadata, bool Disconnecting, ICollection<Guid> RequestedAppearances)
            {
                this.Metadata = Metadata;
                this.Disconnecting = Disconnecting;
                this.RequestedAppearances = RequestedAppearances;
            }
        }
#if !FEZCLIENT
        /// <summary>Reads the data sent by the player/client</summary>
        /// <param name="reader">The BinaryNetworkReader to read data from</param>
        /// <param name="retval">The value to store the return values in</param>
        /// <remarks>
        ///     Note: The data written by this method should be written by <seealso cref="WriteClientGameTickPacket"/>
        /// </remarks>
        /// <returns>the amount of time, in ticks, it took to read the data to the network</returns>
        protected long ReadClientGameTickPacket(BinaryNetworkReader reader, ref MiscClientData retval, Guid playerUuid)
        {
            Stopwatch sw = new Stopwatch();
            string sig = reader.ReadStringAsByteArrayWithLength(ProtocolSignature.Length);
            string ver = reader.ReadStringAsByteArrayWithLength(MaxProtocolVersionLength);
            ValidateProcotolAndVersion(sig, ver);

            PlayerMetadata playerMetadata = reader.ReadPlayerMetadata();
            if (reader.ReadBoolean())
            {
                SaveDataUpdate saveDataUpdate = reader.ReadSaveDataUpdate();
                ProcessSaveDataUpdate(saveDataUpdate);
            }
            if (reader.ReadBoolean())
            {
                ActiveLevelState levelState = reader.ReadActiveLevelState();
                ProcessActiveLevelState(levelState);
            }
            if (reader.ReadBoolean())
            {
                PlayerAppearance appearance = reader.ReadPlayerAppearance();
                UpdatePlayerAppearance(playerUuid, appearance);
            }
            int requestPlayerAppearanceLength = reader.ReadInt32();
            retval.RequestedAppearances.Clear();
            for (int i = 0; i < requestPlayerAppearanceLength; ++i)
            {
                Guid guid = reader.ReadGuid();
                if (i < MiscClientData.MaxRequestedAppearancesSize)
                {
                    retval.RequestedAppearances.Add(guid);
                }
            }
            bool Disconnecting = reader.ReadBoolean();

            retval.Metadata = playerMetadata;
            retval.Disconnecting = Disconnecting;
            return sw.ElapsedTicks;
        }
#else
        /// <summary>
        ///     Writes the supplied player/client data to network stream that is connected to the multiplayer server represented by <paramref name="writer0"/>
        /// </summary>
        /// <remarks>
        ///     Note: This method has a lot of arguments so it is more easily identifiable if one of the arguments is unused.<br />
        ///     Note: The data written by this method should be read by <seealso cref="ReadClientGameTickPacket"/>
        /// </remarks>
        /// <returns>the amount of time, in ticks, it took to write the data to the network</returns>
        protected long WriteClientGameTickPacket(BinaryNetworkWriter writer0, PlayerMetadata playerMetadata, SaveDataUpdate? saveDataUpdate, ActiveLevelState? levelState,
                PlayerAppearance? appearance, ICollection<Guid> requestPlayerAppearance, bool Disconnecting)
        {
            Stopwatch sw = new Stopwatch();
            //optimize network writing so it doesn't send a bazillion packets for a single tick
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryNetworkWriter writer = new BinaryNetworkWriter(ms))
                {
                    writer.WriteStringAsByteArrayWithLength(ProtocolSignature);
                    writer.WriteStringAsByteArrayWithLength(ProtocolVersion);

                    writer.Write(playerMetadata);
                    writer.Write(saveDataUpdate.HasValue);
                    if (saveDataUpdate.HasValue)
                    {
                        writer.Write(saveDataUpdate.Value);
                    }
                    writer.Write(levelState.HasValue);
                    if (levelState.HasValue)
                    {
                        writer.Write(levelState.Value);
                    }
                    writer.Write(appearance.HasValue);
                    if (appearance.HasValue)
                    {
                        writer.Write(appearance.Value);
                    }
                    writer.Write((int)requestPlayerAppearance.Count);
                    foreach (Guid guid in requestPlayerAppearance)
                    {
                        writer.Write(guid);
                    }
                    writer.Write(Disconnecting);
                    writer.Flush();
                }
                byte[] data = ms.ToArray();
                sw.Start();
                writer0.Write(data);
                writer0.Flush();
                sw.Stop();
            }
            return sw.ElapsedTicks;
        }
#endif
#if FEZCLIENT
        /// <summary>Reads the data sent from the server</summary>
        /// <remarks>
        ///     Note: The data written by this method should be written by <seealso cref="WriteServerGameTickPacket"/>
        /// </remarks>
        /// <returns>the amount of time, in ticks, it took to read the data from the network</returns>
        protected long ReadServerGameTickPacket(BinaryNetworkReader reader, ref bool RetransmitAppearance, ref long newTimeOfDay_ticks)
        {
            Stopwatch sw = new Stopwatch();
            string sig = reader.ReadStringAsByteArrayWithLength(ProtocolSignature.Length);
            string ver = reader.ReadStringAsByteArrayWithLength(MaxProtocolVersionLength);
            ValidateProcotolAndVersion(sig, ver);

            int playerMetadataListLength = reader.ReadInt32();
            for (int i = 0; i < playerMetadataListLength; ++i)
            {
                PlayerMetadata playerMetadata = reader.ReadPlayerMetadata();
                //update the data in Players
                Players.AddOrUpdate(playerMetadata.Uuid, (P)playerMetadata, (guid, currentval) =>
                {
                    if (currentval.LastUpdateTimestamp < playerMetadata.LastUpdateTimestamp)
                    {
                        currentval.CopyValuesFrom(playerMetadata);
                    }

                    return currentval;
                });
            }
            if (reader.ReadBoolean())
            {
                SaveDataUpdate saveDataUpdate = reader.ReadSaveDataUpdate();
                ProcessSaveDataUpdate(saveDataUpdate);
            }
            int activeLevelStateListLength = reader.ReadInt32();
            for (int i = 0; i < activeLevelStateListLength; ++i)
            {
                ProcessActiveLevelState(reader.ReadActiveLevelState());
            }
            int disconnectedPlayersListLength = reader.ReadInt32();
            for (int i = 0; i < disconnectedPlayersListLength; ++i)
            {
                ProcessDisconnect(reader.ReadGuid());
            }
            int playerAppearanceListLength = reader.ReadInt32();
            for (int i = 0; i < playerAppearanceListLength; ++i)
            {
                UpdatePlayerAppearance(reader.ReadGuid(), reader.ReadPlayerAppearance());
            }
            if (reader.ReadBoolean())
            {
                Guid NewClientGuid = reader.ReadGuid();
                ProcessNewClientGuid(NewClientGuid);
            }
            RetransmitAppearance = reader.ReadBoolean();
            SetTimeOfDay(reader.ReadInt64());
            return sw.ElapsedTicks;
        }
#else
        /// <summary>Writes the supplied server data to client's network stream <paramref name="writer0"/></summary>
        /// <remarks>
        ///     Note: This method has a lot of arguments so it is more easily identifiable if one of the arguments is unused.<br />
        ///     Note: The data written by this method should be read by <seealso cref="ReadServerGameTickPacket"/>
        /// </remarks>
        /// <returns>the amount of time, in ticks, it took to write the data to the network</returns>
        protected long WriteServerGameTickPacket(BinaryNetworkWriter writer0, List<PlayerMetadata> playerMetadatas, SaveDataUpdate? saveDataUpdate, ICollection<ActiveLevelState> levelStates,
                                                            ICollection<Guid> disconnectedPlayers, IDictionary<Guid, PlayerAppearance> appearances, Guid? NewClientGuid,
                                                            bool RequestAppearance, FezMultiplayerDedicatedServer.SaveData sharedSaveData, TimeSpan timeOfDay)
        {
            Stopwatch sw = new Stopwatch();
            int datalength;
            //optimize network writing so it doesn't send a bazillion packets for a single tick
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryNetworkWriter writer = new BinaryNetworkWriter(ms))
                {
                    writer.WriteStringAsByteArrayWithLength(ProtocolSignature);
                    writer.WriteStringAsByteArrayWithLength(ProtocolVersion);

                    writer.Write((int)playerMetadatas.Count);
                    foreach (PlayerMetadata playerMetadata in playerMetadatas)
                    {
                        writer.Write(playerMetadata);
                    }
                    writer.Write(saveDataUpdate.HasValue);
                    if (saveDataUpdate.HasValue)
                    {
                        writer.Write(saveDataUpdate.Value);
                    }
                    writer.Write((int)levelStates.Count);
                    foreach (ActiveLevelState levelState in levelStates)
                    {
                        writer.Write(levelState);
                    }
                    writer.Write((int)disconnectedPlayers.Count);
                    foreach (Guid disconnectedPlayer in disconnectedPlayers)
                    {
                        writer.Write(disconnectedPlayer);
                    }
                    writer.Write((int)appearances.Count);
                    foreach (KeyValuePair<Guid, PlayerAppearance> appearance in appearances)
                    {
                        writer.Write(appearance.Key);
                        writer.Write(appearance.Value);
                    }
                    writer.Write(NewClientGuid.HasValue);
                    if (NewClientGuid.HasValue)
                    {
                        writer.Write(NewClientGuid.Value);
                    }
                    writer.Write(RequestAppearance);
                    writer.Write(timeOfDay.Ticks);
                    writer.Flush();
                }
                byte[] data = ms.ToArray();
                datalength = data.Length;
                sw.Start();
                writer0.Write(data);
                writer0.Flush();
                sw.Stop();
            }
            return sw.ElapsedTicks / datalength;
        }
#endif
        protected void UpdatePlayerAppearance(Guid puid, PlayerAppearance newAp)
        {
            _ = PlayerAppearances.AddOrUpdate(puid, (u) => newAp, (u, a) => newAp);
        }
        protected abstract void ProcessDisconnect(Guid puid);
        protected abstract void ProcessSaveDataUpdate(SaveDataUpdate saveDataUpdate);
        protected abstract void ProcessActiveLevelState(ActiveLevelState activeLevelState);
#if FEZCLIENT
        protected abstract void ProcessNewClientGuid(Guid puid);
        protected abstract void SetTimeOfDay(long newTimeOfDayTicks);
#endif
#endregion
    }


    public sealed class BinaryNetworkWriter : BinaryWriter
    {
        public BinaryNetworkWriter(Stream output) : base(output)
        {
        }

        public override void Write(short value)
        {
            base.Write(IPAddress.HostToNetworkOrder(value));
        }

        public override void Write(ushort value)
        {
            base.Write((ushort)IPAddress.HostToNetworkOrder((short)value));
        }

        public override void Write(int value)
        {
            base.Write(IPAddress.HostToNetworkOrder(value));
        }

        public override void Write(uint value)
        {
            base.Write((uint)IPAddress.HostToNetworkOrder((int)value));
        }

        public override void Write(long value)
        {
            base.Write(IPAddress.HostToNetworkOrder(value));
        }

        public override void Write(ulong value)
        {
            base.Write((ulong)IPAddress.HostToNetworkOrder((long)value));
        }

        public override void Write(float value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            base.Write(bytes);
        }

        public override void Write(double value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            base.Write(bytes);
        }
    }

    public sealed class BinaryNetworkReader : BinaryReader
    {
        public BinaryNetworkReader(Stream input) : base(input)
        {
        }

        public override short ReadInt16()
        {
            return IPAddress.NetworkToHostOrder(base.ReadInt16());
        }

        public override ushort ReadUInt16()
        {
            return (ushort)IPAddress.NetworkToHostOrder(base.ReadInt16());
        }

        public override int ReadInt32()
        {
            return IPAddress.NetworkToHostOrder(base.ReadInt32());
        }

        public override uint ReadUInt32()
        {
            return (uint)IPAddress.NetworkToHostOrder(base.ReadInt32());
        }

        public override long ReadInt64()
        {
            return IPAddress.NetworkToHostOrder(base.ReadInt64());
        }

        public override ulong ReadUInt64()
        {
            return (ulong)IPAddress.NetworkToHostOrder(base.ReadInt64());
        }

        public override float ReadSingle()
        {
            var bytes = base.ReadBytes(sizeof(float));
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToSingle(bytes, 0);
        }

        public override double ReadDouble()
        {
            var bytes = base.ReadBytes(sizeof(double));
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToDouble(bytes, 0);
        }
    }
}
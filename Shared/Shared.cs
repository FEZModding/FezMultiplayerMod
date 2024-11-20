

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;

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
namespace FezSharedTools
{
    class SharedConstants
    {
        public static readonly int DefaultPort = 7777;
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
    public class SharedSaveData
    {
        //TODO not yet implemented
    }

    public static class FezMultiplayerBinaryIOExtensions
    {
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

        public static readonly int MaxPlayerNameLength = 32;
        public static readonly int MaxLevelNameLength = 256;

        /// <summary>
        /// Reads a string from the given <see cref="BinaryNetworkReader"/> as a byte array with an explicit length,
        /// throwing an <see cref="ArgumentOutOfRangeException"/> if the string length is larger than <paramref name="maxLength"/>.
        /// <br /> 
        /// Note: do NOT use <see cref="BinaryNetworkReader.ReadString()"/> for network data, as the string length can be maliciously manipulated to hog network traffic.
        /// <br /> See <a href="https://cwe.mitre.org/data/definitions/130.html">CWE-130</a> and <a href="https://cwe.mitre.org/data/definitions/400.html">CWE-400</a>
        /// </summary>
        /// <param name="reader">The <see cref="BinaryNetworkReader"/> from which to read the string.</param>
        /// <param name="maxLength">The maximum allowable length for the string. Any length greater than this will result in an exception.</param>
        /// <returns>A string read from the binary stream, decoded using UTF-8.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of the string read (specified by the first 4 bytes) is outside the allowed range of 0 to <paramref name="maxLength"/>.
        /// This exception is raised to prevent the application from processing excessively long data, which could lead to denial of service or allocate undue resources.
        /// </exception>
        public static string ReadStringAsByteArrayWithLength(this BinaryNetworkReader reader, int maxLength)
        {
            const int minLength = 0;
            int length = reader.ReadInt32();
            if (length > maxLength || length < 0)
            {
                throw new ArgumentOutOfRangeException($"The length {length} is outside the allowed range of {minLength} to {maxLength}.");
            }
            else
            {
                return Encoding.UTF8.GetString(reader.ReadBytes(length));
            }
        }
        /// <summary>
        /// Writes the string to the writer as a byte array, preceded by the array length.
        /// </summary>
        /// <param name="writer">The writer to write to</param>
        /// <param name="str">The string to send as a byte array</param>
        /// <remarks>
        /// See also: <seealso cref="ReadStringAsByteArrayWithLength"/>
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

    public class VersionMismatchException : Exception
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
        protected const string ProtocolSignature = "FezMultiplayer";// Do not change
        public const string ProtocolVersion = "seventeen";//Update this ever time you change something that affect the packets

        public volatile string ErrorMessage = null;//Note: this gets updated in the listenerThread
        /// <summary>
        /// If not null, contains a fatal exception that was thrown on a child Thread
        /// </summary>
        public volatile Exception FatalException = null;

        public abstract ConcurrentDictionary<Guid, P> Players { get; }
        protected ConcurrentDictionary<Guid, PlayerAppearance> PlayerAppearances = new ConcurrentDictionary<Guid, PlayerAppearance>();
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

        public struct MiscClientData {
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
        /// <summary>
        /// 
        /// </summary>
        /// <param name="reader">The BinaryNetworkReader to read data from</param>
        /// <param name="retval">The value to store the return values in</param>
        /// <returns>PlayerMetadata, true if the client is going to disconnect</returns>
        protected void ReadClientGameTickPacket(BinaryNetworkReader reader, ref MiscClientData retval, Guid playerUuid)
        {
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
                if(i < MiscClientData.MaxRequestedAppearancesSize)
                {
                    retval.RequestedAppearances.Add(guid);
                }
            }
            bool Disconnecting = reader.ReadBoolean();

            retval.Metadata = playerMetadata;
            retval.Disconnecting = Disconnecting;
        }
        protected void WriteClientGameTickPacket(BinaryNetworkWriter writer0, PlayerMetadata playerMetadata, SaveDataUpdate? saveDataUpdate, ActiveLevelState? levelState,
                PlayerAppearance? appearance, ICollection<Guid> requestPlayerAppearance, bool Disconnecting)
        {
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
                writer0.Write(ms.ToArray());
            }
        }
        protected void ReadServerGameTickPacket(BinaryNetworkReader reader, ref bool RetransmitAppearance)
        {
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
        }
        protected void WriteServerGameTickPacket(BinaryNetworkWriter writer0, List<PlayerMetadata> playerMetadatas, SaveDataUpdate? saveDataUpdate, ICollection<ActiveLevelState> levelStates,
                                                            ICollection<Guid> disconnectedPlayers, IDictionary<Guid, PlayerAppearance> appearances, Guid? NewClientGuid,
                                                            bool RequestAppearance, SharedSaveData sharedSaveData)
        {
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
                    writer.Flush();
                }
                writer0.Write(ms.ToArray());
            }
        }
        protected void UpdatePlayerAppearance(Guid puid, PlayerAppearance newAp)
        {
            _ = PlayerAppearances.AddOrUpdate(puid, (u) => newAp, (u, a) => newAp);
        }
        protected void UpdatePlayerAppearance(Guid puid, string pname, object appearance)
        {
            PlayerAppearance newAp = new PlayerAppearance(pname, appearance);
            _ = PlayerAppearances.AddOrUpdate(puid, (u) => newAp, (u, a) => newAp);
        }
        protected abstract void ProcessDisconnect(Guid puid);
        protected abstract void ProcessSaveDataUpdate(SaveDataUpdate saveDataUpdate);
        protected virtual void ProcessNewClientGuid(Guid puid) { }
        protected abstract void ProcessActiveLevelState(ActiveLevelState activeLevelState);
        #endregion
    }


    public class BinaryNetworkWriter : BinaryWriter
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

    public class BinaryNetworkReader : BinaryReader
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
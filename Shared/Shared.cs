

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
        //TODO

        public SaveDataUpdate(int TODO)
        {
            this.TODO = TODO;
        }
    }
    [Serializable]
    public struct ActiveLevelState
    {
        public int TODO;
        //TODO

        public ActiveLevelState(int TODO)
        {
            this.TODO = TODO;
        }
    }
    public static class FezMultiplayerBinaryIOExtensions
    {
        private static readonly MethodInfo read7BitEncodedIntMethod;
        static FezMultiplayerBinaryIOExtensions(){
            //Read7BitEncodedInt is marked as protected internal on Framework 4.0
            read7BitEncodedIntMethod = typeof(BinaryReader).GetMethod(
                    "Read7BitEncodedInt",
                    BindingFlags.Instance | BindingFlags.NonPublic); // Get the protected internal method

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

        public static readonly int maxplayernamelength = 32;

        public static string ReadStringWithLengthLimit(this BinaryReader reader, int maxLength)
        {
            const int minLength = 0;
            int length = (int)read7BitEncodedIntMethod.Invoke(reader, null);//note Read7BitEncodedInt is marked as protected internal on Framework 4.0
            if (length > maxLength || length < 0)
            {
                throw new ArgumentOutOfRangeException($"The length {length} is outside the allowed range of {minLength} to {maxLength}.");
            }
            else
            {
                return new String(reader.ReadChars(length));
            }
        }

        public static Guid ReadGuid(this BinaryReader reader)
        {
            return new Guid(reader.ReadInt32(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
        }
        public static void Write(this BinaryWriter writer, Guid guid)
        {
            writer.Write(guid.ToByteArray());
        }

        public static PlayerMetadata ReadPlayerMetadata(this BinaryReader reader)
        {
            Guid uuid = reader.ReadGuid();
            string lvl = reader.ReadString();
            Vector3 pos = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            Viewpoint vp = (Viewpoint)reader.ReadInt32();
            ActionType act = (ActionType)reader.ReadInt32();
            int frame = reader.ReadInt32();
            HorizontalDirection lookdir = (HorizontalDirection)reader.ReadInt32();
            long timestamp = reader.ReadInt64();
            return new PlayerMetadata(uuid, lvl, pos, vp, act, frame, lookdir, timestamp);
        }
        public static void Write(this BinaryWriter writer, PlayerMetadata playerMetadata)
        {
            writer.Write((Guid)playerMetadata.Uuid);
            writer.Write((String)playerMetadata.CurrentLevelName ?? "");
            writer.Write((Single)playerMetadata.Position.X);
            writer.Write((Single)playerMetadata.Position.Y);
            writer.Write((Single)playerMetadata.Position.Z);
            writer.Write((Int32)playerMetadata.CameraViewpoint);
            writer.Write((Int32)playerMetadata.Action);
            writer.Write((Int32)playerMetadata.AnimFrame);
            writer.Write((Int32)playerMetadata.LookingDirection);
            writer.Write((Int64)playerMetadata.LastUpdateTimestamp);
        }

        public static PlayerAppearance ReadPlayerAppearance(this BinaryReader reader)
        {
            //TODO
            throw new NotImplementedException();
        }
        public static void Write(this BinaryWriter writer, PlayerAppearance playerAppearance)
        {
            //TODO
            throw new NotImplementedException();
        }

        public static SaveDataUpdate ReadSaveDataUpdate(this BinaryReader reader)
        {
            //TODO
            throw new NotImplementedException();
        }
        public static void Write(this BinaryWriter writer, SaveDataUpdate saveDataUpdate)
        {
            //TODO
            throw new NotImplementedException();
        }

        public static ActiveLevelState ReadActiveLevelState(this BinaryReader reader)
        {
            //TODO
            throw new NotImplementedException();
        }
        public static void Write(this BinaryWriter writer, ActiveLevelState activeLevelState)
        {
            //TODO
            throw new NotImplementedException();
        }
    }

    public static class PlayerMetadataExtensions
    {
        public static string GetPlayerName(this PlayerMetadata p)
        {
            return SharedNetcode<PlayerMetadata>.PlayerAppearances[p.Uuid].PlayerName;
        }
    }
    public abstract class SharedNetcode<P> where P : PlayerMetadata
    {
        #region network packet stuff
        protected const string ProtocolSignature = "FezMultiplayer";// Do not change
        public const string ProtocolVersion = "quince";//Update this ever time you change something that affect the packets

        public volatile string ErrorMessage = null;//Note: this gets updated in the listenerThread
        /// <summary>
        /// If not null, contains a fatal exception that was thrown on a child Thread
        /// </summary>
        public volatile Exception FatalException = null;

        public abstract ConcurrentDictionary<Guid, P> Players { get; }
        public static ConcurrentDictionary<Guid, PlayerAppearance> PlayerAppearances = new ConcurrentDictionary<Guid, PlayerAppearance>();

        protected void ReadClientGameTickPacket(BinaryReader reader){
            PlayerMetadata playerMetadata = reader.ReadPlayerMetadata();
            if(reader.ReadBoolean()){
                SaveDataUpdate saveDataUpdate = reader.ReadSaveDataUpdate();
                ProcessSaveDataUpdate(saveDataUpdate);
            }
            if (reader.ReadBoolean()){
                ActiveLevelState levelState = reader.ReadActiveLevelState();
                //TODO do something with read data
            }
            if (reader.ReadBoolean()){
                PlayerAppearance appearance = reader.ReadPlayerAppearance();
                UpdatePlayerAppearance(playerMetadata.Uuid, appearance.PlayerName, appearance.CustomCharacterAppearance);
            }
        }
        //TODO use this in MultiplayerClientNetcode.cs
        protected void WriteClientGameTickPacket(BinaryWriter writer, PlayerMetadata playerMetadata, SaveDataUpdate? saveDataUpdate, ActiveLevelState? levelState, PlayerAppearance? appearance){
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
        }

        //TODO use this in MultiplayerClientNetcode.cs
        protected void ReadServerGameTickPacket(BinaryReader reader){
            int playerMetadataListLength = reader.ReadInt32();
            for (int i = 0; i < playerMetadataListLength; ++i)
            {
                PlayerMetadata playerMetadata = reader.ReadPlayerMetadata();
                //TODO update the data in Players
                if(Players.TryGetValue(playerMetadata.Uuid, out P val)){
                    if (val.LastUpdateTimestamp < playerMetadata.LastUpdateTimestamp)
                        Players.AddOrUpdate(playerMetadata.Uuid, (P)playerMetadata, (guid, currentval) => {
                            foreach(var f in typeof(PlayerMetadata).GetFields()){//TODO probably a better way to do this
                                f.SetValue(currentval, f.GetValue(playerMetadata));
                            }
                            return currentval;
                        });
                }
            }
            if(reader.ReadBoolean()){
                SaveDataUpdate saveDataUpdate = reader.ReadSaveDataUpdate();
                ProcessSaveDataUpdate(saveDataUpdate);
            }
            int activeLevelStateListLength = reader.ReadInt32();
            List<ActiveLevelState> activeLevelStates = new List<ActiveLevelState>(activeLevelStateListLength);
            for (int i = 0; i < activeLevelStateListLength; ++i)
            {
                activeLevelStates.Add(reader.ReadActiveLevelState());
                //TODO do something with read data
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
        }
        protected void WriteServerGameTickPacket(BinaryWriter writer, List<PlayerMetadata> playerMetadatas, SaveDataUpdate? saveDataUpdate, List<ActiveLevelState> levelStates,
                                                            List<Guid> disconnectedPlayers, Dictionary<Guid, PlayerAppearance> appearances, object sharedSaveData) {
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
        #endregion
    }
}


using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Diagnostics;

#if FEZCLIENT
using ActionType = FezGame.Structure.ActionType;
using ActorType = FezEngine.Structure.ActorType;
using TrileEmplacement = FezEngine.Structure.TrileEmplacement;
using HorizontalDirection = FezEngine.HorizontalDirection;
using Viewpoint = FezEngine.Viewpoint;
using Vector3 = Microsoft.Xna.Framework.Vector3;
using SaveData = FezGame.Structure.SaveData;
using LevelSaveData = FezGame.Structure.LevelSaveData;
using WinConditions = FezEngine.Structure.WinConditions;
#else
using ActionType = FezMultiplayerDedicatedServer.ActionType;
using ActorType = FezMultiplayerDedicatedServer.ActorType;
using HorizontalDirection = FezMultiplayerDedicatedServer.HorizontalDirection;
using Viewpoint = FezMultiplayerDedicatedServer.Viewpoint;
using Vector3 = FezMultiplayerDedicatedServer.Vector3;
using TrileEmplacement = FezMultiplayerDedicatedServer.TrileEmplacement;
using SaveData = FezMultiplayerDedicatedServer.SaveData;
using LevelSaveData = FezMultiplayerDedicatedServer.LevelSaveData;
using WinConditions = FezMultiplayerDedicatedServer.WinConditions;
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
        public static readonly Encoding UTF8 = Encoding.UTF8;

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

        internal const bool TODO_Debug_EnableLevelStateSync = false;//TODO remove this once level states get synced
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
                return SharedConstants.UTF8.GetString(reader.ReadBytes(length));
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
            byte[] bytes = SharedConstants.UTF8.GetBytes(str);
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

        internal static void ReadAndProcessSaveDataUpdate(this BinaryNetworkReader reader, bool SyncWorldState)
        {
            int len = reader.ReadInt32();
            byte[] data = reader.ReadBytes(len);
            if (SyncWorldState)
            {
                SaveDataChanges.DeserializeAndProcess(data);
            }
        }
        internal static void Write(this BinaryNetworkWriter writer, SaveDataChanges saveDataUpdate)
        {
            byte[] bytes = saveDataUpdate.Serialize();
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        public static ActiveLevelState ReadActiveLevelState(this BinaryNetworkReader reader)
        {
            //TODO not yet implemented
            if (SharedConstants.TODO_Debug_EnableLevelStateSync)
            {
                System.Diagnostics.Debugger.Launch();
                System.Diagnostics.Debugger.Break();
            }
            return new ActiveLevelState();
        }
        public static void Write(this BinaryNetworkWriter writer, ActiveLevelState activeLevelState)
        {
            //TODO not yet implemented
            if (SharedConstants.TODO_Debug_EnableLevelStateSync)
            {
                System.Diagnostics.Debugger.Launch();
                System.Diagnostics.Debugger.Break();
            }
        }
        public static SaveData ReadSharedSaveData(this BinaryReader r)
        {
            //Note: this method reads save data from the network, but does not process it
            SaveData saveData = new SaveData();
            long num = r.ReadInt64();
            if (num != 6)
            {
                throw new IOException("Invalid version : " + num + " (expected " + 6L + ")");
            }
            saveData.CreationTime = r.ReadInt64();
            saveData.Finished32 = r.ReadBoolean();
            saveData.Finished64 = r.ReadBoolean();
#if FEZCLIENT
            saveData.HasFPView = 
#endif
            r.ReadBoolean();
#if FEZCLIENT
            saveData.HasStereo3D = 
#endif
            r.ReadBoolean();
            saveData.CanNewGamePlus = r.ReadBoolean();
            saveData.IsNewGamePlus = r.ReadBoolean();
            saveData.OneTimeTutorials.Clear();
            int num2;
            saveData.OneTimeTutorials = new Dictionary<string, bool>(num2 = r.ReadInt32());
            for (int i = 0; i < num2; i++)
            {
                saveData.OneTimeTutorials.Add(r.ReadNullableString(), r.ReadBoolean());
            }
            saveData.Level = r.ReadNullableString();
            saveData.View = (Viewpoint)r.ReadInt32();
            saveData.Ground = r.ReadVector3();
            saveData.TimeOfDay = new TimeSpan(r.ReadInt64());
            saveData.UnlockedWarpDestinations = new List<string>(num2 = r.ReadInt32());
            for (int j = 0; j < num2; j++)
            {
                saveData.UnlockedWarpDestinations.Add(r.ReadNullableString());
            }
            saveData.Keys = r.ReadInt32();
            saveData.CubeShards = r.ReadInt32();
            saveData.SecretCubes = r.ReadInt32();
            saveData.CollectedParts = r.ReadInt32();
            saveData.CollectedOwls = r.ReadInt32();
            saveData.PiecesOfHeart = r.ReadInt32();
            if (saveData.SecretCubes > 32 || saveData.CubeShards > 32 || saveData.PiecesOfHeart > 3)
            {
                saveData.ScoreDirty = true;
            }
            saveData.SecretCubes = Math.Min(saveData.SecretCubes, 32);
            saveData.CubeShards = Math.Min(saveData.CubeShards, 32);
            saveData.PiecesOfHeart = Math.Min(saveData.PiecesOfHeart, 3);
            saveData.Maps = new List<string>(num2 = r.ReadInt32());
            for (int k = 0; k < num2; k++)
            {
                saveData.Maps.Add(r.ReadNullableString());
            }
            saveData.Artifacts = new List<ActorType>(num2 = r.ReadInt32());
            for (int l = 0; l < num2; l++)
            {
                saveData.Artifacts.Add((ActorType)r.ReadInt32());
            }
            saveData.EarnedAchievements = new List<string>(num2 = r.ReadInt32());
            for (int m = 0; m < num2; m++)
            {
                saveData.EarnedAchievements.Add(r.ReadNullableString());
            }
            saveData.EarnedGamerPictures = new List<string>(num2 = r.ReadInt32());
            for (int n = 0; n < num2; n++)
            {
                saveData.EarnedGamerPictures.Add(r.ReadNullableString());
            }
            saveData.ScriptingState = r.ReadNullableString();
            saveData.FezHidden = r.ReadBoolean();
            saveData.GlobalWaterLevelModifier = r.ReadNullableSingle();
            saveData.HasHadMapHelp = r.ReadBoolean();
#if FEZCLIENT
            saveData.CanOpenMap = 
#endif
            r.ReadBoolean();
            saveData.AchievementCheatCodeDone = r.ReadBoolean();
            saveData.AnyCodeDeciphered = r.ReadBoolean();
            saveData.MapCheatCodeDone = r.ReadBoolean();
            saveData.World = new Dictionary<string, LevelSaveData>(num2 = r.ReadInt32());
            for (int num3 = 0; num3 < num2; num3++)
            {
                try
                {
                    saveData.World.Add(r.ReadNullableString(), ReadLevel(r));
                }
                catch (Exception)
                {
                    break;
                }
            }
            r.ReadBoolean();
            saveData.ScoreDirty = true;
            saveData.HasDoneHeartReboot = r.ReadBoolean();
            saveData.PlayTime = r.ReadInt64();
#if FEZCLIENT
            saveData.IsNew = string.IsNullOrEmpty(saveData.Level) || saveData.CanNewGamePlus || saveData.World.Count == 0;
            saveData.HasFPView |= saveData.HasStereo3D;
#endif
            return saveData;
        }
        private static LevelSaveData ReadLevel(BinaryReader r)
        {
            LevelSaveData levelSaveData = new LevelSaveData();
            int num;
            levelSaveData.DestroyedTriles = new List<TrileEmplacement>(num = r.ReadInt32());
            for (int i = 0; i < num; i++)
            {
                levelSaveData.DestroyedTriles.Add(r.ReadTrileEmplacement());
            }
            levelSaveData.InactiveTriles = new List<TrileEmplacement>(num = r.ReadInt32());
            for (int j = 0; j < num; j++)
            {
                levelSaveData.InactiveTriles.Add(r.ReadTrileEmplacement());
            }
            levelSaveData.InactiveArtObjects = new List<int>(num = r.ReadInt32());
            for (int k = 0; k < num; k++)
            {
                levelSaveData.InactiveArtObjects.Add(r.ReadInt32());
            }
            levelSaveData.InactiveEvents = new List<int>(num = r.ReadInt32());
            for (int l = 0; l < num; l++)
            {
                levelSaveData.InactiveEvents.Add(r.ReadInt32());
            }
            levelSaveData.InactiveGroups = new List<int>(num = r.ReadInt32());
            for (int m = 0; m < num; m++)
            {
                levelSaveData.InactiveGroups.Add(r.ReadInt32());
            }
            levelSaveData.InactiveVolumes = new List<int>(num = r.ReadInt32());
            for (int n = 0; n < num; n++)
            {
                levelSaveData.InactiveVolumes.Add(r.ReadInt32());
            }
            levelSaveData.InactiveNPCs = new List<int>(num = r.ReadInt32());
            for (int num2 = 0; num2 < num; num2++)
            {
                levelSaveData.InactiveNPCs.Add(r.ReadInt32());
            }
            levelSaveData.PivotRotations = new Dictionary<int, int>(num = r.ReadInt32());
            for (int num3 = 0; num3 < num; num3++)
            {
                levelSaveData.PivotRotations.Add(r.ReadInt32(), r.ReadInt32());
            }
            levelSaveData.LastStableLiquidHeight = r.ReadNullableSingle();
            levelSaveData.ScriptingState = r.ReadNullableString();
            levelSaveData.FirstVisit = r.ReadBoolean();
            levelSaveData.FilledConditions = ReadWonditions(r);
            return levelSaveData;
        }
        private static WinConditions ReadWonditions(BinaryReader r)
        {
#pragma warning disable IDE0017 // Simplify object initialization
            WinConditions winConditions = new WinConditions();
#pragma warning restore IDE0017 // Simplify object initialization
            winConditions.LockedDoorCount = r.ReadInt32();
            winConditions.UnlockedDoorCount = r.ReadInt32();
            winConditions.ChestCount = r.ReadInt32();
            winConditions.CubeShardCount = r.ReadInt32();
            winConditions.OtherCollectibleCount = r.ReadInt32();
            winConditions.SplitUpCount = r.ReadInt32();
            int num;
            winConditions.ScriptIds = new List<int>(num = r.ReadInt32());
            for (int i = 0; i < num; i++)
            {
                winConditions.ScriptIds.Add(r.ReadInt32());
            }
            winConditions.SecretCount = r.ReadInt32();
            return winConditions;
        }
#if !FEZCLIENT
        public static void Write(this BinaryNetworkWriter w, SaveData sd)
        {
            w.Write(6L);
            w.Write(sd.CreationTime);
            w.Write(sd.Finished32);
            w.Write(sd.Finished64);
            w.Write(sd.HasFPView);
            w.Write(sd.HasStereo3D);
            w.Write(sd.CanNewGamePlus);
            w.Write(sd.IsNewGamePlus);
            w.Write(sd.OneTimeTutorials.Count);
            foreach (KeyValuePair<string, bool> oneTimeTutorial in sd.OneTimeTutorials)
            {
                w.WriteObject(oneTimeTutorial.Key);
                w.Write(oneTimeTutorial.Value);
            }
            w.WriteObject(sd.Level);
            w.Write((int)sd.View);
            w.Write(sd.Ground);
            w.Write(sd.TimeOfDay.Ticks);
            w.Write(sd.UnlockedWarpDestinations.Count);
            foreach (string unlockedWarpDestination in sd.UnlockedWarpDestinations)
            {
                w.WriteObject(unlockedWarpDestination);
            }
            w.Write(sd.Keys);
            w.Write(sd.CubeShards);
            w.Write(sd.SecretCubes);
            w.Write(sd.CollectedParts);
            w.Write(sd.CollectedOwls);
            w.Write(sd.PiecesOfHeart);
            w.Write(sd.Maps.Count);
            foreach (string map in sd.Maps)
            {
                w.WriteObject(map);
            }
            w.Write(sd.Artifacts.Count);
            foreach (ActorType artifact in sd.Artifacts)
            {
                w.Write((int)artifact);
            }
            w.Write(sd.EarnedAchievements.Count);
            foreach (string earnedAchievement in sd.EarnedAchievements)
            {
                w.WriteObject(earnedAchievement);
            }
            w.Write(sd.EarnedGamerPictures.Count);
            foreach (string earnedGamerPicture in sd.EarnedGamerPictures)
            {
                w.WriteObject(earnedGamerPicture);
            }
            w.WriteObject(sd.ScriptingState);
            w.Write(sd.FezHidden);
            w.WriteObject(sd.GlobalWaterLevelModifier);
            w.Write(sd.HasHadMapHelp);
            w.Write(sd.CanOpenMap);
            w.Write(sd.AchievementCheatCodeDone);
            w.Write(sd.AnyCodeDeciphered);
            w.Write(sd.MapCheatCodeDone);
            w.Write(sd.World.Count);
            foreach (KeyValuePair<string, LevelSaveData> item in sd.World)
            {
                w.WriteObject(item.Key);
                WriteLevelSaveData(w, item.Value);
            }
            w.Write(sd.ScoreDirty);
            w.Write(sd.HasDoneHeartReboot);
            w.Write(sd.PlayTime);
            //w.Write(sd.IsNew);//this flag gets written to the end of normal save files in the game, but it is never read
        }
        private static void WriteLevelSaveData(BinaryNetworkWriter w, LevelSaveData lsd)
        {
            w.Write(lsd.DestroyedTriles.Count);
            foreach (TrileEmplacement destroyedTrile in lsd.DestroyedTriles)
            {
                w.Write(destroyedTrile);
            }
            w.Write(lsd.InactiveTriles.Count);
            foreach (TrileEmplacement inactiveTrile in lsd.InactiveTriles)
            {
                w.Write(inactiveTrile);
            }
            w.Write(lsd.InactiveArtObjects.Count);
            foreach (int inactiveArtObject in lsd.InactiveArtObjects)
            {
                w.Write(inactiveArtObject);
            }
            w.Write(lsd.InactiveEvents.Count);
            foreach (int inactiveEvent in lsd.InactiveEvents)
            {
                w.Write(inactiveEvent);
            }
            w.Write(lsd.InactiveGroups.Count);
            foreach (int inactiveGroup in lsd.InactiveGroups)
            {
                w.Write(inactiveGroup);
            }
            w.Write(lsd.InactiveVolumes.Count);
            foreach (int inactiveVolume in lsd.InactiveVolumes)
            {
                w.Write(inactiveVolume);
            }
            w.Write(lsd.InactiveNPCs.Count);
            foreach (int inactiveNPC in lsd.InactiveNPCs)
            {
                w.Write(inactiveNPC);
            }
            w.Write(lsd.PivotRotations.Count);
            foreach (KeyValuePair<int, int> pivotRotation in lsd.PivotRotations)
            {
                w.Write(pivotRotation.Key);
                w.Write(pivotRotation.Value);
            }
            w.WriteObject(lsd.LastStableLiquidHeight);
            w.WriteObject(lsd.ScriptingState);
            w.Write(lsd.FirstVisit);
            WriteWonditions(w, lsd.FilledConditions);
        }
        private static void WriteWonditions(BinaryNetworkWriter w, WinConditions wc)
        {
            w.Write(wc.LockedDoorCount);
            w.Write(wc.UnlockedDoorCount);
            w.Write(wc.ChestCount);
            w.Write(wc.CubeShardCount);
            w.Write(wc.OtherCollectibleCount);
            w.Write(wc.SplitUpCount);
            w.Write(wc.ScriptIds.Count);
            foreach (int scriptId in wc.ScriptIds)
            {
                w.Write(scriptId);
            }
            w.Write(wc.SecretCount);
        }
#endif
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
        public static readonly string ProtocolVersion = "nineteen-wip4";//Update this ever time you change something that affect the packets

        public volatile string ErrorMessage = null;//Note: this gets updated in the listenerThread
        /// <summary>
        /// If not null, contains a fatal exception that was thrown on a child Thread
        /// </summary>
        public volatile Exception FatalException = null;

        public volatile bool SyncWorldState = false;

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
            public bool ResendSaveData;
            public const int MaxRequestedAppearancesSize = 10;

            public MiscClientData(PlayerMetadata Metadata, bool Disconnecting, ICollection<Guid> RequestedAppearances, bool ResendSaveData)
            {
                this.Metadata = Metadata;
                this.Disconnecting = Disconnecting;
                this.RequestedAppearances = RequestedAppearances;
                this.ResendSaveData = ResendSaveData;
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
                reader.ReadAndProcessSaveDataUpdate(SyncWorldState);
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
            bool ResendSaveData = reader.ReadBoolean();

            retval.Metadata = playerMetadata;
            retval.Disconnecting = Disconnecting;
            retval.ResendSaveData = ResendSaveData;
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
        protected long WriteClientGameTickPacket(BinaryNetworkWriter writer0, PlayerMetadata playerMetadata, SaveDataChanges saveDataUpdate, ActiveLevelState? levelState,
                PlayerAppearance? appearance, ICollection<Guid> requestPlayerAppearance, bool Disconnecting, bool ResendSaveData)
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
                    writer.Write(saveDataUpdate != null);
                    if (saveDataUpdate != null)
                    {
                        writer.Write(saveDataUpdate);
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
                    writer.Write(ResendSaveData);
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
                reader.ReadAndProcessSaveDataUpdate(SyncWorldState);
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
            if (reader.ReadBoolean())
            {
                ProcessServerSharedSaveData(reader.ReadSharedSaveData());
            }
            byte test = reader.ReadByte();//0xfe
        }
#else
        /// <summary>Writes the supplied server data to client's network stream <paramref name="writer0"/></summary>
        /// <remarks>
        ///     Note: This method has a lot of arguments so it is more easily identifiable if one of the arguments is unused.<br />
        ///     Note: The data written by this method should be read by <seealso cref="ReadServerGameTickPacket"/>
        /// </remarks>
        protected void WriteServerGameTickPacket(BinaryNetworkWriter writer0, List<PlayerMetadata> playerMetadatas, SaveDataChanges saveDataUpdate, ICollection<ActiveLevelState> levelStates,
                                                            ICollection<Guid> disconnectedPlayers, IDictionary<Guid, PlayerAppearance> appearances, Guid? NewClientGuid,
                                                            bool RequestAppearance, SaveData sharedSaveData, TimeSpan timeOfDay)
        {
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
                    writer.Write(saveDataUpdate != null);
                    if (saveDataUpdate != null)
                    {
                        writer.Write(saveDataUpdate);
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
                    bool hasSaveData = sharedSaveData != null;
                    writer.Write(hasSaveData);
                    if (hasSaveData)
                    {
                        writer.Write(sharedSaveData);
                    }
                    writer.Write((byte)0xfe);
                    writer.Flush();
                }
                byte[] data = ms.ToArray();
                datalength = data.Length;
                writer0.Write(data);
                writer0.Flush();
            }
        }
#endif
        protected void UpdatePlayerAppearance(Guid puid, PlayerAppearance newAp)
        {
            _ = PlayerAppearances.AddOrUpdate(puid, (u) => newAp, (u, a) => newAp);
        }
        protected abstract void ProcessDisconnect(Guid puid);
        protected abstract void ProcessActiveLevelState(ActiveLevelState activeLevelState);
#if FEZCLIENT
        protected abstract void ProcessNewClientGuid(Guid puid);
        protected abstract void SetTimeOfDay(long newTimeOfDayTicks);
        protected abstract void ProcessServerSharedSaveData(SaveData saveData);

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
    public static class BinaryIOExtensions 
    {
#if !FEZCLIENT
        public static void WriteObject(this BinaryWriter writer, string s)
        {
            writer.Write(s != null);
            if (s != null)
            {
                writer.Write(s);
            }
        }
        public static void WriteObject(this BinaryWriter writer, float? s)
        {
            writer.Write(s.HasValue);
            if (s != null)
            {
                writer.Write(s.Value);
            }
        }
        public static void Write(this BinaryWriter writer, Vector3 s)
        {
            writer.Write(s.X);
            writer.Write(s.Y);
            writer.Write(s.Z);
        }
        public static void Write(this BinaryWriter writer, TrileEmplacement s)
        {
            writer.Write(s.X);
            writer.Write(s.Y);
            writer.Write(s.Z);
        }
#endif
        public static string ReadNullableString(this BinaryReader reader)
        {
            if (reader.ReadBoolean())
            {
                return reader.ReadString();
            }
            return null;
        }
        public static float? ReadNullableSingle(this BinaryReader reader)
        {
            if (reader.ReadBoolean())
            {
                return reader.ReadSingle();
            }
            return null;
        }
        public static Vector3 ReadVector3(this BinaryReader reader)
        {
            return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        public static TrileEmplacement ReadTrileEmplacement(this BinaryReader reader)
        {
            return new TrileEmplacement(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
        }
    }
}
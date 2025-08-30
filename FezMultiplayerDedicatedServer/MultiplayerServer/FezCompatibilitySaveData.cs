using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace FezMultiplayerDedicatedServer
{
    public class SaveData
    {
        public bool IsNew => false;

        public long CreationTime;

        //TODO increment PlayTime
        public long PlayTime;

        //TODO increment SinceLastSaved
        //TODO use SinceLastSaved to save the server's save data somewhere and then reset SinceLastSaved
        public long? SinceLastSaved;

        //mainly to prevent having to start ng+ to get the abilities
        public bool HasFPView => CubeShards + SecretCubes >= 32;
        public bool HasStereo3D => CubeShards + SecretCubes >= 64;

        //TODO increment TimeOfDay
        public TimeSpan TimeOfDay = TimeSpan.FromHours(12.0);

        public List<string> UnlockedWarpDestinations = new List<string> { "NATURE_HUB" };

        public int Keys;
        public int CubeShards;
        public int SecretCubes;
        public int CollectedParts;
        public int CollectedOwls;
        public int PiecesOfHeart;

        public List<string> Maps = new List<string>();
        public List<ActorType> Artifacts = new List<ActorType>();

        public string ScriptingState;

        public float? GlobalWaterLevelModifier;

        public bool AchievementCheatCodeDone;
        public bool MapCheatCodeDone;

        public Dictionary<string, LevelSaveData> World = new Dictionary<string, LevelSaveData>();

        public SaveData()
        {
            CreationTime = DateTime.Now.ToFileTimeUtc();
            Clear();
        }

        public void Clear()
        {
            TimeOfDay = TimeSpan.FromHours(12.0);
            UnlockedWarpDestinations = new List<string> { "NATURE_HUB" };
            SecretCubes = CubeShards = Keys = 0;
            CollectedParts = CollectedOwls = 0;
            PiecesOfHeart = 0;
            Maps = new List<string>();
            Artifacts = new List<ActorType>();
            ScriptingState = null;
            GlobalWaterLevelModifier = null;
            MapCheatCodeDone = AchievementCheatCodeDone = false;
            World = new Dictionary<string, LevelSaveData>();
        }
    }

    public class LevelSaveData
    {
        public List<TrileEmplacement> DestroyedTriles = new List<TrileEmplacement>();
        public List<TrileEmplacement> InactiveTriles = new List<TrileEmplacement>();

        public List<int> InactiveArtObjects = new List<int>();
        public List<int> InactiveEvents = new List<int>();
        public List<int> InactiveGroups = new List<int>();
        public List<int> InactiveVolumes = new List<int>();
        public List<int> InactiveNPCs = new List<int>();

        public Dictionary<int, int> PivotRotations = new Dictionary<int, int>();

        public float? LastStableLiquidHeight;

        public string ScriptingState;

        public WinConditions FilledConditions = new WinConditions();
    }

    public class WinConditions
    {
        public int LockedDoorCount = 0;
        public int UnlockedDoorCount = 0;
        public int ChestCount = 0;
        public int CubeShardCount = 0;
        public int OtherCollectibleCount = 0;
        public int SplitUpCount = 0;
        public List<int> ScriptIds = new List<int>();
        public int SecretCount = 0;
    }
}
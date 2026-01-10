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

        #region mostly for full compatability with FEZ save files
        public bool Finished32;
        public bool Finished64;
        public bool HasDoneHeartReboot;
		public bool ScoreDirty;
        public List<string> EarnedAchievements = new List<string>();
        public List<string> EarnedGamerPictures = new List<string>();
        public bool AnyCodeDeciphered;
        public bool FezHidden;
        public bool HasHadMapHelp;
        public bool CanOpenMap => true;
        public bool CanNewGamePlus;
        public bool IsNewGamePlus;
        public Dictionary<string, bool> OneTimeTutorials;
        public string Level;
        public Viewpoint View;
        public Vector3 Ground;
        #endregion

        //mainly to prevent having to start ng+ to get the abilities
        public bool HasFPView => CubeShards + SecretCubes >= 32;
        public bool HasStereo3D => CubeShards + SecretCubes >= 64;

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
            Level = null;
            View = Viewpoint.None;
            CanNewGamePlus = false;
            IsNewGamePlus = false;
            Finished32 = Finished64 = false;
            HasDoneHeartReboot = false;
            Ground = new Vector3(0, 0, 0);
            TimeOfDay = TimeSpan.FromHours(12.0);
            UnlockedWarpDestinations = new List<string> { "NATURE_HUB" };
            SecretCubes = CubeShards = Keys = 0;
            CollectedParts = CollectedOwls = 0;
            PiecesOfHeart = 0;
            Maps = new List<string>();
            Artifacts = new List<ActorType>();
            ScoreDirty = false;
            ScriptingState = null;
            FezHidden = false;
            GlobalWaterLevelModifier = null;
            HasHadMapHelp = false;
            MapCheatCodeDone = AchievementCheatCodeDone = false;
            ScoreDirty = false;
            World = new Dictionary<string, LevelSaveData>();
            OneTimeTutorials = new Dictionary<string, bool>
            {
                { "DOT_LOCKED_DOOR_A", false },
                { "DOT_NUT_N_BOLT_A", false },
                { "DOT_PIVOT_A", false },
                { "DOT_TIME_SWITCH_A", false },
                { "DOT_TOMBSTONE_A", false },
                { "DOT_TREASURE", false },
                { "DOT_VALVE_A", false },
                { "DOT_WEIGHT_SWITCH_A", false },
                { "DOT_LESSER_A", false },
                { "DOT_WARP_A", false },
                { "DOT_BOMB_A", false },
                { "DOT_CLOCK_A", false },
                { "DOT_CRATE_A", false },
                { "DOT_TELESCOPE_A", false },
                { "DOT_WELL_A", false },
                { "DOT_WORKING", false }
            };
        }

        public void CloneInto(SaveData d)
        {
            d.AchievementCheatCodeDone = AchievementCheatCodeDone;
            d.AnyCodeDeciphered = AnyCodeDeciphered;
            d.CanNewGamePlus = CanNewGamePlus;
            //d.CanOpenMap = CanOpenMap;
            d.CollectedOwls = CollectedOwls;
            d.CollectedParts = CollectedParts;
            d.CreationTime = CreationTime;
            d.CubeShards = CubeShards;
            d.FezHidden = FezHidden;
            d.Finished32 = Finished32;
            d.Finished64 = Finished64;
            d.GlobalWaterLevelModifier = GlobalWaterLevelModifier;
            d.Ground = Ground;
            d.HasDoneHeartReboot = HasDoneHeartReboot;
            //d.HasFPView = HasFPView;
            d.HasHadMapHelp = HasHadMapHelp;
            //d.HasStereo3D = HasStereo3D;
            //d.IsNew = IsNew;
            d.IsNewGamePlus = IsNewGamePlus;
            d.Keys = Keys;
            d.Level = Level;
            d.MapCheatCodeDone = MapCheatCodeDone;
            d.PiecesOfHeart = PiecesOfHeart;
            d.PlayTime = PlayTime;
            d.ScoreDirty = ScoreDirty;
            d.ScriptingState = ScriptingState;
            d.SecretCubes = SecretCubes;
            d.SinceLastSaved = SinceLastSaved;
            d.TimeOfDay = TimeOfDay;
            d.View = View;
            try
            {
                d.Artifacts.Clear();
                d.Artifacts.AddRange(Artifacts);
                d.EarnedAchievements.Clear();
                d.EarnedAchievements.AddRange(EarnedAchievements);
                d.EarnedGamerPictures.Clear();
                d.EarnedGamerPictures.AddRange(EarnedGamerPictures);
                d.Maps.Clear();
                d.Maps.AddRange(Maps);
                d.UnlockedWarpDestinations.Clear();
                d.UnlockedWarpDestinations.AddRange(UnlockedWarpDestinations);
                d.OneTimeTutorials.Clear();
                foreach (string key in OneTimeTutorials.Keys)
                {
                    d.OneTimeTutorials[key] = OneTimeTutorials[key];
                }
                foreach (string key2 in World.Keys)
                {
                    if (!d.World.ContainsKey(key2))
                    {
                        d.World.Add(key2, new LevelSaveData());
                    }
                    World[key2].CloneInto(d.World[key2]);
                }
                foreach (string key3 in d.World.Keys)
                {
                    if (!World.ContainsKey(key3))
                    {
                        d.World.Remove(key3);
                    }
                }
            }
            catch (InvalidOperationException)
            {
                CloneInto(d);
            }
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

        #region mostly for full compatability with FEZ save files
        public bool FirstVisit;
        #endregion

        public WinConditions FilledConditions = new WinConditions();

        public void CloneInto(LevelSaveData d)
        {
            FilledConditions.CloneInto(d.FilledConditions);
            d.FirstVisit = FirstVisit;
            d.LastStableLiquidHeight = LastStableLiquidHeight;
            d.ScriptingState = ScriptingState;
            d.DestroyedTriles.Clear();
            d.DestroyedTriles.AddRange(DestroyedTriles);
            d.InactiveArtObjects.Clear();
            d.InactiveArtObjects.AddRange(InactiveArtObjects);
            d.InactiveEvents.Clear();
            d.InactiveEvents.AddRange(InactiveEvents);
            d.InactiveGroups.Clear();
            d.InactiveGroups.AddRange(InactiveGroups);
            d.InactiveNPCs.Clear();
            d.InactiveNPCs.AddRange(InactiveNPCs);
            d.InactiveTriles.Clear();
            d.InactiveTriles.AddRange(InactiveTriles);
            d.InactiveVolumes.Clear();
            d.InactiveVolumes.AddRange(InactiveVolumes);
            d.PivotRotations.Clear();
            foreach (int key in PivotRotations.Keys)
            {
                d.PivotRotations.Add(key, PivotRotations[key]);
            }
        }

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

        public void CloneInto(WinConditions w)
        {
            w.LockedDoorCount = LockedDoorCount;
            w.UnlockedDoorCount = UnlockedDoorCount;
            w.ChestCount = ChestCount;
            w.CubeShardCount = CubeShardCount;
            w.OtherCollectibleCount = OtherCollectibleCount;
            w.SplitUpCount = SplitUpCount;
            w.SecretCount = SecretCount;
            w.ScriptIds.Clear();
            w.ScriptIds.AddRange(ScriptIds);
        }

    }
}
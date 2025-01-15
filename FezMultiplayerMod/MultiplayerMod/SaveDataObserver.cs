using FezEngine.Components;
using FezEngine.Services;
using FezEngine.Structure;
using FezEngine.Tools;
using FezGame.Services;
using FezGame.Structure;
using Microsoft.Xna.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace FezGame.MultiplayerMod
{
    internal sealed class SaveDataObserver : GameComponent
    {
        public class SaveDataChanges
        {
            public bool HasChanges => Changes.Any();

            //Note this collection should only have a single entry per unique containerIdentifier and field combo
            public readonly Dictionary<string, ChangeInfo> Changes = new Dictionary<string, ChangeInfo>();

            internal void Add(string containerIdentifier, string key, object currentVal, object oldVal)
            {
                //Note this collection should only have a single entry per unique containerIdentifier and field combo
                string uniqueIdentifier = containerIdentifier + IDENTIFIER_SEPARATOR + key;
                if (Changes.TryGetValue(uniqueIdentifier, out ChangeInfo change))
                {
                    Changes.Add(uniqueIdentifier, new ChangeInfo(containerIdentifier, key, currentVal, change.OldVal));
                }
                else
                {
                    Changes.Add(uniqueIdentifier, new ChangeInfo(containerIdentifier, key, currentVal, oldVal));
                }
            }

            public override string ToString()
            {
                return string.Join(Environment.NewLine, Changes);
            }

            public class ChangeInfo
            {
                public readonly string ContainerIdentifier;
                public readonly string Key;
                public readonly object NewVal;
                public readonly object OldVal;

                public ChangeInfo(string containerIdentifier, string fieldName, object currentVal, object oldVal)
                {
                    ContainerIdentifier = containerIdentifier;
                    Key = fieldName;
                    NewVal = currentVal;
                    OldVal = oldVal;
                }

                public override string ToString()
                {
                    return $"(Container: {ContainerIdentifier}, Key: {Key}, CurrentVal: {NewVal}, OldVal: {OldVal})";
                }
            }
        }

        private IGameStateManager GameState { get; set; }
        private IPlayerManager PM { get; set; }

        //internal static readonly string IDENTIFIER_SEPARATOR = ".";
        private const string IDENTIFIER_SEPARATOR = ".";

        private SaveData CurrentSaveData => GameState?.SaveData;
        private readonly SaveData OldSaveData = new SaveData();

        public event Action<SaveData, SaveDataChanges> OnSaveDataChanged = (UpdatedSaveData, SaveDataChanges) => { };

        public SaveDataObserver(Game game) : base(game)
        {
            _ = Waiters.Wait(() =>
            {
                return ServiceHelper.FirstLoadDone;
            },
            () =>
            {
                GameState = ServiceHelper.Get<IGameStateManager>();
                PM = ServiceHelper.Get<IPlayerManager>();
            });
        }
        private static readonly SaveDataChanges newChanges = new SaveDataChanges();
        public override void Update(GameTime gameTime)
        {
            //check a save file is actually loaded 
            int slot = GameState?.SaveSlot ?? -1;
            if (CurrentSaveData != null && slot >= 0
                    && !GameState.Loading
                    && !GameState.TimePaused
                    //Note: GameState.TimePaused combines
                    //          GameState.Paused,
                    //          GameState.ForceTimePaused,
                    //          GameState.InCutscene,
                    //          GameState.InMenuCube,
                    //          GameState.InMap,
                    //          GameState.InFpsMode
                    //So we shouldn't need the following line:
                    //&& !GameState.Paused && !GameState.ForceTimePaused && !GameState.InCutscene

                    //check the player actually exists
                    && PM.CanControl && PM.Action != ActionType.None && !PM.Hidden)
            {
                newChanges.Changes.Clear();

                /*
                 * Honestly, instead of using reflection, it would probably be faster and more memory efficient
                 * to explicitly check every field in SaveData
                 */
                //CheckType(newChanges, typeof(SaveData), "SaveData", CurrentSaveData, OldSaveData);
                CheckSaveData(newChanges, CurrentSaveData, OldSaveData);

                //update old data
                CurrentSaveData.CloneInto(OldSaveData);

                //Notify listeners of changes
                if (newChanges.HasChanges)
                {
                    OnSaveDataChanged(CurrentSaveData, newChanges);
                }
            }
        }
        /// <summary>
        /// Uses manual checking to recursively compare all the values for all the field in <see cref="SaveData"/>
        /// and add the changes to the supplied <see cref="SaveDataChanges"/> object.
        /// </summary>
        /// <param name="changes">The place where the changes are recorded</param>
        /// <param name="currentSaveData">The current <see cref="SaveData"/> to find changes</param>
        /// <param name="oldSaveData">The old <see cref="SaveData"/> to find changes</param>
        private static void CheckSaveData(SaveDataChanges changes, SaveData currentSaveData, SaveData oldSaveData)
        {
            void AddIfDifferent_ValueType(string containerIdentifier, string name, object currentVal, object oldVal)
            {
                if (!object.Equals(currentVal, oldVal))
                {
                    changes.Add(containerIdentifier, name, currentVal, oldVal);
                }
            }
            void AddIfDifferent_ValueList(string containerIdentifier, string name, IList currentVal, IList oldVal)
            {
                //Note: all the List fields in SaveData are of value types (namely List<string> and List<ActorType>)
                //Note: all the List fields in LevelSaveData are of value types (namely List<TrileEmplacement> and List<int>)
                //Note: the only List fields in WinConditions is of value type List<int>
                //This means none of the fields in SaveData or the types it contains have a List of non-value types

                HashSet<object> currentVal_hashset = new HashSet<object>(currentVal.Cast<object>());
                HashSet<object> oldVal_hashset = new HashSet<object>(oldVal.Cast<object>());

                // Find items in currentVal that are not in oldVal
                List<object> addedVals = currentVal_hashset.Except(oldVal_hashset).ToList();

                // Find items in list2 that are not in list1
                List<object> removedVals = oldVal_hashset.Except(currentVal_hashset).ToList();

                if (addedVals.Count > 0 || removedVals.Count > 0)
                {
                    // add changes to changes
                    addedVals.ForEach(addedVal =>
                    {
                        changes.Add(containerIdentifier, name, addedVal, null);
                    });
                    removedVals.ForEach(removedVal =>
                    {
                        //TODO? idk if this ever actually happens, but should probably implement it somehow
                        changes.Add(containerIdentifier, name, null, oldVal);
                    });
                }
            }
            void AddIfDifferent_Dict(string containerIdentifier, string name, IDictionary currentVal_dict, IDictionary oldVal_dict, bool world = false)
            {
                //Note: SaveData contains fields of types Dictionary<string, bool> and Dictionary<string, LevelSaveData>
                //Note: LevelSaveData contains fields of types Dictionary<int, int>

                //iterate through the entries checking the keys and values

                HashSet<object> currentVal_dictKeys = new HashSet<object>(currentVal_dict.Keys.Cast<object>());
                HashSet<object> oldVal_dictKeys = new HashSet<object>(oldVal_dict.Keys.Cast<object>());

                // Find keys in currentVal_dict that are not in oldVal_dict
                List<object> onlyIncurrentVal_dictKeys = currentVal_dictKeys.Except(oldVal_dictKeys).ToList();

                // Find keys in oldVal_dict that are not in currentVal_dict
                List<object> onlyInoldVal_dictKeys = oldVal_dictKeys.Except(currentVal_dictKeys).ToList();

                string containerID = containerIdentifier + IDENTIFIER_SEPARATOR + name;

                onlyIncurrentVal_dictKeys.ForEach(k =>
                {
                    object val = currentVal_dict[k];
                    changes.Add(containerID, k.ToString(), val, null);
                });
                onlyInoldVal_dictKeys.ForEach(k =>
                {
                    object val = oldVal_dict[k];
                    changes.Add(containerID, k.ToString(), null, val);
                });
                // For keys that are present in both, check for value differences
                List<object> sharedKeys = currentVal_dictKeys.Intersect(oldVal_dictKeys).ToList();
                foreach (object key in sharedKeys)
                {
                    object value1 = currentVal_dict[key];
                    object value2 = oldVal_dict[key];
                    string containerID_withKey = containerID + "[" + key.ToString() + "]";
                    if (world)
                    {
                        CheckLevelSaveData(containerID_withKey, (LevelSaveData)value1, (LevelSaveData)value2);
                    }
                    else
                    {
                        AddIfDifferent_ValueType(containerID, "" + key, value1, value2);
                    }
                }
            }
            void CheckLevelSaveData(string containerID_withKey, LevelSaveData currentLevelSaveData, LevelSaveData oldLevelSaveData)
            {
                void CheckWinConditions(string containerID, WinConditions currentWinConditions, WinConditions oldWinConditions)
                {
                    AddIfDifferent_ValueType(containerID, "LockedDoorCount", currentWinConditions.LockedDoorCount, oldWinConditions.LockedDoorCount);
                    AddIfDifferent_ValueType(containerID, "UnlockedDoorCount", currentWinConditions.UnlockedDoorCount, oldWinConditions.UnlockedDoorCount);
                    AddIfDifferent_ValueType(containerID, "ChestCount", currentWinConditions.ChestCount, oldWinConditions.ChestCount);
                    AddIfDifferent_ValueType(containerID, "CubeShardCount", currentWinConditions.CubeShardCount, oldWinConditions.CubeShardCount);
                    AddIfDifferent_ValueType(containerID, "OtherCollectibleCount", currentWinConditions.OtherCollectibleCount, oldWinConditions.OtherCollectibleCount);
                    AddIfDifferent_ValueType(containerID, "SplitUpCount", currentWinConditions.SplitUpCount, oldWinConditions.SplitUpCount);
                    AddIfDifferent_ValueList(containerID, "ScriptIds", currentWinConditions.ScriptIds, oldWinConditions.ScriptIds);
                    AddIfDifferent_ValueType(containerID, "SecretCount", currentWinConditions.SecretCount, oldWinConditions.SecretCount);
                }
                AddIfDifferent_ValueList(containerID_withKey, "DestroyedTriles", currentLevelSaveData.DestroyedTriles, oldLevelSaveData.DestroyedTriles);
                AddIfDifferent_ValueList(containerID_withKey, "InactiveTriles", currentLevelSaveData.InactiveTriles, oldLevelSaveData.InactiveTriles);
                AddIfDifferent_ValueList(containerID_withKey, "InactiveArtObjects", currentLevelSaveData.InactiveArtObjects, oldLevelSaveData.InactiveArtObjects);
                AddIfDifferent_ValueList(containerID_withKey, "InactiveEvents", currentLevelSaveData.InactiveEvents, oldLevelSaveData.InactiveEvents);
                AddIfDifferent_ValueList(containerID_withKey, "InactiveGroups", currentLevelSaveData.InactiveGroups, oldLevelSaveData.InactiveGroups);
                AddIfDifferent_ValueList(containerID_withKey, "InactiveVolumes", currentLevelSaveData.InactiveVolumes, oldLevelSaveData.InactiveVolumes);
                AddIfDifferent_ValueList(containerID_withKey, "InactiveNPCs", currentLevelSaveData.InactiveNPCs, oldLevelSaveData.InactiveNPCs);
                AddIfDifferent_Dict(containerID_withKey, "PivotRotations", currentLevelSaveData.PivotRotations, oldLevelSaveData.PivotRotations);
                AddIfDifferent_ValueType(containerID_withKey, "LastStableLiquidHeight", currentLevelSaveData.LastStableLiquidHeight, oldLevelSaveData.LastStableLiquidHeight);
                AddIfDifferent_ValueType(containerID_withKey, "ScriptingState", currentLevelSaveData.ScriptingState, oldLevelSaveData.ScriptingState);
                AddIfDifferent_ValueType(containerID_withKey, "FirstVisit", currentLevelSaveData.FirstVisit, oldLevelSaveData.FirstVisit);
                CheckWinConditions(containerID_withKey + IDENTIFIER_SEPARATOR + "FilledConditions", currentLevelSaveData.FilledConditions, oldLevelSaveData.FilledConditions);
            }
            AddIfDifferent_ValueType("SaveData", "CreationTime", currentSaveData.CreationTime, oldSaveData.CreationTime);
            AddIfDifferent_ValueType("SaveData", "Finished32", currentSaveData.Finished32, oldSaveData.Finished32);
            AddIfDifferent_ValueType("SaveData", "Finished64", currentSaveData.Finished64, oldSaveData.Finished64);
            AddIfDifferent_ValueType("SaveData", "HasFPView", currentSaveData.HasFPView, oldSaveData.HasFPView);
            AddIfDifferent_ValueType("SaveData", "HasStereo3D", currentSaveData.HasStereo3D, oldSaveData.HasStereo3D);
            AddIfDifferent_ValueType("SaveData", "CanNewGamePlus", currentSaveData.CanNewGamePlus, oldSaveData.CanNewGamePlus);
            AddIfDifferent_ValueType("SaveData", "IsNewGamePlus", currentSaveData.IsNewGamePlus, oldSaveData.IsNewGamePlus);
            AddIfDifferent_Dict("SaveData", "OneTimeTutorials", currentSaveData.OneTimeTutorials, oldSaveData.OneTimeTutorials);
            AddIfDifferent_ValueType("SaveData", "Level", currentSaveData.Level, oldSaveData.Level);
            AddIfDifferent_ValueType("SaveData", "View", currentSaveData.View, oldSaveData.View);
            AddIfDifferent_ValueType("SaveData", "Ground", currentSaveData.Ground, oldSaveData.Ground);
            AddIfDifferent_ValueType("SaveData", "TimeOfDay", currentSaveData.TimeOfDay, oldSaveData.TimeOfDay);
            AddIfDifferent_ValueList("SaveData", "UnlockedWarpDestinations", currentSaveData.UnlockedWarpDestinations, oldSaveData.UnlockedWarpDestinations);
            AddIfDifferent_ValueType("SaveData", "Keys", currentSaveData.Keys, oldSaveData.Keys);
            AddIfDifferent_ValueType("SaveData", "CubeShards", currentSaveData.CubeShards, oldSaveData.CubeShards);
            AddIfDifferent_ValueType("SaveData", "SecretCubes", currentSaveData.SecretCubes, oldSaveData.SecretCubes);
            AddIfDifferent_ValueType("SaveData", "CollectedParts", currentSaveData.CollectedParts, oldSaveData.CollectedParts);
            AddIfDifferent_ValueType("SaveData", "CollectedOwls", currentSaveData.CollectedOwls, oldSaveData.CollectedOwls);
            AddIfDifferent_ValueType("SaveData", "PiecesOfHeart", currentSaveData.PiecesOfHeart, oldSaveData.PiecesOfHeart);
            AddIfDifferent_ValueList("SaveData", "Maps", currentSaveData.Maps, oldSaveData.Maps);
            AddIfDifferent_ValueList("SaveData", "Artifacts", currentSaveData.Artifacts, oldSaveData.Artifacts);
            AddIfDifferent_ValueList("SaveData", "EarnedAchievements", currentSaveData.EarnedAchievements, oldSaveData.EarnedAchievements);
            AddIfDifferent_ValueList("SaveData", "EarnedGamerPictures", currentSaveData.EarnedGamerPictures, oldSaveData.EarnedGamerPictures);
            AddIfDifferent_ValueType("SaveData", "ScriptingState", currentSaveData.ScriptingState, oldSaveData.ScriptingState);
            AddIfDifferent_ValueType("SaveData", "FezHidden", currentSaveData.FezHidden, oldSaveData.FezHidden);
            AddIfDifferent_ValueType("SaveData", "GlobalWaterLevelModifier", currentSaveData.GlobalWaterLevelModifier, oldSaveData.GlobalWaterLevelModifier);
            AddIfDifferent_ValueType("SaveData", "HasHadMapHelp", currentSaveData.HasHadMapHelp, oldSaveData.HasHadMapHelp);
            AddIfDifferent_ValueType("SaveData", "CanOpenMap", currentSaveData.CanOpenMap, oldSaveData.CanOpenMap);
            AddIfDifferent_ValueType("SaveData", "AchievementCheatCodeDone", currentSaveData.AchievementCheatCodeDone, oldSaveData.AchievementCheatCodeDone);
            AddIfDifferent_ValueType("SaveData", "AnyCodeDeciphered", currentSaveData.AnyCodeDeciphered, oldSaveData.AnyCodeDeciphered);
            AddIfDifferent_ValueType("SaveData", "MapCheatCodeDone", currentSaveData.MapCheatCodeDone, oldSaveData.MapCheatCodeDone);
            AddIfDifferent_Dict("SaveData", "World", currentSaveData.World, oldSaveData.World, world: true);
            AddIfDifferent_ValueType("SaveData", "ScoreDirty", currentSaveData.ScoreDirty, oldSaveData.ScoreDirty);
            AddIfDifferent_ValueType("SaveData", "HasDoneHeartReboot", currentSaveData.HasDoneHeartReboot, oldSaveData.HasDoneHeartReboot);
            AddIfDifferent_ValueType("SaveData", "PlayTime", currentSaveData.PlayTime, oldSaveData.PlayTime);
            AddIfDifferent_ValueType("SaveData", "IsNew", currentSaveData.IsNew, oldSaveData.IsNew);
        }
        /// <summary>
        /// Uses type checking to recursively compare the value(s) for the field specified by <paramref name="field"/>
        /// and add the changes to the supplied <see cref="SaveDataChanges"/> object.
        /// </summary>
        /// <param name="changes">The place where the changes are recorded</param>
        /// <param name="containerIdentifier">The string used to denote the object path of the changes</param>
        /// <param name="field">The field to compare</param>
        /// <param name="currentObject">The current object to find changes</param>
        /// <param name="oldObject">The old object to find changes</param>
        private static void CheckField(SaveDataChanges changes, string containerIdentifier, FieldInfo field, object currentObject, object oldObject)
        {
            if (containerIdentifier is null)
            {
                throw new ArgumentNullException(nameof(containerIdentifier));
            }
            if (field is null)
            {
                throw new ArgumentNullException(nameof(field));
            }

            Type fieldType = field.FieldType;
            object currentVal = field.GetValue(currentObject);
            object oldVal = field.GetValue(oldObject);

            if (fieldType.IsValueType)
            {
                // Compare value types directly
                if (!object.Equals(currentVal, oldVal))
                {
                    //value changed
                    changes.Add(containerIdentifier, field.Name, currentVal, oldVal);
                }
            }
            else
            {
                if (typeof(string).IsAssignableFrom(fieldType))
                {
                    if (!string.Equals((string)currentVal, (string)oldVal, StringComparison.Ordinal))
                    {
                        //value changed
                        changes.Add(containerIdentifier, field.Name, currentVal, oldVal);
                    }
                }
                // handle collections like Lists
                else if (typeof(IList).IsAssignableFrom(fieldType) || (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>)))
                {
                    //Note: all the List fields in SaveData are of value types (namely List<string> and List<ActorType>)
                    //Note: all the List fields in LevelSaveData are of value types (namely List<TrileEmplacement> and List<int>)
                    //Note: the only List fields in WinConditions is of value type List<int>
                    //This means none of the fields in SaveData or the types it contains have a List of non-value types

                    HashSet<object> currentVal_hashset = new HashSet<object>(((IList)currentVal).Cast<object>());
                    HashSet<object> oldVal_hashset = new HashSet<object>(((IList)oldVal).Cast<object>());

                    // Find items in currentVal that are not in oldVal
                    List<object> addedVals = currentVal_hashset.Except(oldVal_hashset).ToList();

                    // Find items in list2 that are not in list1
                    List<object> removedVals = oldVal_hashset.Except(currentVal_hashset).ToList();

                    if (addedVals.Count > 0 || removedVals.Count > 0)
                    {
                        // add changes to changes
                        addedVals.ForEach(addedVal =>
                        {
                            changes.Add(containerIdentifier, field.Name, addedVal, null);
                        });
                        removedVals.ForEach(removedVal =>
                        {
                            //TODO? idk if this ever actually happens, but should probably implement it somehow
                            changes.Add(containerIdentifier, field.Name, null, oldVal);
                        });
                    }
                }
                // handle collections like Dictionaries
                else if (typeof(IDictionary).IsAssignableFrom(fieldType) || (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>)))
                {
                    //Note: SaveData contains fields of types Dictionary<string, bool> and Dictionary<string, LevelSaveData>
                    //Note: LevelSaveData contains fields of types Dictionary<int, int>

                    IDictionary currentVal_dict = (IDictionary)currentVal;
                    IDictionary oldVal_dict = (IDictionary)oldVal;

                    //iterate through the entries checking the keys and values

                    HashSet<object> currentVal_dictKeys = new HashSet<object>(currentVal_dict.Keys.Cast<object>());
                    HashSet<object> oldVal_dictKeys = new HashSet<object>(oldVal_dict.Keys.Cast<object>());

                    // Find keys in currentVal_dict that are not in oldVal_dict
                    List<object> onlyIncurrentVal_dictKeys = currentVal_dictKeys.Except(oldVal_dictKeys).ToList();

                    // Find keys in oldVal_dict that are not in currentVal_dict
                    List<object> onlyInoldVal_dictKeys = oldVal_dictKeys.Except(currentVal_dictKeys).ToList();

                    onlyIncurrentVal_dictKeys.ForEach(k =>
                    {
                        string containerID = containerIdentifier + IDENTIFIER_SEPARATOR + field.Name + "[" + k.ToString() + "]";
                        object val = currentVal_dict[k];
                        changes.Add(containerID, k.ToString(), val, null);
                    });
                    onlyInoldVal_dictKeys.ForEach(k =>
                    {
                        string containerID = containerIdentifier + IDENTIFIER_SEPARATOR + field.Name + "[" + k.ToString() + "]";
                        object val = oldVal_dict[k];
                        changes.Add(containerID, k.ToString(), null, val);
                    });
                    // For keys that are present in both, check for value differences
                    List<object> sharedKeys = currentVal_dictKeys.Intersect(oldVal_dictKeys).ToList();
                    foreach (object key in sharedKeys)
                    {
                        object value1 = currentVal_dict[key];
                        object value2 = oldVal_dict[key];
                        string containerID = containerIdentifier + IDENTIFIER_SEPARATOR + field.Name;
                        string containerID_withKey = containerID + "[" + key.ToString() + "]";
                        void ComparePrimitive()
                        {
                            if (!Equals(value1, value2))
                            {
                                // add changes to changes
                                changes.Add(containerID, ""+key, value1, value2);
                            }
                        }
                        if (fieldType.IsGenericType)
                        {
                            Type valueType = fieldType.GetGenericArguments()[1];
                            if (valueType.IsValueType)
                            {
                                ComparePrimitive();
                            }
                            else
                            {
                                // compare values using CheckType
                                CheckType(changes, valueType, containerID_withKey, value1, value2);
                            }
                        }
                        else if (!Equals(value1, value2))
                        {
                            ComparePrimitive();
                        }
                    }
                }
                // handle collections that are neither List nor Dictionary
                else if (typeof(IEnumerable).IsAssignableFrom(fieldType))
                {
                    Console.WriteLine(fieldType);
                    System.Diagnostics.Debugger.Launch();
                    //TODO if this happens, handle the unhandled enumerable type
                }
                else
                {
                    //It must be a non-enumerable object of some kind
                    // handle non-enumerable object types like LevelSaveData and WinConditions
                    CheckType(changes, fieldType, containerIdentifier + IDENTIFIER_SEPARATOR + fieldType.Name, currentVal, oldVal);
                }
            }
        }
        private static Dictionary<Type, FieldInfo[]> cachedTypeFieldInfoMap = new Dictionary<Type, FieldInfo[]>();
        /// <summary>
        /// Uses reflection to recursively iterate through all the values for all the fields 
        /// and add the changes to the supplied <see cref="SaveDataChanges"/> object.
        /// </summary>
        /// <param name="changes">The place where the changes are recorded</param>
        /// <param name="containingType">The type of the supplied objects</param>
        /// <param name="containerIdentifier">The string used to denote the object path of the changes</param>
        /// <param name="currentObject">The current object to find changes</param>
        /// <param name="oldObject">The old object to find changes</param>
        private static void CheckType(SaveDataChanges changes, Type containingType, string containerIdentifier, object currentObject, object oldObject)
        {
            FieldInfo[] fields;
            if (!cachedTypeFieldInfoMap.TryGetValue(containingType, out fields))
            {
                fields = containingType.GetFields(BindingFlags.Public | BindingFlags.Instance);
                cachedTypeFieldInfoMap.Add(containingType, fields);
            }

            foreach (FieldInfo field in fields)
            {
                CheckField(changes, containerIdentifier, field, currentObject, oldObject);
            }
        }
    }

}

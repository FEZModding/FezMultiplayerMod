#if FEZCLIENT
using FezEngine.Components;
using FezEngine.Services;
using FezEngine.Structure;
using FezEngine.Tools;
using FezGame.Services;
using FezGame.Structure;
using Microsoft.Xna.Framework;
#else
using FezMultiplayerDedicatedServer;
#endif
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace FezSharedTools
{
    public class SaveDataChanges
    {
        public bool HasChanges => KeyedChanges.Any() || ListChanges.Any();

        private List<ChangeInfo> Changes => KeyedChanges.Values.Cast<ChangeInfo>().Concat(ListChanges.Values).ToList();
        private readonly ConcurrentDictionary<string, KeyedChangeInfo> KeyedChanges = new ConcurrentDictionary<string, KeyedChangeInfo>();
        private readonly ConcurrentDictionary<string, ChangeInfo> ListChanges = new ConcurrentDictionary<string, ChangeInfo>();

        public void ClearChanges()
        {
            KeyedChanges.Clear();
            ListChanges.Clear();
        }

        private static readonly List<string> ignoredKeys = new List<string>()
        {
            "AnyCodeDeciphered",
            "CanNewGamePlus",
            "CreationTime",
            "EarnedAchievements",
            "EarnedGamerPictures",
            "FezHidden",
            "Finished32",
            "Finished64",
            "Ground",
            "HasDoneHeartReboot",
            "HasHadMapHelp",
            "IsNew",
            "IsNewGamePlus",
            "Level",
            "OneTimeTutorials",
            "PlayTime",
            "ScoreDirty",
            "TimeOfDay",
            "View",
            "SinceLastSaved",
        };
        private static readonly int ignoreLength = ("SaveData" + SaveDataObserver.SAVE_DATA_IDENTIFIER_SEPARATOR).Length;
        private static readonly string SAVE_DATA_IDENTIFIER_SEPARATOR_ESCAPED = Regex.Escape(SaveDataObserver.SAVE_DATA_IDENTIFIER_SEPARATOR);
        private static readonly Regex ignoreWorldRegex = new Regex($@"^World{SAVE_DATA_IDENTIFIER_SEPARATOR_ESCAPED}\w+{SAVE_DATA_IDENTIFIER_SEPARATOR_ESCAPED}FirstVisit");
        private static readonly Regex ignoreWorldLevelRegex = new Regex($@"^World{SAVE_DATA_IDENTIFIER_SEPARATOR_ESCAPED}\w+$");
        internal void AddListChange(string containerIdentifier, object entry, ChangeType changeType)
        {
            containerIdentifier = containerIdentifier.Substring(ignoreLength);
            if (ignoredKeys.Contains(containerIdentifier))
            {
                return;
            }
            if (ignoreWorldRegex.IsMatch(containerIdentifier))
            {
                return;
            }
            //Note this collection should only have a single entry per unique containerIdentifier and field combo
            string uniqueIdentifier = containerIdentifier + SaveDataObserver.SAVE_DATA_IDENTIFIER_SEPARATOR + entry;
            ListChanges[uniqueIdentifier] = new ChangeInfo(changeType, containerIdentifier, entry);
        }
        internal void AddKeyedChange(string uniqueIdentifier, object newval, object oldVal)
        {
            uniqueIdentifier = uniqueIdentifier.Substring(ignoreLength);
            if (ignoredKeys.Contains(uniqueIdentifier))
            {
                return;
            }
            if (ignoreWorldLevelRegex.IsMatch(uniqueIdentifier))
            {
                return;
            }
            //Note this collection should only have a single entry per unique containerIdentifier and field combo
            if (KeyedChanges.TryGetValue(uniqueIdentifier, out KeyedChangeInfo change))
            {
                oldVal = change.OldVal;
            }
            KeyedChanges[uniqueIdentifier] = new KeyedChangeInfo(uniqueIdentifier, newval, oldVal, ChangeType.Keyed);
        }

        public override string ToString()
        {
            return string.Join(Environment.NewLine, Changes);
        }
        private static readonly char SAVE_DATA_DATA_SEPARATOR = '\x1E';
        private static readonly char SAVE_DATA_ENTRY_SEPARATOR = '\x1D';
        private static readonly string SAVE_DATA_ENTRY_SEPARATOR_STR = SAVE_DATA_ENTRY_SEPARATOR.ToString();
        private static readonly char SAVE_DATA_IDENTIFIER_SEPARATOR_STR = SaveDataObserver.SAVE_DATA_IDENTIFIER_SEPARATOR[0];
        public byte[] Serialize()
        {
            return SharedConstants.UTF8.GetBytes(string.Join(SAVE_DATA_ENTRY_SEPARATOR_STR, Changes.Where(c=>c!=null).Select(c=>
            {
                return c.ContainerIdentifier + SAVE_DATA_DATA_SEPARATOR + ((int)c.ChangeType) + SAVE_DATA_DATA_SEPARATOR + ConvertToString(c.Value);
            })));
        }
        private static string ConvertToString(object obj)
        {
            Type t = obj.GetType();
            var m = t.GetMethod("Parse", new Type[] { typeof(string) });
            if(m!=null)
            {
                return "" + obj;
            }
            if(t.Equals(typeof(TrileEmplacement)))
            {
                TrileEmplacement emplacement = (TrileEmplacement)obj;
                return emplacement.X + ";" + emplacement.Y + ";" + emplacement.Z;
            }
            if(t.Equals(typeof(Vector3)))
            {
                Vector3 vector = (Vector3)obj;
                return vector.X + ";" + vector.Y + ";" + vector.Z;
            }
            if(t.Equals(typeof(TimeSpan)))
            {
                TimeSpan ts = (TimeSpan)obj;
                return "" + ts.Ticks;
            }
            //TODO maybe handle other types like TimeSpan
            if (t.IsEnum)
            {
                return "" + (int)obj;
            }
            System.Diagnostics.Debugger.Launch();
            System.Diagnostics.Debugger.Break();
            throw new ArgumentException($"Type {t.Name} is not handled by this method.", nameof(obj));
        }
        private static object ParseToType(Type t, string val)
        {
            var m = t.GetMethod("Parse", new Type[] { typeof(string) });
            if(m!=null)
            {
                return m.Invoke(null, new object[] { val });
            }
            if (t.Equals(typeof(TrileEmplacement)))
            {
                int[] xyz = val.Split(';').Select(int.Parse).ToArray();
                return new TrileEmplacement(xyz[0], xyz[1], xyz[2]);
            }
            if (t.Equals(typeof(Vector3)))
            {
                float[] xyz = val.Split(';').Select(float.Parse).ToArray();
                return new Vector3(xyz[0], xyz[1], xyz[2]);
            }
            if (t.Equals(typeof(TimeSpan)))
            {
                return new TimeSpan(long.Parse(val));
            }
            //TODO maybe handle other types like Vector3 and TimeSpan
            if (t.IsEnum)
            {
                return int.Parse(val);
            }
            System.Diagnostics.Debugger.Launch();
            System.Diagnostics.Debugger.Break();
            throw new ArgumentException($"Type {t.Name} is not handled by this method.", nameof(t));
        }
        private static readonly Type dictType = typeof(IDictionary);
        private static readonly Type listType = typeof(IList);
        public static void DeserializeAndProcess(byte[] bytes)
        {
            lock (SaveDataObserver.saveDataLock)
            {
                SaveData saveData = SaveDataObserver.Instance.CurrentSaveData;
                var entries = SharedConstants.UTF8.GetString(bytes).Split(SAVE_DATA_ENTRY_SEPARATOR);

                List<string> ChangeLog = new List<string>();
                foreach (var entry in entries)
                {
                    try
                    {
                        string[] r = entry.Split(SAVE_DATA_DATA_SEPARATOR);
                        if (ignoredKeys.Contains(r[0]) || ignoreWorldRegex.IsMatch(r[0]))
                        {
                            continue;
                        }
                        string[] keys = r[0].Split(SAVE_DATA_IDENTIFIER_SEPARATOR_STR);
                        ChangeType changeType = int.TryParse(r[1], out int t) ? (ChangeType)t : ChangeType.None;
                        string val = r[2];
                        Type currType = typeof(SaveData);
                        object currObj = saveData;
                        FieldInfo f = null;
                        for (int i = 0; i < keys.Length; ++i)
                        {
                            string key = keys[i];
                            if (dictType.IsAssignableFrom(currType))
                            {
                                IDictionary v = (IDictionary)currObj;
                                Type[] args = currType.GetGenericArguments();
                                Type ktype = args[0];
                                Type gtype = args[1];
                                object k = key;
                                bool entryAdded = false;
                                if (!ktype.Equals(typeof(string)))
                                {
                                    k = ParseToType(ktype, key);
                                }
                                if (!v.Contains(k))
                                {
                                    v.Add(k, Activator.CreateInstance(gtype));
                                    entryAdded = true;
                                }
                                currObj = v[k];
                                currType = gtype;
                                if (i == keys.Length - 1)
                                {
                                    if (!entryAdded)
                                    {
                                        object g = val;
                                        if (!gtype.Equals(typeof(string)) && gtype.IsValueType)
                                        {
                                            g = ParseToType(gtype, val);
                                        }
                                        v[k] = g;
                                    }
                                    ChangeLog.Add($"Added entry \"{val}\" to {r[0]}");
                                }
                            }
                            else
                            {
                                f = currType.GetField(key);
                                object parent = currObj;
                                currObj = f.GetValue(currObj);
                                currType = f.FieldType;
                                if (i == keys.Length - 1)
                                {
                                    object g = val;
                                    bool valChanged = false;
                                    if (!currType.Equals(typeof(string)))
                                    {
                                        if (listType.IsAssignableFrom(currType))
                                        {
                                            IList v = (IList)currObj;
                                            Type listType = currType.GetGenericArguments()[0];
                                            g = ParseToType(listType, val);
                                            switch (changeType)
                                            {
                                            case ChangeType.List_Add:
                                                //TODO
                                                if (!v.Contains(g))
                                                {
                                                    v.Add(g);
                                                }
                                                valChanged = true;
                                                ChangeLog.Add($"Added entry \"{g}\" to {r[0]}");
                                                break;
                                            case ChangeType.List_Remove:
                                                v.Remove(g);
                                                valChanged = true;
                                                ChangeLog.Add($"Removed entry \"{g}\" from {r[0]}");
                                                break;
                                            case ChangeType.None:
                                            case ChangeType.Keyed:
                                            default:
                                                //these shouldn't happen here
                                                System.Diagnostics.Debugger.Launch();
                                                System.Diagnostics.Debugger.Break();
                                                break;
                                            }
                                        }
                                        else
                                        {
                                            g = currType.GetMethod("Parse", new Type[] { typeof(string) }).Invoke(null, new object[] { val });
                                            f.SetValue(parent, g);
                                            valChanged = true;
                                            ChangeLog.Add($"Set {r[0]} to {g}");
                                        }
                                    }
                                    if (!valChanged)
                                    {
                                        System.Diagnostics.Debugger.Break();
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        //TODO ignore exceptions? 
                        System.Diagnostics.Debug.WriteLine(e);
                        System.Diagnostics.Debugger.Launch();
                        System.Diagnostics.Debugger.Break();
                    }
                }
                if (entries.Length != ChangeLog.Count)
                {
                    System.Diagnostics.Debugger.Launch();
                    System.Diagnostics.Debugger.Break();
                }
            }
        }
        public enum ChangeType
        {
            None = 0,
            Keyed,
            List_Add,
            List_Remove,
        }
        public class ChangeInfo
        {
            public readonly ChangeType ChangeType;
            public readonly string ContainerIdentifier;
            public readonly object Value;

            public ChangeInfo(ChangeType changeType, string containerIdentifier, object value)
            {
                ChangeType = changeType;
                ContainerIdentifier = containerIdentifier;
                Value = value;
            }
            public override string ToString()
            {
                return $"(Container: {ContainerIdentifier}, CurrentVal: {Value}, ChangeType: {ChangeType})";
            }
        }
        public class KeyedChangeInfo : ChangeInfo
        {
            public readonly object OldVal;

            public KeyedChangeInfo(string uniqueIdentifier, object currentVal, object oldVal, ChangeType changeType)
                : base(changeType, uniqueIdentifier, currentVal)
            {
                OldVal = oldVal;
            }

            public override string ToString()
            {
                return $"(Key: {ContainerIdentifier}, CurrentVal: {Value}, OldVal: {OldVal}, ChangeType: {ChangeType})";
            }
        }
    }
    internal sealed class SaveDataObserver
#if FEZCLIENT
: GameComponent
#endif
    {
        internal static readonly object saveDataLock = new object();
        public const string SAVE_DATA_IDENTIFIER_SEPARATOR = "\x1F";

#if FEZCLIENT
        private IGameStateManager GameState { get; set; }
        private IPlayerManager PM { get; set; }
#endif
        internal SaveData CurrentSaveData =>
#if FEZCLIENT
        GameState?.SaveData;
#else
        FezDedicatedServer.server.sharedSaveData;
#endif
        private readonly SaveData OldSaveData = new SaveData();

        public static SaveDataObserver Instance;

#if FEZCLIENT
        public SaveDataObserver(Game game) : base(game)
        {
            Instance = this;
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
#else
        public SaveDataObserver()
        {
            Instance = this;
        }
#endif
        internal static readonly SaveDataChanges newChanges = new SaveDataChanges();
        private static int lastSaveSlot = -1;
        private int CurrentSaveSlot =>
#if FEZCLIENT
        GameState?.SaveSlot ?? -1;
#else
        23;
#endif
        internal bool SaveSlotChanged
        {
            get
            {
                bool r = lastSaveSlot != CurrentSaveSlot;
                lastSaveSlot = CurrentSaveSlot;
                return r;
            }
        }
        
#if FEZCLIENT
        public override void Update(GameTime gameTime)
#else
        public void Update()
#endif
        {
            //check a save file is actually loaded 
            if (CurrentSaveData != null && CurrentSaveSlot >= 0
#if FEZCLIENT
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
                    && PM.CanControl && PM.Action != ActionType.None && !PM.Hidden
#endif
                    )
            {
                if (SaveSlotChanged)
                {
                    newChanges.ClearChanges();
                    CurrentSaveData.CloneInto(OldSaveData);
                }
                else
                {
                    lock (saveDataLock)
                    {
                        CheckType(newChanges, typeof(SaveData), "SaveData", CurrentSaveData, OldSaveData);
                    }
                    //update old data
                    CurrentSaveData.CloneInto(OldSaveData);
                }
            }
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
                    changes.AddKeyedChange(containerIdentifier + SAVE_DATA_IDENTIFIER_SEPARATOR + field.Name, currentVal, oldVal);
                }
            }
            else if (typeof(string).IsAssignableFrom(fieldType))
            {
                if (!string.Equals((string)currentVal, (string)oldVal, StringComparison.Ordinal))
                {
                    //value changed
                    changes.AddKeyedChange(containerIdentifier + SAVE_DATA_IDENTIFIER_SEPARATOR + field.Name, currentVal, oldVal);
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
                        changes.AddListChange(containerIdentifier + SAVE_DATA_IDENTIFIER_SEPARATOR + field.Name, addedVal, SaveDataChanges.ChangeType.List_Add);
                    });
                    removedVals.ForEach(removedVal =>
                    {
                            //TODO? idk if this ever actually happens, but should probably implement it somehow
                            changes.AddListChange(containerIdentifier + SAVE_DATA_IDENTIFIER_SEPARATOR + field.Name, removedVal, SaveDataChanges.ChangeType.List_Remove);
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

                string containerID = containerIdentifier + SAVE_DATA_IDENTIFIER_SEPARATOR + field.Name;
                onlyIncurrentVal_dictKeys.ForEach(k =>
                {
                    string containerID_withKey = containerID + SAVE_DATA_IDENTIFIER_SEPARATOR + k.ToString();
                    object val = currentVal_dict[k];
                    changes.AddKeyedChange(containerID_withKey, val, null);
                });
                onlyInoldVal_dictKeys.ForEach(k =>
                {
                    string containerID_withKey = containerID + SAVE_DATA_IDENTIFIER_SEPARATOR + k.ToString();
                    object val = oldVal_dict[k];
                    changes.AddKeyedChange(containerID_withKey, null, val);
                });
                // For keys that are present in both, check for value differences
                List<object> sharedKeys = currentVal_dictKeys.Intersect(oldVal_dictKeys).ToList();
                foreach (object key in sharedKeys)
                {
                    object value1 = currentVal_dict[key];
                    object value2 = oldVal_dict[key];
                    string containerID_withKey = containerID + SAVE_DATA_IDENTIFIER_SEPARATOR + key.ToString();
                    void ComparePrimitive()
                    {
                        if (!Equals(value1, value2))
                        {
                            // add changes to changes
                            changes.AddKeyedChange(containerID_withKey, value1, value2);
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
                System.Diagnostics.Debugger.Break();
                //TODO if this happens, handle the unhandled enumerable type
            }
            else
            {
                //It must be a non-enumerable object of some kind
                // handle non-enumerable object types like LevelSaveData and WinConditions
                CheckType(changes, fieldType, containerIdentifier + SAVE_DATA_IDENTIFIER_SEPARATOR + field.Name, currentVal, oldVal);
            }
        }
        private static readonly ConcurrentDictionary<Type, FieldInfo[]> cachedTypeFieldInfoMap = new ConcurrentDictionary<Type, FieldInfo[]>();
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
#pragma warning disable IDE0018 // Inline variable declaration
            FieldInfo[] fields;
#pragma warning restore IDE0018 // Inline variable declaration

            //Note: .NET has a built-in RuntimeTypeCache, so we might not even need cachedTypeFieldInfoMap
            if (!cachedTypeFieldInfoMap.TryGetValue(containingType, out fields))
            {
                fields = containingType.GetFields(BindingFlags.Public | BindingFlags.Instance);
                cachedTypeFieldInfoMap[containingType] = fields;
            }

            foreach (FieldInfo field in fields)
            {
                CheckField(changes, containerIdentifier, field, currentObject, oldObject);
            }
        }
    }

}

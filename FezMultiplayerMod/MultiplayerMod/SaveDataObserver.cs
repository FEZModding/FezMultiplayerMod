﻿using FezEngine.Components;
using FezEngine.Services;
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
                CheckType(newChanges, typeof(SaveData), "SaveData", CurrentSaveData, OldSaveData);

                //update old data
                CurrentSaveData.CloneInto(OldSaveData);

                //Notify listeners of changes
                if (newChanges.HasChanges)
                {
                    OnSaveDataChanged(CurrentSaveData, newChanges);
                }
            }
        }
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
                    changes.Add(containerIdentifier, field, currentVal, oldVal);
                }
            }
            else
            {
                if (typeof(string).IsAssignableFrom(fieldType))
                {
                    if (!string.Equals((string)currentVal, (string)oldVal, StringComparison.Ordinal))
                    {
                        //value changed
                        changes.Add(containerIdentifier, field, currentVal, oldVal);
                    }
                }
                // handle collections like Lists
                else if (typeof(IList).IsAssignableFrom(fieldType) || (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>)))
                {
                    //Note: all the List fields in SaveData are of value types (namely List<string> and List<ActorType>)
                    //Note: all the List fields in LevelSaveData are of value types (namely List<TrileEmplacement> and List<int>)
                    //Note: the only List fields in WinConditions is of value type List<int>
                    //This means none of the fields in SaveData or the types it contains have a List of non-value types

                    Console.Write(fieldType);
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
                            changes.Add(containerIdentifier, field, addedVal, null);
                        });
                        removedVals.ForEach(removedVal =>
                        {
                            //TODO? idk if this ever actually happens, but should probably implement it somehow
                            changes.Add(containerIdentifier, field, null, oldVal);
                            System.Diagnostics.Debugger.Launch();
                            System.Diagnostics.Debugger.Break();
                        });
                    }
                }
                // handle collections like Dictionaries
                else if (typeof(IDictionary).IsAssignableFrom(fieldType) || (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>)))
                {
                    //Note: SaveData contains fields of types Dictionary<string, bool> and Dictionary<string, LevelSaveData>
                    //Note: LevelSaveData contains fields of types Dictionary<int, int>

                    Console.Write(fieldType);

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
                        string containerID = containerIdentifier + IDENTIFIER_SEPARATOR + field.Name + $"[{k.ToString().Replace("[", "%5B").Replace("]", "%5D")}]";
                        object val = currentVal_dict[k];
                        changes.Add(containerID, val.GetType(), val, null);
                    });
                    onlyInoldVal_dictKeys.ForEach(k =>
                    {
                        string containerID = containerIdentifier + IDENTIFIER_SEPARATOR + field.Name + $"[{k.ToString().Replace("[", "%5B").Replace("]", "%5D")}]";
                        object val = oldVal_dict[k];
                        changes.Add(containerID, val.GetType(), null, val);
                    });
                    // For keys that are present in both, check for value differences
                    List<object> sharedKeys = currentVal_dictKeys.Intersect(oldVal_dictKeys).ToList();
                    foreach (object key in sharedKeys)
                    {
                        object value1 = currentVal_dict[key];
                        object value2 = oldVal_dict[key];
                        string containerID = containerIdentifier + IDENTIFIER_SEPARATOR + field.Name + $"[{key.ToString().Replace("[","%5B").Replace("]","%5D")}]";
                        void ComparePrimitive()
                        {
                            if (!Equals(value1, value2))
                            {
                                // add changes to changes
                                changes.Add(containerID, value1.GetType(), value1, value2);
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
                                CheckType(changes, valueType, containerID, value1, value2);
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
                    Console.Write(fieldType);
                    System.Diagnostics.Debug.WriteLine(fieldType);
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
        private static void CheckType(SaveDataChanges changes, Type containingType, string containerIdentifier, object currentObject, object oldObject)
        {
            FieldInfo[] fields = containingType.GetFields(BindingFlags.Public | BindingFlags.Instance);

            foreach (FieldInfo field in fields)
            {
                CheckField(changes, containerIdentifier, field, currentObject, oldObject);
            }
        }
        public class SaveDataChanges
        {
            public bool HasChanges => Changes.Any();

            //Note this collection should only have a single entry per unique containerIdentifier and field combo
            public readonly Dictionary<string, ChangeInfo> Changes = new Dictionary<string, ChangeInfo>();

            internal void Add(string containerIdentifier, FieldInfo field, object currentVal, object oldVal)
            {
                //Note this collection should only have a single entry per unique containerIdentifier and field combo
                string uniqueIdentifier = containerIdentifier + IDENTIFIER_SEPARATOR + field.Name;
                if (Changes.TryGetValue(uniqueIdentifier, out ChangeInfo change))
                {
                    Changes.Add(uniqueIdentifier, new ChangeInfo(containerIdentifier, field.Name, field.FieldType, currentVal, change.OldVal));
                }
                else
                {
                    Changes.Add(uniqueIdentifier, new ChangeInfo(containerIdentifier, field.Name, field.FieldType, currentVal, oldVal));
                }
            }
            internal void Add(string uniqueIdentifier, Type type, object currentVal, object oldVal)
            {
                //Note this collection should only have a single entry per unique containerIdentifier and field combo
                if (Changes.TryGetValue(uniqueIdentifier, out ChangeInfo change))
                {
                    Changes.Add(uniqueIdentifier, new ChangeInfo(uniqueIdentifier, null, type, currentVal, change.OldVal));
                }
                else
                {
                    Changes.Add(uniqueIdentifier, new ChangeInfo(uniqueIdentifier, null, type, currentVal, oldVal));
                }
            }

            public override string ToString()
            {
                return string.Join(Environment.NewLine, Changes);
            }

            public class ChangeInfo
            {
                public readonly string ContainerIdentifier;
                public readonly string FieldName;
                public readonly Type FieldType;
                public readonly object CurrentVal;
                public readonly object OldVal;

                public ChangeInfo(string containerIdentifier, string fieldName, Type fieldType, object currentVal, object oldVal)
                {
                    ContainerIdentifier = containerIdentifier;
                    FieldName = fieldName;
                    FieldType = fieldType;
                    CurrentVal = currentVal;
                    OldVal = oldVal;
                }

                public override string ToString()
                {
                    return $"(Container: {ContainerIdentifier}, FieldName: {FieldName}, FieldType: {FieldType}, CurrentVal: {CurrentVal}, OldVal: {OldVal})";
                }
            }
        }
    }

}

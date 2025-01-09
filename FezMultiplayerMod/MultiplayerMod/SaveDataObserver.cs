using FezEngine.Components;
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
    public sealed class SaveDataObserver : GameComponent
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
                SaveDataChanges newChanges = new SaveDataChanges();
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
                //TODO handle collections like Lists and Dictionaries
                else if (typeof(IList).IsAssignableFrom(fieldType) || (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>)))
                {
                    Console.Write(fieldType);
                    //TODO iterate through the entries checking values
                    //CheckType(changes, typeof(fieldType), containerIdentifier+IDENTIFIER_SEPARATOR+"List TODO", currentVal, oldVal);
                }
                else if (typeof(IDictionary).IsAssignableFrom(fieldType) || (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>)))
                {
                    Console.Write(fieldType);
                    //TODO iterate through the entries checking the keys and values
                    //CheckType(changes, typeof(fieldType), containerIdentifier+IDENTIFIER_SEPARATOR+"Dict TODO", currentVal, oldVal);
                }
                else if (typeof(IEnumerable).IsAssignableFrom(fieldType))
                {
                    Console.Write(fieldType);
                    System.Diagnostics.Debugger.Launch();
                    //TODO if this happens, handle the unhandled enumerable type
                }
                else
                {
                    //It must be a non-enumerable object of some kind
                    //TODO handle non-enumerable object types like LevelSaveData
                    //CheckType(changes, typeof(fieldType), containerIdentifier+IDENTIFIER_SEPARATOR+ typeof(fieldType).Name + " TODO", currentVal, oldVal);
                }
            }
        }
        private static bool debuggerWasAttached = false;
        private static void CheckType(SaveDataChanges changes, Type containingType, string containerIdentifier, object currentObject, object oldObject)
        {
            FieldInfo[] fields = containingType.GetFields(BindingFlags.Public | BindingFlags.Instance);

            IDictionary a = new Dictionary<string, bool>();

            #if DEBUG
            if (!debuggerWasAttached && !System.Diagnostics.Debugger.IsAttached)
            {
                System.Diagnostics.Debugger.Launch();
                debuggerWasAttached = true;
            }
            #endif
            foreach (FieldInfo field in fields)
            {
                CheckField(changes, containerIdentifier, field, currentObject, oldObject);
            }
        }
        public class SaveDataChanges
        {
            public bool HasChanges { get; private set; } = false;

            //Note this collection should only have a single entry per unique containerIdentifier and field combo
            public readonly Dictionary<string, ChangeInfo> Changes = new Dictionary<string, ChangeInfo>();

            internal void Add(string containerIdentifier, FieldInfo field, object currentVal, object oldVal)
            {
                HasChanges = true;
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

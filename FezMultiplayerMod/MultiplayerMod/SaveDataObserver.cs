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
    //TODO Implement this
    /// <summary>
    /// Uses format codes to stylize, format, and display text.
    /// See ANSI escape codes or ECMA-48
    /// </summary>
    /// <remarks>
    /// See ANSI escape codes or <a href="https://www.ecma-international.org/publications-and-standards/standards/ecma-48/">ECMA-48</a> for more formatting information.
    /// </remarks>
    public sealed class SaveDataObserver : GameComponent
    {
        private IGameStateManager GameState { get; set; }

        private SaveData CurrentSaveData => GameState?.SaveData;
        private readonly SaveData OldSaveData = new SaveData();

        public event Action<SaveData,SaveDataChanges> OnSaveDataChanged = (UpdatedSaveData, SaveDataChanges) => { };

        public SaveDataObserver(Game game) : base(game)
        {
            _ = Waiters.Wait(() =>
            {
                return ServiceHelper.FirstLoadDone;
            },
            () =>
            {
                GameState = ServiceHelper.Get<IGameStateManager>();
            });
        }
        public override void Update(GameTime gameTime)
        {
            //TODO check a save file is actually loaded
            if (CurrentSaveData != null)
            {
                void CheckField(SaveDataChanges changes, Type containingType, FieldInfo field, object currentObject, object oldObject)
                {
                    if (containingType is null)
                    {
                        throw new ArgumentNullException(nameof(containingType));
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
                            changes.Add(containingType, field, currentVal, oldVal);
                        }
                    }
                    else
                    {
                        if (typeof(string).IsAssignableFrom(fieldType))
                        {
                            if (!string.Equals((string)currentVal, (string)oldVal, StringComparison.Ordinal))
                            {
                                //value changed
                                changes.Add(containingType, field, currentVal, oldVal);
                            }
                        }
                        //TODO handle collections like Lists and Dictionaries
                        else if (typeof(IList).IsAssignableFrom(fieldType) || (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>)))
                        {
                            Console.Write(fieldType);
                            //TODO iterate through the entries checking values
                        }
                        else if (typeof(IDictionary).IsAssignableFrom(fieldType) || (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>)))
                        {
                            Console.Write(fieldType);
                            //TODO iterate through the entries checking the keys and values
                        }
                        //TODO handle other object types like LevelSaveData
                    }
                }
                void CheckType(SaveDataChanges changes, Type containingType, object currentObject, object oldObject)
                {
                    FieldInfo[] fields = containingType.GetFields(BindingFlags.Public | BindingFlags.Instance);

                    IDictionary a = new Dictionary<string, bool>();


                    System.Diagnostics.Debugger.Launch();
                    foreach (FieldInfo field in fields)
                    {
                        CheckField(changes, containingType, field, CurrentSaveData, OldSaveData);
                    }
                }
                SaveDataChanges newChanges = new SaveDataChanges();
                CheckType(newChanges, typeof(SaveData), CurrentSaveData, OldSaveData);

                //update old data
                CurrentSaveData.CloneInto(OldSaveData);

                //Notify listeners of changes
                if (newChanges.HasChanges)
                {
                    OnSaveDataChanged(CurrentSaveData, newChanges);
                }
            }
        }
    }

    public class SaveDataChanges
    {
        public bool HasChanges { get; private set; } = false;

        internal void Add(Type ContainingType, FieldInfo field, object currentVal, object oldVal)
        {
            HasChanges = true;
            //TODO add to a collection of some kind
            //throw new NotImplementedException();
        }
    }
}

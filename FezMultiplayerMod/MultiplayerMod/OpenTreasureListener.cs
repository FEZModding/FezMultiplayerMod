﻿using FezEngine.Components;
using FezEngine.Structure;
using FezEngine.Tools;
using FezGame.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using FezSharedTools;

namespace FezGame.MultiplayerMod
{
    internal class OpenTreasureListener : GameComponent
    {
        private Func<string> GetValues;
        private Func<string, object> GetValueByNameOrFail;
        private T GetCastedValueByNameOrDefault<T>(string name, T defaultValue)
        {
            try
            {
                object rawVal = GetValueByNameOrFail(name);
                return (T)rawVal;
            }
            catch
            {
                return defaultValue;
            }
        }
        private TimeSpan SinceActive => GetCastedValueByNameOrDefault<TimeSpan>("sinceActive", TimeSpan.Zero);
        private ActorType TreasureActorType => GetCastedValueByNameOrDefault<ActorType>("treasureActorType", ActorType.None);
        /// <summary>
        /// if the item collected is an art object (artifacts)
        /// <seealso cref="TreasureAoInstance"/>
        /// </summary>
        private bool TreasureIsAo => GetCastedValueByNameOrDefault<bool>("treasureIsAo", false);
        /// <summary>
        /// if the item collected is a map
        /// </summary>
        private bool TreasureIsMap => GetCastedValueByNameOrDefault<bool>("treasureIsMap", false);
        /// <summary>
        /// the chest the player is in front of
        /// </summary>
        private ArtObjectInstance ChestAO => GetCastedValueByNameOrDefault<ArtObjectInstance>("chestAO", null);
        /// <summary>
        /// the Trile for the item collected / spawned by the chest
        /// </summary>
        private TrileInstance TreasureInstance => GetCastedValueByNameOrDefault<TrileInstance>("treasureInstance", null);
        /// <summary>
        /// the Art Object for the item collected / spawned by the chest
        /// </summary>
        private ArtObjectInstance TreasureAoInstance => GetCastedValueByNameOrDefault<ArtObjectInstance>("treasureAoInstance", null);
        /**
         * <summary><para>
         * Note: when collecting maps, map name is stored in <br />
         * • <c>treasureAoInstance.ActorSettings.TreasureMapName</c> if it's that map in boilerroom,<br />
         * • or <c>chestAO.ActorSettings.TreasureMapName</c> if it's from a chest.
         * </para></summary>
         */
        private string TreasureMapName => ChestAO?.ActorSettings?.TreasureMapName ?? TreasureAoInstance?.ActorSettings?.TreasureMapName ?? null;

        public event Action<TreasureCollectionData> OnTreasureCollected = (data) => { };

        public OpenTreasureListener(Game game) : base(game)
        {
            Type openTreasureType = typeof(Fez).Assembly.GetType("FezGame.Components.Actions.OpenTreasure");

            var excludedTypes = new[] { "SoundEffect", "Texture2D", "Quaternion", "List", "Group", "Mesh" };
            var excludedNames = new[] { "^[Oo]ld", "^SinceCreated$", "^lastZoom$", "Service", "__BackingField",
                    "^treasureTrile$", "^treasureAo$"}.Select(s => new Regex(s));
            var fieldNameFieldMap = openTreasureType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(fieldInfo =>
            {
                string fieldType = fieldInfo.FieldType.Name.Split(new[] { '`', '[' })[0];
                return !excludedTypes.Contains(fieldType) && !excludedNames.Any(s => s.IsMatch(fieldInfo.Name));
            })
            .ToDictionary(
                fieldInfo => fieldInfo.Name,
                fieldInfo => fieldInfo
            );
            _ = Waiters.Wait(() => ServiceHelper.FirstLoadDone, () =>
            {
                object openTreasure = ServiceHelper.Game.Components.FirstOrDefault(c => c.GetType() == openTreasureType);
                GetValueByNameOrFail = (name) =>
                {
                    return fieldNameFieldMap[name].GetValue(openTreasure);
                };
                GetValues = () =>
                {
                    return String.Join("\n", fieldNameFieldMap.Select(p =>
                    {
                        return p.Key + ": " + p.Value.GetValue(openTreasure);
                    }));
                };
            });

            /*
             * unique IDs: 
             *   chestAO.Id,                                                      | ChestCount
             *   treasureAoInstance.Id,                                           | OtherCollectibleCount
             *   treasureInstance.OriginalEmplacement + treasureInstance.Foreign, | CubeShardCount or OtherCollectibleCount
             *   treasureActorType
             *   ForcedTreasure
             */

            //Note: internally in FEZ, upon collecting a treasure, the following occurs to update the level data:
            //if it's a chest, it runs
            /*
             * base.GameState.SaveData.ThisLevel.InactiveArtObjects.Add(chestAO.Id);
			 * base.GameState.SaveData.ThisLevel.FilledConditions.ChestCount++;
			 * ArtObjectService.OnTreasureOpened(chestAO.Id);
             *
             */
            //if it's that map in boilerroom, it runs
            /*
			 * base.GameState.SaveData.ThisLevel.InactiveArtObjects.Add(treasureAoInstance.Id);
			 * base.GameState.SaveData.ThisLevel.FilledConditions.OtherCollectibleCount++;
             *
             */
            //else it's a trile of some kind, and it runs
            /*
             * base.GameState.SaveData.ThisLevel.DestroyedTriles.Add(treasureInstance.OriginalEmplacement);
             * if (!treasureInstance.Foreign)
             * {
             *     if (treasureInstance.Trile.ActorSettings.Type == ActorType.CubeShard)
             *     {
             *         base.GameState.SaveData.ThisLevel.FilledConditions.CubeShardCount++;
             *     }
             *     else
             *     {
             *         base.GameState.SaveData.ThisLevel.FilledConditions.OtherCollectibleCount++;
             *     }
             * }
             * 
             */
            //after that, there's a switch statement that adds the relevant item to the item to the player's global inventory:
            /*
             * switch (treasureActorType)
             * SecretCube, CubeShard, PieceOfHeart, Keys, NumberCube, TriSkull, LetterCube, Tome, TreasureMap
             */

            //Note: when collecting CubeShard:
            /*
             * if (base.PlayerManager.ForcedTreasure != null)
             * {
             * 	base.GameState.SaveData.CollectedParts = 0;
             * }
             * 
             */

            //finally, the game removes the entity from the level:
            /* 
             * if (treasureIsAo) {
             *     LevelManager.ArtObjects.Remove(treasureAoInstance.Id);
             * } else if (!treasureIsMap && !treasureIsMail) { //it's a trile
             *     treasureInstance.Collected = true;
             *     base.LevelManager.UpdateInstance(treasureInstance);
             *     LevelManager.ClearTrile(treasureInstance);
             * }
             */
        }
        private TimeSpan? lastSinceActive = null;
        private bool IsActive = false;
        private bool LastIsActive = false;
        public override void Update(GameTime gameTime)
        {
            if (GetValues != null)
            {
                var sinceActive = SinceActive;
                if (!lastSinceActive.HasValue)
                {
                    lastSinceActive = sinceActive;
                }
                if (!ServiceHelper.Get<Services.IGameStateManager>().Paused)
                {
                    IsActive = lastSinceActive < sinceActive;
                    lastSinceActive = sinceActive;
                }
                if (IsActive != LastIsActive)
                {
                    if (IsActive)
                    {
                        TreasureCollectionData treasureCollectionData = new TreasureCollectionData(
                            type: TreasureIsAo ? TreasureType.Ao : (TreasureIsMap ? TreasureType.Map : TreasureType.Trile),
                            source: ServiceHelper.Get<Services.IPlayerManager>().ForcedTreasure != null ? TreasureSource.Forced
                                        : (ChestAO != null ? TreasureSource.Chest
                                                    : (TreasureIsMap ? TreasureSource.Map : TreasureSource.Trile)
                                        ),
                            treasureMapName: TreasureMapName,
                            treasureActorType: TreasureActorType,
                            artObjectId: ChestAO?.Id ?? TreasureAoInstance?.Id,
                            trileEmplacement: TreasureInstance?.OriginalEmplacement,
                            trileIsForeign: TreasureInstance?.Foreign
                        );
                        //Note: Foreign is for triles that get spawned in, like code cubes, heart cubes, clock cubes, and fork cubes.
                        OnTreasureCollected(treasureCollectionData);
                    }
                }
                LastIsActive = IsActive;
            }
        }
    }
}
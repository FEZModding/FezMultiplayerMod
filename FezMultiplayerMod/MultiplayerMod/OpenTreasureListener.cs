using FezEngine.Components;
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

namespace FezGame.MultiplayerMod
{
    internal class OpenTreasureListener : DrawableGameComponent
    {
        private readonly Hook ActHook;
        private Func<string> GetValues;
        private readonly SpriteBatch drawer;
        private IFontManager FontManager;
        public OpenTreasureListener(Game game) : base(game)
        {
            DrawOrder = int.MaxValue;
            Type OpenTreasureType = typeof(Fez).Assembly.GetType("FezGame.Components.Actions.OpenTreasure");

            MethodInfo ActMethod = OpenTreasureType.GetMethod("Act", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            ActHook = new Hook(
                    ActMethod,
                    new Func<Func<object, TimeSpan, bool>, object, TimeSpan, bool>((orig, self, elapsed) =>
                    {
                        return orig(self, elapsed);
                    })
                );
            ;
            var fieldNameTypeMap = new Dictionary<string, Type>(){
                {"sinceActive", typeof(TimeSpan)},
                {"chestAO", typeof(ArtObjectInstance)},
                {"treasureInstance", typeof(TrileInstance)},
                {"treasureAoInstance", typeof(ArtObjectInstance)},
                {"sinceCollect", typeof(TimeSpan)},
                {"treasureIsAo", typeof(bool)},
                {"treasureIsMap", typeof(bool)},
                {"treasureActorType", typeof(ActorType)}
            };
            var fieldNameFieldMap = fieldNameTypeMap.ToDictionary(p => p.Key, p =>
            {
                return OpenTreasureType.GetField(p.Key, BindingFlags.Instance | BindingFlags.NonPublic);
            });
            drawer = new SpriteBatch(GraphicsDevice);
            _ = Waiters.Wait(() => ServiceHelper.FirstLoadDone, () =>
            {
                FontManager = ServiceHelper.Get<IFontManager>();
                object OpenTreasure = ServiceHelper.Game.Components.FirstOrDefault(c => c.GetType() == OpenTreasureType);
                GetValues = () =>
                {
                    return String.Join("\n", fieldNameFieldMap.Select(p =>
                    {
                        return p.Key + ": " + p.Value.GetValue(OpenTreasure);
                    }));
                };
            });

            //TODO so something with this data, like send it to an event or something

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

            /*
             * Note: when collecting maps, map name is stored in 
             * treasureAoInstance.ActorSettings.TreasureMapName if it's that map in boilerroom, 
             * or chestAO.ActorSettings.TreasureMapName if it's from a chest.
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
             * } else { //it's a trile
             *     treasureInstance.Collected = true;
             *     base.LevelManager.UpdateInstance(treasureInstance);
             *     LevelManager.ClearTrile(treasureInstance);
             * }
             */
        }
        public override void Draw(GameTime gameTime)
        {
            if(GetValues != null)
            {
                string text = GetValues();
                drawer.Begin();

                //align to bottom
                Vector2 pos = Vector2.UnitY * (GraphicsDevice.Viewport.Height - FontManager.Big.MeasureString(text).Y * FontManager.BigFactor);
                
                drawer.DrawString(FontManager.Big, text, pos + Vector2.One, Color.Black, 0, Vector2.Zero, FontManager.BigFactor, SpriteEffects.None, 0);
                drawer.DrawString(FontManager.Big, text, pos, Color.White, 0, Vector2.Zero, FontManager.BigFactor, SpriteEffects.None, 0);
                drawer.End();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ActHook.Dispose();
            }
        }
    }
}
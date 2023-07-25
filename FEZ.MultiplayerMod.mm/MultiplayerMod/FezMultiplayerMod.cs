using FezEngine.Components;
using FezEngine.Services;
using FezEngine.Services.Scripting;
using FezEngine.Structure;
using FezEngine.Tools;
using FezGame.Components;
using FezGame.Services;
using FezGame.Structure;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Audio;
using FezEngine.Components.Scripting;
using FezEngine.Structure.Input;
using System.Net.Sockets;
using System.Text;
using System.Net;
using FezEngine;
using FezEngine.Effects;

namespace FezGame.MultiplayerMod
{
    /// <summary>
    /// This class mostly draws the other players to the screen
    /// </summary>
    public class FezMultiplayerMod : DrawableGameComponent
    {
        /// <summary>
        /// A string representing the current version of this class.
        /// </summary>
        public static readonly string Version = "0.0.3"
#if DEBUG
        + $" (debug build {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version})"
#endif
        ;//TODO add a version checker to check for new versions? (accessing the internet might trigger antivirus); see System.Net.WebClient.DownloadStringAsync

        /// <summary>
        /// This class is mainly so we can get text to display over everything else but still have the other players render on the correct layer. 
        /// </summary>
        private class DebugTextDrawer : DrawableGameComponent
        {
            private readonly FezMultiplayerMod mod;
            public string Text = "";
            public Color Color = Color.Gray;

            public DebugTextDrawer(Game game, FezMultiplayerMod mod)
                : base(game)
            {
                this.mod = mod;
                this.DrawOrder = int.MaxValue;
            }
            public override void Draw(GameTime gameTime)
            {
                if (mod == null || mod.drawer == null || mod.FontManager == null)
                {
                    return;
                }

                mod.drawer.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                mod.drawer.DrawString(mod.FontManager.Big, Text, Vector2.Zero, Color, 0f, Vector2.Zero, 2f, SpriteEffects.None, 0f);
                mod.drawer.End();
            }
        }

        #region ServiceDependencies
        [ServiceDependency]
        public IPlayerManager PlayerManager { private get; set; }

        [ServiceDependency]
        public IGameLevelManager LevelManager { private get; set; }

        [ServiceDependency]
        public IGameStateManager GameState { private get; set; }

        [ServiceDependency]
        public IGameCameraManager CameraManager { private get; set; }

        [ServiceDependency]
        public IFontManager FontManager { private get; set; }

        #endregion

        public static FezMultiplayerMod Instance;

        private readonly MultiplayerClient mp;
        public bool ShowDebugInfo = true;
        private readonly DebugTextDrawer debugTextDrawer;

        public FezMultiplayerMod(Game game)
            : base(game)
        {
            //Fez.SkipIntro = true;
            Instance = this;
            ServiceHelper.AddComponent(debugTextDrawer = new DebugTextDrawer(game, Instance), false);

            const string SettingsFilePath = "FezMultiplayerMod.ini";//TODO: probably should use an actual path instead of just the file name
            MultiplayerClientSettings settings = MultiplayerClientSettings.ReadSettingsFile(SettingsFilePath);
            mp = new MultiplayerClient(settings);
            MultiplayerClientSettings.WriteSettingsFile(SettingsFilePath, settings);

            drawer = new SpriteBatch(GraphicsDevice);
            mesh.AddFace(new Vector3(1f), new Vector3(0f, 0.25f, 0f), FaceOrientation.Front, centeredOnOrigin: true, doublesided: true);
        }

        private bool disposing = false;
        protected override void Dispose(bool disposing)
        {
            if (this.disposing)
                return;
            this.disposing = true;
            mp.Dispose();
            drawer.Dispose();

            base.Dispose();
        }

        private readonly SpriteBatch drawer;
        public override void Initialize()
        {
            DrawOrder = 9;//player gomez is at 9, bombs and trixel partical system and warp gates are at 10, black holes & liquids are at 50, split up cubes at 75, cloud shadows at 100
            //dunno if there's a good way to get the other players to appear on top of the water in first person mode
            LevelManager.LevelChanged += delegate
            {
                effect.ColorSwapMode = ((LevelManager.WaterType == LiquidType.Sewer) ? ColorSwapMode.Gameboy : ((LevelManager.WaterType == LiquidType.Lava) ? ColorSwapMode.VirtualBoy : (LevelManager.BlinkingAlpha ? ColorSwapMode.Cmyk : ColorSwapMode.None)));
            };
            ILightingPostProcess lpp = null;
            _ = Waiters.Wait(() => (lpp = ServiceHelper.Get<ILightingPostProcess>()) != null, () => lpp.DrawGeometryLights += PreDraw);
            DrawActionScheduler.Schedule(delegate
            {
                mesh.Effect = (effect = new GomezEffect());
            });
        }
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (!GameState.Paused)
            {
                mp.Update();
            }
        }

        private void PreDraw(GameTime gameTime)
        {
            if (!GameState.Loading && !PlayerManager.Hidden && !GameState.InFpsMode)
            {
                effect.Pass = LightingEffectPass.Pre;
                if (!PlayerManager.FullBright)
                {
                    base.GraphicsDevice.PrepareStencilWrite(StencilMask.Level);
                }
                else
                {
                    base.GraphicsDevice.PrepareStencilWrite(StencilMask.None);
                }
                foreach (var p in mp.Players.Values)
                {
                    DrawPlayer(p, gameTime, false);
                    mesh.Draw();
                }
                base.GraphicsDevice.PrepareStencilWrite(StencilMask.None);
                effect.Pass = LightingEffectPass.Main;
            }
        }

        public override void Draw(GameTime gameTime)
        {
            if (this.disposing)
            {
                return;
            }

#if DEBUG
            PlayerManager.CanRotate = true;
            LevelManager.Flat = false;
            GameState.SaveData.HasFPView = true;
#endif

            string s = "";
            if(mp.ErrorMessage != null)
            {
                debugTextDrawer.Color = Color.Red;
                s += $"{mp.ErrorMessage}\n";
            }
            s += $"ColorSwapMode: {effect.ColorSwapMode}\n";
            if (mp.Listening)
            {
                debugTextDrawer.Color = Color.Gray;
                drawer.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                foreach (var p in mp.Players.Values)
                {
                    if (ShowDebugInfo)
                    {
                        if (p.Uuid == mp.MyUuid)
                        {
                            s += "(you): ";
                        }
                        s += $"{p.Endpoint}, {p.Uuid}, {p.CurrentLevelName}, {p.Action}, {p.CameraViewpoint}, {p.Position}, {p.LastUpdateTimestamp}\n";
                    }
                    //draw other player to screen if in the same level
                    if (p.Uuid != mp.MyUuid && p.CurrentLevelName != null && p.CurrentLevelName.Length > 0 && p.CurrentLevelName == LevelManager.Name)
                    {
                        try
                        {
                            DrawPlayer(p, gameTime);
                        }
                        catch { }
                    }
                }
                drawer.End();
            }
            else
            {
                s += "Not connected";
            }
            debugTextDrawer.Text = s;
        }

        #region internal drawing stuff

        /// <summary>
        /// used for areas with colored filters (e.g., lava, sewer, cmy, etc.)
        /// </summary>
        private GomezEffect effect;
        private readonly Mesh mesh = new Mesh()
        {
            SamplerState = SamplerState.PointClamp
        };
        private TimeSpan sinceBackgroundChanged = TimeSpan.Zero;
        internal void DrawPlayer(MultiplayerClient.PlayerMetadata p, GameTime gameTime, bool doDraw = true)
        {
            #region adapted from GomezHost.Update
            if (GameState.Loading
                || GameState.InMap
                || GameState.Paused
                || (FezMath.AlmostEqual(PlayerManager.GomezOpacity, 0f) && CameraManager.Viewpoint != Viewpoint.Perspective)
                || p.Action == ActionType.None)
            {
                return;
            }
            //TODO fix the problem with different viewpoints; see p.CameraViewpoint
            AnimatedTexture animation = PlayerManager.GetAnimation(p.Action);
            if(animation.Offsets.Length < 0)
            {
                return;
            }
            int width = animation.Texture.Width;
            int height = animation.Texture.Height;
            int frame = p.AnimFrame;
            if(frame >= animation.Offsets.Length)
            {
                frame = 0;
            }
            if(frame < 0)
            {
                return;
            }
            Rectangle rectangle = animation.Offsets[frame];
            effect.Animation = animation.Texture;
            mesh.Texture = animation.Texture;
            mesh.FirstGroup.TextureMatrix.Set(new Matrix((float)rectangle.Width / (float)width, 0f, 0f, 0f, 0f, (float)rectangle.Height / (float)height, 0f, 0f, (float)rectangle.X / (float)width, (float)rectangle.Y / (float)height, 1f, 0f, 0f, 0f, 0f, 0f));
            bool playerinbackground = false;// PlayerManager.Background;
            /*if (lastBackground != playerinbackground && !p.Action.NoBackgroundDarkening())
            {
                sinceBackgroundChanged = TimeSpan.Zero;
                lastBackground = playerinbackground;
            }*/
            if (sinceBackgroundChanged.TotalSeconds < 1.0)
            {
                sinceBackgroundChanged += gameTime.ElapsedGameTime;
            }
            effect.Background = p.Action.NoBackgroundDarkening() ? 0f : FezMath.Saturate(playerinbackground ? ((float)sinceBackgroundChanged.TotalSeconds * 2f) : (1f - (float)sinceBackgroundChanged.TotalSeconds * 2f));
            mesh.Scale = new Vector3(animation.FrameWidth / 16f, animation.FrameHeight / 16f, 1f);
            mesh.Position = p.Position + GetPositionOffset(p, ref animation);
            #endregion
            #region adapted from GomezHost.DoDraw_Internal
            //if (GameState.StereoMode || LevelManager.Quantum)
            //{
            /*if (!CameraManager.Viewpoint.IsOrthographic() && CameraManager.LastViewpoint != 0)
            {
                mesh.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, CameraManager.LastViewpoint.ToPhi());
            }
            else*/
            {
                mesh.Rotation = CameraManager.Rotation;//always point the mesh at the camera so first person mode looks good
            }
            if (p.LookingDirection == HorizontalDirection.Left)
            {
                mesh.Rotation *= FezMath.QuaternionFromPhi((float)Math.PI);
            }
            //}
            //blinking
            /*if (p.Action == ActionType.Suffering || p.Action == ActionType.Sinking)
            {
                mesh.Material.Opacity = (float)FezMath.Saturate((Math.Sin(PlayerManager.BlinkSpeed * ((float)Math.PI * 2f) * 5f) + 0.5 - (double)(PlayerManager.BlinkSpeed * 1.25f)) * 2.0);
            }
            else
            {
                mesh.Material.Opacity = PlayerManager.GomezOpacity;
            }*/
            GraphicsDevice graphicsDevice = base.GraphicsDevice;
            //silhouette
            if (!p.Action.SkipSilhouette())
            {
                graphicsDevice.PrepareStencilRead(CompareFunction.Greater, StencilMask.NoSilhouette);
                mesh.DepthWrites = false;
                mesh.AlwaysOnTop = true;
                effect.Silhouette = true;
                if(doDraw) mesh.Draw();
            }
            if (!playerinbackground)
            {
                graphicsDevice.PrepareStencilRead(CompareFunction.Equal, StencilMask.Hole);
                mesh.AlwaysOnTop = true;
                mesh.DepthWrites = false;
                effect.Silhouette = false;
                if (doDraw) mesh.Draw();
            }
            //finally draw the mesh
            graphicsDevice.PrepareStencilWrite(StencilMask.Gomez);
            mesh.AlwaysOnTop = p.Action.NeedsAlwaysOnTop();
            mesh.DepthWrites = !GameState.InFpsMode;
            effect.Silhouette = false;
            if (doDraw) mesh.Draw();
            graphicsDevice.PrepareStencilWrite(StencilMask.None);
            #endregion
        }
        //Adapted from GomezHost.GetPositionOffset
        private Vector3 GetPositionOffset(MultiplayerClient.PlayerMetadata p, ref AnimatedTexture anim)
        {
            float playerSizeY = p.Action.IsCarry() ? (Enum.GetName(typeof(ActionType), p.Action).Contains("Heavy") ? 1.75f : 1.9375f) : 0.9375f;//numbers from PlayerManager.SyncCollisionSize
            float num = playerSizeY + ((p.Action.IsCarry() || p.Action == ActionType.ThrowingHeavy) ? (-2) : 0);
            Vector3 vector = (1f - num) / 2f * Vector3.UnitY;
            Vector2 vector2 = p.Action.GetOffset() / 16f;
            vector2.Y -= anim.PotOffset.Y / 64f;
            Viewpoint view = ((CameraManager.Viewpoint.IsOrthographic() || !CameraManager.ActionRunning) ? CameraManager.Viewpoint : CameraManager.LastViewpoint);
            return vector + (vector2.X * view.RightVector() * p.LookingDirection.Sign() + vector2.Y * Vector3.UnitY);
        }
        #endregion
    }
}
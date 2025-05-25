using FezEngine.Components;
using FezEngine.Services;
using FezEngine.Structure;
using FezEngine.Tools;
using FezGame.Components;
using FezGame.Services;
using FezGame.Structure;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using FezEngine.Structure.Input;
using FezEngine;
using FezEngine.Effects;
using FezSharedTools;

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
        public static readonly string Version = "0.6.0"
#if DEBUG
        + $" (debug build {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version})"
#endif
        ;//TODO add a version checker to check for new versions? (accessing the internet might trigger antivirus); see System.Net.WebClient.DownloadStringAsync

        /// <summary>
        /// This class is mainly so we can get text to display over everything else but still have the other players render on the correct layer. 
        /// </summary>
        private sealed class DebugTextDrawer : DrawableGameComponent
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
                float scale = 2f * (mod.FontManager.BigFactor / 2f);
                mod.drawer.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                mod.drawer.DrawString(mod.FontManager.Big, Text, Vector2.Zero, Color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                mod.drawer.End();

#if DEBUG
                mod.drawer.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                if (RichTextRenderer.testStrings.TryGetValue(mod.ShownFontTest, out string[] tests))
                {
                    RichTextRenderer.DrawString(mod.drawer, mod.FontManager, String.Join("\n", tests), new Vector2(50, 100), Color.White, Color.Transparent, mod.ShownFontTestScale);
                }
                mod.drawer.End();
#endif

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

        [ServiceDependency]
        public IKeyboardStateManager KeyboardState { private get; set; }
        [ServiceDependency]
        public IContentManagerProvider CMProvider { private get; set; }

        #endregion

        public static FezMultiplayerMod Instance;

        private readonly MultiplayerClient mp;
        public volatile bool ShowDebugInfo = true;
        private const Microsoft.Xna.Framework.Input.Keys ToggleMPDebug = Microsoft.Xna.Framework.Input.Keys.F3,
                        LeftAlt = Microsoft.Xna.Framework.Input.Keys.LeftAlt,
                        ToggleFontTest1 = Microsoft.Xna.Framework.Input.Keys.D1,
                        ToggleFontTest2 = Microsoft.Xna.Framework.Input.Keys.D2,
                        ToggleFontTest3 = Microsoft.Xna.Framework.Input.Keys.D3,
                        ScaleFontTestDown = Microsoft.Xna.Framework.Input.Keys.D4,
                        ScaleFontTestUp = Microsoft.Xna.Framework.Input.Keys.D5;
        private readonly DebugTextDrawer debugTextDrawer;

        public FezMultiplayerMod(Game game)
            : base(game)
        {
            //System.Diagnostics.Debugger.Launch();
            //Fez.SkipIntro = true;
            Instance = this;
            ServiceHelper.AddComponent(debugTextDrawer = new DebugTextDrawer(game, Instance), false);

            SaveDataObserver saveDataObserver;
            ServiceHelper.AddComponent(saveDataObserver = new SaveDataObserver(game));
            saveDataObserver.OnSaveDataChanged += SaveDataObserver_OnSaveDataChanged;

            ServiceHelper.AddComponent(new OpenTreasureListener(game));

            const string SettingsFilePath = "FezMultiplayerMod.ini";//TODO: probably should use an actual path instead of just the file name
            MultiplayerClientSettings settings = IniTools.ReadSettingsFile(SettingsFilePath, new MultiplayerClientSettings());
            mp = new MultiplayerClient(settings);
            IniTools.WriteSettingsFile(SettingsFilePath, settings);

            ServerListMenu serverListMenu;
            ServiceHelper.AddComponent(serverListMenu = new ServerListMenu(game, mp));
            serverListMenu.LoadServerSettings(settings);
            serverListMenu.OnServerListChange += (serverList =>
            {
                settings.ServerList.Clear();
                settings.ServerList.AddRange(serverList);
                IniTools.WriteSettingsFile(SettingsFilePath, settings);
            });

            drawer = new SpriteBatch(GraphicsDevice);
            mesh.AddFace(new Vector3(1f), new Vector3(0f, 0.25f, 0f), FaceOrientation.Front, centeredOnOrigin: true, doublesided: true);
        }

        private void SaveDataObserver_OnSaveDataChanged(SaveData UpdatedSaveData, SaveDataObserver.SaveDataChanges SaveDataChanges)
        {
            //TODO send SaveDataChanges to the server, wait until the update is sent to the server, then clear the changes
            System.Diagnostics.Debug.WriteLine("Save data updated at "+DateTime.UtcNow.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffffff'Z'"));
            System.Diagnostics.Debug.WriteLine(SaveDataChanges.ToString());
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
        private TextDrawer3D textDrawer;
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
            textDrawer = new TextDrawer3D(this.Game, FontManager);

            //IContentManagerProvider cmp = null;
            //_ = Waiters.Wait(() => (cmp = ServiceHelper.Get<IContentManagerProvider>()) != null, () => RichTextRenderer.LoadFonts(cmp));
            _ = RichTextRenderer.LoadFonts(CMProvider);

            DrawActionScheduler.Schedule(delegate
            {
                mesh.Effect = (effect = new GomezEffect());
            });

            KeyboardState.RegisterKey(ToggleMPDebug);
            KeyboardState.RegisterKey(LeftAlt);
            KeyboardState.RegisterKey(ToggleFontTest1);
            KeyboardState.RegisterKey(ToggleFontTest2);
            KeyboardState.RegisterKey(ToggleFontTest3);
            KeyboardState.RegisterKey(ScaleFontTestDown);
            KeyboardState.RegisterKey(ScaleFontTestUp);
        }
        private int ShownFontTest = 0;
        private float ShownFontTestScale = 0.95f;
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (KeyboardState.GetKeyState(ToggleMPDebug) == FezButtonState.Pressed)
            {
                ShowDebugInfo = !ShowDebugInfo;
            }
            if (KeyboardState.GetKeyState(LeftAlt) == FezButtonState.Down)
            {
                if (KeyboardState.GetKeyState(ToggleFontTest1) == FezButtonState.Pressed)
                {
                    ShownFontTest = ShownFontTest == 1 ? ShownFontTest = 0 : ShownFontTest = 1;
                }
                if (KeyboardState.GetKeyState(ToggleFontTest2) == FezButtonState.Pressed)
                {
                    ShownFontTest = ShownFontTest == 2 ? ShownFontTest = 0 : ShownFontTest = 2;
                }
                if (KeyboardState.GetKeyState(ToggleFontTest3) == FezButtonState.Pressed)
                {
                    ShownFontTest = ShownFontTest == 3 ? ShownFontTest = 0 : ShownFontTest = 3;
                }
                if (KeyboardState.GetKeyState(ScaleFontTestDown) == FezButtonState.Pressed)
                {
                    ShownFontTestScale -= 0.1f;
                }
                if (KeyboardState.GetKeyState(ScaleFontTestUp) == FezButtonState.Pressed)
                {
                    ShownFontTestScale += 0.1f;
                }
                ShownFontTestScale = MathHelper.Clamp(ShownFontTestScale, 0.45f, 2.55f);
            }
            try
            {
                mp.Update();
            }
            catch (VersionMismatchException e)
            {
                //Server replied with data that is for a different network version of FezMultiplayerMod
                //TODO tell the user the desired FezMultiplayerServer is using a network protocol that is incompatible with their version.
                throw e;
            }
            catch (System.IO.InvalidDataException e)
            {
                //Server replied with data that is not related to FezMultiplayerMod or data is malformed
                //TODO tell the user the IP endpoint provided is not a FezMultiplayerServer.
                throw e;
            }
            catch (System.IO.IOException e)
            {
                //Connection failed, data read error, connection timeout, connection terminated by server, etc.
                //TODO
                throw e;
            }
            catch (System.Net.Sockets.SocketException e)
            {
                //Connection refused
                //TODO
#if DEBUG
                if (!System.Diagnostics.Debugger.IsAttached)
                {
                    System.Diagnostics.Debugger.Launch();
                }
                System.Diagnostics.Debugger.Break();
#endif
                throw e;
            }
            catch (Exception e)
            {
#if DEBUG
                if (!System.Diagnostics.Debugger.IsAttached)
                {
                    System.Diagnostics.Debugger.Launch();
                }
                System.Diagnostics.Debugger.Break();
#endif
                throw e;//This should never happen
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
                try
                {
                    foreach (PlayerMetadata p in mp.Players.Values)
                    {
                        DrawPlayer(p, mp.GetPlayerName(p.Uuid), gameTime, false);
                        mesh.Draw();
                    }
                }
                catch (KeyNotFoundException)//this can happen if an item is removed by another thread while this thread is iterating over the items
                {
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
            if (ShowDebugInfo)
            {
                //s += $"GameTime:{gameTime.TotalGameTime.TotalSeconds}\n";
                //var kbd = Microsoft.Xna.Framework.Input.Keyboard.GetState();
                //s += "Keys pressed: " + String.Join(", ", kbd.GetPressedKeys()/*.Select(k => k.ToString())*/) + "\n";
                //var mouse = Microsoft.Xna.Framework.Input.Mouse.GetState();
                //s += $"Mouse state: X:{mouse.X}, Y:{mouse.Y}, Scroll:{mouse.ScrollWheelValue}, buttons:{String.Join(", ", new[] { mouse.LeftButton, mouse.RightButton, mouse.MiddleButton }.Select((mb,i) => new Object[] { mb == Microsoft.Xna.Framework.Input.ButtonState.Pressed, i }).Where(a=>(bool)a[0]).Select(a=>"M"+(1+(int)a[1])))}\n";
                var p = mp.MyPlayerMetadata;
                if (p != null)
                {
                    s += "(you): ";
                    s += $"{mp.MyAppearance.PlayerName}, "//{mp.MyUuid}, "
                        + $"{((p.CurrentLevelName == null || p.CurrentLevelName.Length == 0) ? "???" : p.CurrentLevelName)}, "
                        + $"{p.Action}, {p.CameraViewpoint}, "
                        + $"{p.Position.Round(3)}, "
                        + $"ping: {(mp.ConnectionLatencyUp + mp.ConnectionLatencyDown) / TimeSpan.TicksPerMillisecond}ms\n";
                }
            }
            if (mp.ErrorMessage != null)
            {
                debugTextDrawer.Color = Color.Red;
                s += $"{mp.ErrorMessage}\n";
            }
            switch (mp.ActiveConnectionState)
            {
            case ConnectionState.Connected:
                if (ShowDebugInfo)
                {
                    s += $"Connected players: \n";
                }
                debugTextDrawer.Color = Color.Gray;
                drawer.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                try
                {
                    //sort by distance so first person has correct draw order
                    IOrderedEnumerable<PlayerMetadata> players = mp.Players.Values.OrderByDescending(p => Vector3.Distance(mp.MyPlayerMetadata.Position, p.Position));
                    foreach (PlayerMetadata p in players)
                    {
                        string playerName = mp.GetPlayerName(p.Uuid);
                        if (ShowDebugInfo)
                        {
                            if (p.Uuid == mp.MyUuid)
                            {
                                s += "(you): ";
                            }
                            s += $"{playerName}, "// + p.Uuid + ", "//{Convert.ToBase64String(p.Uuid.ToByteArray()).TrimEnd('=')}, "
                                + $"{((p.CurrentLevelName == null || p.CurrentLevelName.Length == 0) ? "???" : p.CurrentLevelName)}, "
                                + $"{p.Action}, {p.CameraViewpoint}, "
                                + $"{p.Position.Round(3)}, {(DateTime.UtcNow.Ticks - p.LastUpdateTimestamp) / (float)TimeSpan.TicksPerSecond}\n";
                        }
                        //draw other player to screen if in the same level
                        if (p.Uuid != mp.MyUuid && p.CurrentLevelName != null && p.CurrentLevelName.Length > 0 && p.CurrentLevelName == LevelManager.Name)
                        {
                            try
                            {
                                DrawPlayer(p, playerName, gameTime);
                            }
                            catch (Exception e)
                            {
                                Common.Logger.Log("MultiplayerClientSettings", Common.LogSeverity.Warning, e.ToString());
                                Console.WriteLine("Warning: " + e);
#if DEBUG
                                System.Diagnostics.Debugger.Launch();
#endif
                            }
                        }
                    }
                }
                catch (KeyNotFoundException)//this can happen if an item is removed by another thread while this thread is iterating over the items
                {
                }
                drawer.End();
                break;
            case ConnectionState.Connecting:
                s += "Connecting to " + mp.RemoteEndpoint;
                break;
            case ConnectionState.Disconnected:
                s += "Not connected";
                break;
            default:
                break;
            }
            if (mp.FatalException != null)
            {
                if(!ShowingFatalException){
                    ShowingFatalException = true;
                    ShowingFatalExceptionStartTimestamp = gameTime.TotalGameTime;
#if DEBUG
                    System.Diagnostics.Debugger.Launch();
                    //TODO relay connection problems to the user more effectively
#endif
                }
                s += "\nWarning: " + mp.FatalException.Message;


                if ((gameTime.TotalGameTime - ShowingFatalExceptionStartTimestamp).TotalSeconds > 10.0f)
                {
                    mp.FatalException = null;
                    ShowingFatalException = false;
                }
            }
            debugTextDrawer.Text = s;
        }
        private bool ShowingFatalException = false;
        private TimeSpan ShowingFatalExceptionStartTimestamp = TimeSpan.Zero;

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
        const bool HideInFirstPerson = false;
        internal void DrawPlayer(PlayerMetadata p, string playerName, GameTime gameTime, bool doDraw = true)
        {
            ActionType pAction = p.Action;
            #region adapted from GomezHost.Update
            if (GameState.Loading
                || GameState.InMap
                || GameState.Paused
                || (HideInFirstPerson
                    && ((FezMath.AlmostEqual(PlayerManager.GomezOpacity, 0f) && CameraManager.Viewpoint != Viewpoint.Perspective)
                    || pAction == ActionType.None)
                )
            )
            {
                return;
            }
            //TODO fix the problem with climbing in different viewpoints; see p.CameraViewpoint
            HorizontalDirection LookingDir = p.LookingDirection;
            Viewpoint cameraViewpoint = CameraManager.Viewpoint.IsOrthographic() ? CameraManager.Viewpoint : CameraManager.LastViewpoint;
            if (cameraViewpoint.GetOpposite() == p.CameraViewpoint)
            {
                LookingDir = LookingDir.GetOpposite();
            }
            AnimatedTexture animation = PlayerManager.GetAnimation(pAction);
            if (animation.Offsets.Length < 0)
            {
                return;
            }
            int width = animation.Texture.Width;
            int height = animation.Texture.Height;
            int frame = p.AnimFrame;
            if (frame >= animation.Offsets.Length)
            {
                frame = 0;
            }
            if (frame < 0)
            {
                return;
            }
            Rectangle rectangle = animation.Offsets[frame];
            effect.Animation = animation.Texture;
            mesh.Texture = animation.Texture;
            mesh.FirstGroup.TextureMatrix.Set(new Matrix((float)rectangle.Width / (float)width, 0f, 0f, 0f, 0f, (float)rectangle.Height / (float)height, 0f, 0f, (float)rectangle.X / (float)width, (float)rectangle.Y / (float)height, 1f, 0f, 0f, 0f, 0f, 0f));
            bool playerinbackground = false;// PlayerManager.Background;
            /*if (lastBackground != playerinbackground && !pAction.NoBackgroundDarkening())
            {
                sinceBackgroundChanged = TimeSpan.Zero;
                lastBackground = playerinbackground;
            }*/
            if (sinceBackgroundChanged.TotalSeconds < 1.0)
            {
                sinceBackgroundChanged += gameTime.ElapsedGameTime;
            }
            effect.Background = pAction.NoBackgroundDarkening() ? 0f : FezMath.Saturate(playerinbackground ? ((float)sinceBackgroundChanged.TotalSeconds * 2f) : (1f - (float)sinceBackgroundChanged.TotalSeconds * 2f));
            mesh.Scale = new Vector3(animation.FrameWidth / 16f, animation.FrameHeight / 16f, 1f);

            mesh.Position = p.Position + GetPositionOffset(pAction, ref animation, LookingDir, cameraViewpoint);
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
            if (LookingDir == HorizontalDirection.Left)
            {
                mesh.Rotation *= FezMath.QuaternionFromPhi((float)Math.PI);
            }
            //}
            //blinking
            /*if (pAction == ActionType.Suffering || pAction == ActionType.Sinking)
            {
                mesh.Material.Opacity = (float)FezMath.Saturate((Math.Sin(PlayerManager.BlinkSpeed * ((float)Math.PI * 2f) * 5f) + 0.5 - (double)(PlayerManager.BlinkSpeed * 1.25f)) * 2.0);
            }
            else
            {
                mesh.Material.Opacity = PlayerManager.GomezOpacity;
            }*/
            mesh.Material.Opacity = 1;
            GraphicsDevice graphicsDevice = base.GraphicsDevice;
            //silhouette
            if (!pAction.SkipSilhouette())
            {
                graphicsDevice.PrepareStencilRead(CompareFunction.Greater, StencilMask.NoSilhouette);
                mesh.DepthWrites = false;
                mesh.AlwaysOnTop = true;
                effect.Silhouette = true;
                if (doDraw) mesh.Draw();
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
            mesh.AlwaysOnTop = pAction.NeedsAlwaysOnTop();
            mesh.DepthWrites = !GameState.InFpsMode;
            effect.Silhouette = false;
            if (doDraw) mesh.Draw();
            graphicsDevice.PrepareStencilWrite(StencilMask.None);
            #endregion

            #region draw player name
            Vector3 namePos = p.Position + Vector3.Up * 1.35f;//center text over player 
            //TODO: sanitize player name because the game's font doesn't have every character
            textDrawer.DrawPlayerName(GraphicsDevice, playerName, namePos, CameraManager.Rotation, mesh.DepthWrites, 2, GraphicsDevice.GetViewScale() / 32f / 1.5f, 0.35f);
            #endregion
        }
        //Adapted from GomezHost.GetPositionOffset
        private Vector3 GetPositionOffset(ActionType pAction, ref AnimatedTexture anim, HorizontalDirection LookingDir, Viewpoint view)
        {
            float playerSizeY = pAction.IsCarry() ? (Enum.GetName(typeof(ActionType), pAction).Contains("Heavy") ? 1.75f : 1.9375f) : 0.9375f;//numbers from PlayerManager.SyncCollisionSize
            float num = playerSizeY + ((pAction.IsCarry() || pAction == ActionType.ThrowingHeavy) ? (-2) : 0);
            Vector3 vector = (1f - num) / 2f * Vector3.UnitY;
            Vector2 vector2 = pAction.GetOffset() / 16f;
            vector2.Y -= anim.PotOffset.Y / 64f;
            return vector + (vector2.X * view.RightVector() * LookingDir.Sign() + vector2.Y * Vector3.UnitY);
        }
        #endregion
    }
}
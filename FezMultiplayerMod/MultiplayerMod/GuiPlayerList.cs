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
    internal class GuiPlayerList : DrawableGameComponent
    {
        #region ServiceDependencies
        private IInputManager InputManager { get; set; }
        private IFontManager FontManager { get; set; }
        private IKeyboardStateManager KeyboardState { get; set; }

        #endregion

        private MultiplayerClient mp;
        public GuiPlayerList(Game game, MultiplayerClient mp)
            : base(game)
        {
            this.mp = mp;
            drawer = new SpriteBatch(GraphicsDevice);

            _ = Waiters.Wait(() =>
            {
                return ServiceHelper.FirstLoadDone;
            },
            () =>
            {
                InputManager = ServiceHelper.Get<IInputManager>();
                FontManager = ServiceHelper.Get<IFontManager>();
            });
        }

        private bool disposing = false;
        protected override void Dispose(bool disposing)
        {
            if (this.disposing)
                return;
            this.disposing = true;
            drawer.Dispose();

            base.Dispose();
        }

        private readonly SpriteBatch drawer;
        public override void Initialize()
        {
            DrawOrder = 9;

            SharedTools.OnLogWarning += (text, severity) =>
            {
                //TODO
            };
        }
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if(!ServiceHelper.FirstLoadDone)
            {
                return;
            }
            Visible = InputManager.ClampLook.IsDown();
        }

        public override void Draw(GameTime gameTime)
        {
            if (this.disposing || !Visible || !ServiceHelper.FirstLoadDone)
            {
                return;
            }

            List<string> lines = new List<string>();
            var me = mp.MyPlayerMetadata;
            if (me != null)
            {
                lines.Add($"(you): {mp.MyAppearance.PlayerName}, "//{mp.MyUuid}, "
                    + $"{((me.CurrentLevelName == null || me.CurrentLevelName.Length == 0) ? "???" : me.CurrentLevelName)}, "
                    + $"{me.Action}, {me.CameraViewpoint}, "
                    + $"{me.Position.Round(3)}, "
                    + $"ping: {(mp.ConnectionLatencyUpDown) / TimeSpan.TicksPerMillisecond}ms");
            }
            string connectionStatusText = "";
            if (mp.ExtraMessage != null)
            {
                connectionStatusText += $"{mp.ExtraMessage}\n";
            }
            if (mp.ErrorMessage != null)
            {
                connectionStatusText += $"{mp.ErrorMessage}\n";
            }
            lines.Add(connectionStatusText);

            //TODO draw player data as a table 
            switch (mp.ActiveConnectionState)
            {
            case ConnectionState.Connected:
                try
                {
                    //sort players
                    IOrderedEnumerable<PlayerMetadata> players = mp.Players.Values.OrderByDescending(p => p.Uuid);
                    foreach (PlayerMetadata p in players)
                    {
                        string playerName = mp.GetPlayerName(p.Uuid);
                        string s = "";
                        if (p.Uuid == mp.MyUuid)
                        {
                            s += "(you): ";
                        }
                        s += $"{playerName}, "
                            + $"{((p.CurrentLevelName == null || p.CurrentLevelName.Length == 0) ? "???" : p.CurrentLevelName)}, "
                            + $"{p.Action}, {p.CameraViewpoint}, "
                            + $"{p.Position.Round(3)}, {(DateTime.UtcNow.Ticks - p.LastUpdateTimestamp) / TimeSpan.TicksPerMillisecond}\n";
                        lines.Add(s);
                    }
                }
                catch (KeyNotFoundException)//this can happen if an item is removed by another thread while this thread is iterating over the items
                {
                }
                break;
            case ConnectionState.Connecting:
                lines.Add("Connecting to " + mp.RemoteEndpoint);
                break;
            case ConnectionState.Disconnected:
                lines.Add("Not connected");
                break;
            default:
                break;
            }
            if (mp.FatalException != null)
            {
                if (!ShowingFatalException)
                {
                    ShowingFatalException = true;
                    ShowingFatalExceptionStartTimestamp = gameTime.TotalGameTime;
#if DEBUG
                    System.Diagnostics.Debugger.Launch();
                    //TODO relay connection problems to the user more effectively
#endif
                }
                lines.Add("Warning: " + mp.FatalException.Message);


                if ((gameTime.TotalGameTime - ShowingFatalExceptionStartTimestamp).TotalSeconds > 5.0f)
                {
                    mp.FatalException = null;
                    ShowingFatalException = false;
                }
            }
            float scale = 1f;
            drawer.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            Vector2 origin = Vector2.Zero;
            foreach (string line in lines)
            {
                Vector2 lineSize = RichTextRenderer.MeasureString(FontManager, line) * scale;
                origin.X = (GraphicsDevice.Viewport.Width - lineSize.X) / 2f;
                drawer.DrawTextRichShadow(FontManager, line, origin, scale);
                origin.Y += lineSize.Y;
            }
            drawer.End();
        }
        private bool ShowingFatalException = false;
        private TimeSpan ShowingFatalExceptionStartTimestamp = TimeSpan.Zero;
    }
}
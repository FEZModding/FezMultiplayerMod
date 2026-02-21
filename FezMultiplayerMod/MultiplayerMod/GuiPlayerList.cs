using FezEngine.Components;
using FezEngine.Structure.Input;
using FezEngine.Tools;
using FezSharedTools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

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

        #endregion

        public Color BackgroundColor = new Color(Color.Black, 0.5f);
        private readonly Dictionary<string, Func<PlayerMetadata, string>> Columns;
        private readonly Dictionary<string, Func<float>> MaxWidths;
        private readonly Dictionary<string, Func<float>> FixedWidths;
        private readonly Dictionary<string, Func<int>> MaxCharWidths;
        private readonly MultiplayerClient mp;
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

            Columns = new Dictionary<string, Func<PlayerMetadata, string>>()
            {
                {"Name", p => mp.GetPlayerName(p.Uuid)},
                {"Level", p => (p.CurrentLevelName == null || p.CurrentLevelName.Length == 0) ? "???" : p.CurrentLevelName },
                {"Action", p => p.Action.ToString() },
                {"Viewpoint", p => p.CameraViewpoint.ToString() },
                {"Position", p => p.Position.Round(3).ToString() },
                {"Ping", p => ((DateTime.UtcNow.Ticks - p.LastUpdateTimestamp) / TimeSpan.TicksPerMillisecond).ToString() },
            };
            MaxCharWidths = new Dictionary<string, Func<int>>()
            {
                {"Name", () => FezMultiplayerBinaryIOExtensions.MaxPlayerNameLength},
                {"Level", () => FezMultiplayerBinaryIOExtensions.MaxLevelNameLength},
            };
            MaxWidths = new Dictionary<string, Func<float>>()
            {
                {"Action", () => RichTextRenderer.MeasureString(FontManager, "Action").X},
                {"Viewpoint", () => float.MaxValue },
            };
            FixedWidths = new Dictionary<string, Func<float>>()
            {
                {"Position", () => RichTextRenderer.MeasureString(FontManager, new Vector3(1/3f).Round(3).ToString()).X },
                {"Ping", () => RichTextRenderer.MeasureString(FontManager, "0000").X },
            };
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
            DrawOrder = 102000000;

            SharedTools.OnLogWarning += (text, severity) =>
            {
                //TODO
            };
        }
        private float scrollY = 0;
        private float minScrollY = 0;
        private float maxScrollY = float.PositiveInfinity;
        private const float ScrollSpeedModifierY = 18;
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (!ServiceHelper.FirstLoadDone)
            {
                return;
            }
            Visible = InputManager.ClampLook.IsDown();
            if (Visible && mp.ActiveConnectionState == ConnectionState.Connected)
            {
                float scrollChange = //MathHelper.Clamp(
                    ScrollSpeedModifierY * (InputManager.Movement.Y
                    + InputManager.FreeLook.Y
                    + (InputManager.MapZoomIn.IsDown() ? 1 : 0)
                    + (InputManager.MapZoomOut.IsDown() ? -1 : 0))
                    * (InputManager.CancelTalk.IsDown() ? 3 : 1)
                //, -1, 1)
                ;
                if (scrollChange != 0)
                {
                    scrollY = MathHelper.Clamp(scrollY + scrollChange, minScrollY, maxScrollY);
                }
            }
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
            lines.Add(scrollY.ToString());
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

            bool doDrawPlayerTable = false;
            switch (mp.ActiveConnectionState)
            {
            case ConnectionState.Connected:
                doDrawPlayerTable = true;
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
            if (doDrawPlayerTable)
            {
                // draw player data as a table 
                try
                {
                    //sort players
                    var players = mp.Players.Values.OrderByDescending(p => p.Uuid);

                    const int borderWidth = 1;

                    string[] headers = Columns.Keys.ToArray();
                    int myRow = -1;
                    List<string[]> rows = players.Select((p, i) =>
                    {
                        if (myRow == -1 && p.Uuid == mp.MyUuid)
                        {
                            myRow = i + 1;
                        }
                        return Columns.Select(c => c.Value(p)).ToArray();
                    }).ToList();
                    rows.Insert(0, headers);
                    float emSize = RichTextRenderer.MeasureString(FontManager, "M").X;
                    float lineHeight = RichTextRenderer.MeasureString(FontManager, "M").Y;

                    float paddingBlock = Math.Max(1f, lineHeight * 0.1f);
                    float paddingInline = Math.Max(1f, emSize * 0.2f);

                    //calculate cell sizes
                    float cellHeight = lineHeight;
                    float[] cellWidths = Enumerable.Range(0, Columns.Count)
                        .Select(i =>
                        {
                            string h = headers[i];
                            float colMaxWidth = float.MaxValue;
                            float colMinWidth = 0f;
                            if (MaxWidths.TryGetValue(h, out var f))
                            {
                                colMaxWidth = f();
                            }
                            else if (MaxCharWidths.TryGetValue(h, out var cf))
                            {
                                colMaxWidth = cf() * emSize;
                            }
                            else if (FixedWidths.TryGetValue(h, out var ff))
                            {
                                return ff();
                            }
                            // Measure the widths of all elements in column i
                            var columnWidths = rows.Select(r => RichTextRenderer.MeasureString(FontManager, r.ElementAt(i)).X);
                            var maxContentWidth = columnWidths.Max();
                            // Clamp the width between colMinWidth and colMaxWidth
                            var columnWidth = MathHelper.Clamp(maxContentWidth, colMinWidth, colMaxWidth);
                            return columnWidth;
                        }
                        ).ToArray();
                    ;
                    float totalWidth = cellWidths.Sum() + Columns.Count * (borderWidth + 2 * paddingInline);
                    float totalHeight = rows.Count * (cellHeight + borderWidth + 2 * paddingBlock);

                    float viewportHeight = GraphicsDevice.Viewport.Height;
                    float viewportWidth = GraphicsDevice.Viewport.Width;
                    float leftEdge = (viewportWidth - totalWidth) / 2f;
                    float topEdgeOffset = cellHeight * 3;
                    float topEdge = topEdgeOffset + scrollY;
                    origin = new Vector2(leftEdge, topEdge);
                    drawer.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                    drawer.DrawRect(origin, totalWidth, totalHeight, BackgroundColor);
                    drawer.End();
                    minScrollY = cellHeight - (topEdgeOffset + totalHeight);
                    maxScrollY = viewportHeight - cellHeight - topEdgeOffset;

                    //foreach (var v in cellVals)
                    //{
                    //    if (p.Uuid == mp.MyUuid)
                    //    {
                    //        s += "(you): ";
                    //    }
                    //}
                    //draw cells
                    int ri = 0;
                    rows.ForEach(r =>
                    {
                        origin.X = leftEdge;
                        origin.Y += paddingBlock;
                        if (ri == myRow)
                        {
                            const string youIdentifierText = "(you): ";
                            float offX = RichTextRenderer.MeasureString(FontManager, youIdentifierText).X;
                            origin.X = leftEdge - offX;
                            drawer.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                            RichTextRenderer.DrawString(drawer, FontManager, youIdentifierText, origin, Color.White);
                            drawer.End();
                            origin.X = leftEdge;
                        }
                        for (int i = 0; i < r.Length; ++i)
                        {
                            string text = r[i];
                            var boxTextClipRect = new Rectangle(
                                (int)origin.X,
                                (int)(origin.Y - paddingBlock),
                                (int)Math.Ceiling(cellWidths[i] + 2 * paddingInline),
                                (int)Math.Ceiling(cellHeight + 2 * paddingBlock)
                            );
                            origin.X += paddingInline;
                            GraphicsDevice.ScissorRectangle = boxTextClipRect;
                            RasterizerState scissorState = new RasterizerState
                            {
                                ScissorTestEnable = true
                            };
                            drawer.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, scissorState);
                            RichTextRenderer.DrawString(drawer, FontManager, text, origin, Color.White);
                            drawer.End();
                            origin.X += cellWidths[i];
                            origin.X += paddingInline;
                        }
                        origin.Y += cellHeight;
                        origin.Y += paddingBlock;
                        ++ri;
                    });
                }
                catch (KeyNotFoundException)//this can happen if an item is removed by another thread while this thread is iterating over the items
                {
                }
            }
        }
        private bool ShowingFatalException = false;
        private TimeSpan ShowingFatalExceptionStartTimestamp = TimeSpan.Zero;
    }
}
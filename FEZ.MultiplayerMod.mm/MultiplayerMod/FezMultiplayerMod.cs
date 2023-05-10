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

namespace FezGame.MultiplayerMod
{
    public class FezMultiplayerMod : DrawableGameComponent
    {
        /// <summary>
        /// A string representing the current version of this class.
        /// </summary>
        public static readonly string Version = "0.0.1"
#if DEBUG
        + $" (debug build {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version})"
#endif
        ;//TODO add a version checker to check for new versions? (accessing the internet might trigger antivirus); see System.Net.WebClient.DownloadStringAsync

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

        private MultiplayerClient mp;

        public FezMultiplayerMod(Game game)
            : base(game)
        {
            Instance = this;
            mp = new MultiplayerClient(game);
            drawer = new SpriteBatch(GraphicsDevice);
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
            DrawOrder = 4000;
        }
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (!GameState.Paused)
            {
                mp.Update(gameTime);
            }
        }

        public override void Draw(GameTime gameTime)
        {
            if(this.disposing)
            {
                return;
            }
            drawer.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            string s = "";
            foreach(var p in mp.Players.Values)
            {
                if(p.uuid == mp.MyUuid)
                {
                    s += "(you): ";
                }
                s += $"{p.endpoint}, {p.uuid}, {p.currentLevelName}, {p.action}, {p.position}, {p.lastUpdateTimestamp}\n";
                //draw other player to screen if in the same level
                if(p.uuid != mp.MyUuid && p.currentLevelName != null && p.currentLevelName.Length > 0 && p.currentLevelName == LevelManager.Name)
                {
                    //TODO actually draw the players on the screen 
                }
            }
            drawer.DrawString(FontManager.Big, s, Vector2.Zero, Color.Gray, 0f, Vector2.Zero, 2f, SpriteEffects.None, 0f);
            drawer.End();
        }
    }
}
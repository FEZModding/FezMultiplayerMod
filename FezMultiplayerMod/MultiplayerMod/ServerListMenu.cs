using FezEngine.Components;
using FezEngine.Services;
using FezEngine.Structure.Input;
using FezEngine.Tools;
using FezGame.Services;
using FezGame.Structure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;

namespace FezGame.MultiplayerMod
{
    internal sealed class ServerListMenu : DrawableGameComponent
    {
        public static ServerListMenu Instance;
        private readonly SpriteBatch drawer;
        private IKeyboardStateManager KeyboardState { get; set; }
        private IContentManagerProvider CMProvider { get; set; }
        private IInputManager InputManager { get; set; }
        private IGameStateManager GameState { get; set; }
        private IFontManager Fonts { get; set; }
        private ISoundManager SoundManager { get; set; }

        public ServerListMenu(Game game) : base(game)
        {
            DrawOrder = 2300;
            Instance = this;
            drawer = new SpriteBatch(GraphicsDevice);
            _ = Waiters.Wait(() =>
            {
                return ServiceHelper.FirstLoadDone;
            },
            () =>
            {
                KeyboardState = ServiceHelper.Get<IKeyboardStateManager>();
                CMProvider = ServiceHelper.Get<IContentManagerProvider>();
                InputManager = ServiceHelper.Get<IInputManager>();
                GameState = ServiceHelper.Get<IGameStateManager>();
                Fonts = ServiceHelper.Get<IFontManager>();
                SoundManager = ServiceHelper.Get<ISoundManager>();
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

        private class ServerInfo
        {
            public string Name;
            public readonly IPEndPoint Endpoint;
            public ServerInfo(string text, IPEndPoint endpoint)
            {
                this.Name = text;
                this.Endpoint = endpoint;
            }
        }
        private static readonly List<ServerInfo> ServerInfoList = new List<ServerInfo>();
        private static readonly ConcurrentBag<ServerInfo> LANServerInfoList = new ConcurrentBag<ServerInfo>();
        private class MenuListOption
        {
            public string DisplayText;
            public readonly Action Action;
            public MenuListOption(string text, Action action)
            {
                this.DisplayText = text;
                this.Action = action;
            }
        }

        private static readonly MenuListOption OptionAdd = new MenuListOption("Add", SelectAdd);
        private static readonly MenuListOption OptionBack = new MenuListOption("Back", SelectBack);

        private static IEnumerable<MenuListOption> ServerList => ServerInfoList.Concat(LANServerInfoList.OrderBy(s => s.Name))
                        .Select(info => new MenuListOption(info.Name, () => SelectServer(info)));
        private static List<MenuListOption> GetListOptions()
        {
            List<MenuListOption> list = new List<MenuListOption>();
            list.Add(OptionAdd);
            list.AddRange(ServerList);
            list.Add(OptionBack);
            return list;
        }

        private static void SelectAdd()
        {
            //TODO
        }
        private static void SelectBack()
        {
            //TODO
        }
        private static void SelectServer(ServerInfo info)
        {
            //TODO
        }

        private static List<MenuListOption> cachedMenuListOptions = null;
        private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(60);
        private static TimeSpan SinceLastUpdateList = UpdateInterval;

        private static bool hasFocus = true;
        private static int currentIndex = 0;
        public override void Update(GameTime gameTime)
        {
            if(!ServiceHelper.FirstLoadDone)
            {
                return;
            }
            SinceLastUpdateList += gameTime.ElapsedGameTime;
            if(SinceLastUpdateList > UpdateInterval)
            {
                cachedMenuListOptions = GetListOptions();
                SinceLastUpdateList = TimeSpan.Zero;
            }
            //TODO set hasFocus somewhere
            if (hasFocus)
            {
                if (InputManager.Up == FezButtonState.Pressed)
                {
                    if(currentIndex > 0)
                    {
                        //sCursorUp.Emit();
                        currentIndex--;
                    }
                }
                if (InputManager.Down == FezButtonState.Pressed)
                {
                    if (currentIndex < cachedMenuListOptions.Count - 1)
                    {
                        //sCursorUp.Emit();
                        currentIndex++;
                    }
                }
                if ((InputManager.CancelTalk == FezButtonState.Pressed) || InputManager.Back == FezButtonState.Pressed)
                {
                    OptionBack.Action.Invoke();
                }
                if (InputManager.Jump == FezButtonState.Pressed || InputManager.Start == FezButtonState.Pressed)
                {
                    cachedMenuListOptions.ElementAt(currentIndex).Action.Invoke();
                }
            }
        }
        public override void Draw(GameTime gameTime)
        {
            if (hasFocus && cachedMenuListOptions != null && drawer != null)
            {
                drawer.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                Vector2 position = Vector2.Zero;
                int i = 0;
                foreach(var option in cachedMenuListOptions)
                {
                    bool selected = i == currentIndex;
                    string text = $"{(selected ? ">" : " ")} {option.DisplayText} {(selected ? "<" : " ")}";
                    Vector2 lineSize = RichTextRenderer.MeasureString(Fonts, text);
                    RichTextRenderer.DrawString(drawer, Fonts, text, position, Color.White);
                    position.Y += lineSize.Y;
                    ++i;
                }
                drawer.End();
            }
        }
    }
}

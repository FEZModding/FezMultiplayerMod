﻿using FezEngine.Components;
using FezEngine.Services;
using FezEngine.Structure.Input;
using FezEngine.Tools;
using FezGame.Services;
using FezGame.Structure;
using FezSharedTools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;

namespace FezGame.MultiplayerMod
{
    internal sealed class ServerListMenu : DrawableGameComponent
    {
        public class ServerInfo
        {
            public string Name;
            public readonly IPEndPoint Endpoint;
            public ServerInfo(string text, IPEndPoint endpoint)
            {
                this.Name = text;
                this.Endpoint = endpoint;
            }
        }
        private class LANServerInfo : ServerInfo
        {
            public long lastUpdate;
            public LANServerInfo(string text, IPEndPoint endpoint) : base(text, endpoint)
            {
            }
        }
        private class MenuListOption
        {
            public string DisplayText;
            public readonly Action Action;
            public bool Enabled = true;
            public MenuListOption(string text, Action action)
            {
                this.DisplayText = text;
                this.Action = action;
            }
        }

        #region Service dependencies
        private IKeyboardStateManager KeyboardState { get; set; }
        private IContentManagerProvider CMProvider { get; set; }
        private IInputManager InputManager { get; set; }
        private IGameStateManager GameState { get; set; }
        private IFontManager Fonts { get; set; }
        private ISoundManager SoundManager { get; set; }
        #endregion

        public static ServerListMenu Instance;
        private readonly SpriteBatch drawer;
        private readonly ServerDiscoverer serverDiscoverer;

        private readonly List<ServerInfo> ServerInfoList = new List<ServerInfo>();
        private readonly ConcurrentDictionary<IPEndPoint, LANServerInfo> LANServerInfoList = new ConcurrentDictionary<IPEndPoint, LANServerInfo>();
        private readonly MultiplayerClient mp;

        private readonly MenuListOption OptionAdd;
        private readonly MenuListOption OptionRemove;
        private readonly MenuListOption OptionBack;
        private readonly MenuListOption OptionJoin;
        private readonly MenuListOption OptionDisconnect;
        private readonly MenuListOption OptionRefreshLAN;

        /// <summary>
        /// TODO call this with <c><see cref="ServerInfoList"/>.AsReadOnly()</c> when <see cref="ServerInfoList"/> changes <br />
        /// Called when the user adds or removes a server from the server list. <br />
        /// Supplies the new server list, and does not include LAN servers.
        /// </summary>
        public event Action<ReadOnlyCollection<ServerInfo>> OnServerListChange = (serverList) => { };

        /// <summary>
        /// Loads the server list from the supplied <paramref name="settings"/> into <see cref="ServerInfoList"/>
        /// </summary>
        /// <param name="settings">The settings object from which to load the server list</param>
        public void LoadServerSettings(MultiplayerClientSettings settings)
        {
            //TODO load the server list from the supplied settings into ServerInfoList
            //ServerInfoList.AddRange()
        }

        public ServerListMenu(Game game, MultiplayerClient client) : base(game)
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
            mp = client;

            OptionAdd = new MenuListOption("Add", SelectAdd);
            OptionRemove = new MenuListOption("Remove", SelectRemove);
            OptionBack = new MenuListOption("Back", SelectBack);
            OptionJoin = new MenuListOption("Join", JoinServer);
            OptionDisconnect = new MenuListOption("Disconnect from server", LeaveServer) { Enabled = false };
            OptionRefreshLAN = new MenuListOption("Refresh LAN servers", ForceRefreshOptionsList);

            serverDiscoverer = new ServerDiscoverer(SharedConstants.MulticastAddress);
            serverDiscoverer.OnReceiveData += ServerDiscoverer_OnReceiveData;

            mp.OnConnect += () =>
            {
                OptionDisconnect.Enabled = true;
            };
            mp.OnDisconnect += () =>
            {
                OptionDisconnect.Enabled = false;
            };
        }

        private void ServerDiscoverer_OnReceiveData(IPEndPoint remoteEndpoint, Dictionary<string, string> obj)
        {
            if (obj.TryGetValue("Protocol", out string protocol) && protocol.Equals(SharedNetcode<PlayerMetadata>.ProtocolSignature))
            {
                if (obj.TryGetValue("Endpoint", out string endpointString) && int.TryParse(endpointString.Split(':').Last(), out int port))
                {
                    if (obj.TryGetValue("Version", out string version) && version.Equals(SharedNetcode<PlayerMetadata>.ProtocolVersion))
                    {
                        IPEndPoint targetEndpoint = new IPEndPoint(remoteEndpoint.Address, port);
                        string name = obj.TryGetValue("Name", out string name0) ? name0 : targetEndpoint.ToString();
                        _ = LANServerInfoList.AddOrUpdate(targetEndpoint, (endpoint) =>
                        {
                            return new LANServerInfo(name, targetEndpoint) { lastUpdate = DateTime.UtcNow.Ticks };

                        }, (endpoint, server) =>
                        {
                            server.Name = name;
                            server.lastUpdate = DateTime.UtcNow.Ticks;
                            return server;
                        });

                    }
                    else
                    {
                        //TODO version doesn't match; signify this somehow?
                    }
                }
                else
                {
                    //Malformed message: Missing endpoint information; ignore
                }
            }
            else
            {
                //Different protocol; ignore
            }
        }
        /// <summary>
        /// The amount of time, in ticks, before forgetting about a LAN server info.
        /// </summary>
        private static readonly long LANServerTimeout = TimeSpan.FromSeconds(30).Ticks;
        private void RemoveOldLANServers()
        {
            long currentTime = DateTime.UtcNow.Ticks;
            var expiredKeys = new List<IPEndPoint>();
            try
            {
                expiredKeys = LANServerInfoList
                        .Where(server => (currentTime - server.Value.lastUpdate) > LANServerTimeout)
                        .Select(a => a.Key).ToList();
                expiredKeys.ForEach(key => LANServerInfoList.TryRemove(key, out _));
            }
            catch
            {
            }
        }

        private bool disposing = false;
        protected override void Dispose(bool disposing)
        {
            if (this.disposing)
                return;
            this.disposing = true;
            drawer.Dispose();

            serverDiscoverer.Dispose();

            base.Dispose();
        }

        private IEnumerable<MenuListOption> ServerList => ServerInfoList.Concat(LANServerInfoList.Values.OrderBy(s => s.Name))
                        .Select(info => new MenuListOption(info.Name, () => SelectServer(info)));
        private enum MenuLevel
        {
            Hidden = 0,
            ServerList,
            ServerSelected,
            ServerAdd,
            ServerRemove,
        }
        private MenuLevel __currentMenu = MenuLevel.ServerList;
        private MenuLevel currentMenu
        {
            get => __currentMenu;
            set
            {
                __currentMenu = value;
                currentIndex = 0;
                ForceRefreshOptionsList();
            }
        }
        private ServerInfo selectedInfo = null;
        private List<MenuListOption> GetListOptions()
        {
            List<MenuListOption> list = new List<MenuListOption>();
            switch (currentMenu)
            {
            case MenuLevel.ServerList:
                list.Add(OptionDisconnect);
                list.Add(OptionAdd);
                list.Add(OptionRefreshLAN);
                list.AddRange(ServerList);
                break;
            case MenuLevel.ServerSelected:
                list.Add(OptionJoin);
                if (!selectedInfo.GetType().IsAssignableFrom(typeof(LANServerInfo)))
                {
                    list.Add(OptionRemove);
                }
                break;
            case MenuLevel.ServerAdd:
                //TODO
                break;
            case MenuLevel.ServerRemove:
                //TODO
                break;
            case MenuLevel.Hidden:
                break;
            }
            list.Add(OptionBack);
            return list;
        }

        private void ForceRefreshOptionsList()
        {
            SinceLastUpdateList += UpdateInterval;
        }
        private void SelectAdd()
        {
            currentMenu = MenuLevel.ServerAdd;
            //TODO
        }
        private void SelectRemove()
        {
            currentMenu = MenuLevel.ServerRemove;
            //TODO
        }
        private void SelectBack()
        {
            switch (currentMenu)
            {
            case MenuLevel.ServerList:
                currentMenu = MenuLevel.ServerList;
                break;
            case MenuLevel.ServerRemove:
                currentMenu = MenuLevel.ServerSelected;
                break;
            case MenuLevel.ServerAdd:
            case MenuLevel.ServerSelected:
                currentMenu = MenuLevel.ServerList;
                break;
            case MenuLevel.Hidden:
                break;
            }
        }
        private void SelectServer(ServerInfo info)
        {
            selectedInfo = info;
            currentMenu = MenuLevel.ServerSelected;
        }
        private void JoinServer()
        {
            mp.ConnectToServerAsync(selectedInfo.Endpoint);
        }
        private void LeaveServer()
        {
            mp.Disconnect();
            OptionDisconnect.Enabled = false;
            ForceRefreshOptionsList();
        }

        private List<MenuListOption> cachedMenuListOptions = null;
        private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan FirstUpdateInterval = TimeSpan.FromSeconds(15);
        private static bool FirstUpdateDone = false;
        private static TimeSpan SinceLastUpdateList = UpdateInterval;

        private bool hasFocus = true;
        private int currentIndex = 0;
        public override void Update(GameTime gameTime)
        {
            if(!ServiceHelper.FirstLoadDone)
            {
                return;
            }
            SinceLastUpdateList += gameTime.ElapsedGameTime;
            if(!FirstUpdateDone && SinceLastUpdateList > FirstUpdateInterval)
            {
                cachedMenuListOptions = GetListOptions();
                SinceLastUpdateList = TimeSpan.Zero;
            }
            if (SinceLastUpdateList > UpdateInterval)
            {
                RemoveOldLANServers();
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
                int maxIndex = cachedMenuListOptions.Count - 1;
                if (InputManager.Down == FezButtonState.Pressed)
                {
                    if (currentIndex < maxIndex)
                    {
                        //sCursorUp.Emit();
                        currentIndex++;
                    }
                }
                if (currentIndex < 0)
                {
                    currentIndex = 0;
                }
                if (currentIndex > maxIndex)
                {
                    currentIndex = maxIndex;
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
                position.Y = GraphicsDevice.Viewport.Height / 10;
                int i = 0;
                foreach(var option in cachedMenuListOptions)
                {
                    bool selected = i == currentIndex;
                    string optionName = option.DisplayText;
                    if(!option.Enabled)
                    {
                        optionName = "\x1B[90m" + optionName + "\x1B[0m";
                    }
                    string text = $"{(selected ? ">" : " ")} {optionName} {(selected ? "<" : " ")}";
                    Vector2 lineSize = RichTextRenderer.MeasureString(Fonts, text);
                    position.X = GraphicsDevice.Viewport.Width / 2 - lineSize.X / 2;
                    RichTextRenderer.DrawString(drawer, Fonts, text, position + Vector2.One, Color.Black);
                    RichTextRenderer.DrawString(drawer, Fonts, text, position, Color.White);
                    position.Y += lineSize.Y;
                    ++i;
                    if(position.Y > GraphicsDevice.Viewport.Height)
                    {
                        break;
                    }
                }
                drawer.End();
            }
        }
    }
}

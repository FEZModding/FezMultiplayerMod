using Common;
using FezEngine.Components;
using FezEngine.Services;
using FezEngine.Structure.Input;
using FezEngine.Tools;
using FezGame.Services;
using FezGame.Structure;
using FezSharedTools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.RuntimeDetour;
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
            public MenuListOption(string text, Func<MenuLevel> submenuSupplier)
            {
                this.DisplayText = text;
                this.Action = ()=> { Instance.CurrentMenuLevel = submenuSupplier.Invoke(); };
            }
        }
        private class MenuLevel
        {
            private static int NextId = 0;
            public readonly int Id;
            public readonly string Name;
            public readonly MenuLevel ParentMenu;
            public readonly Action<List<MenuListOption>> AddOptions;
            public MenuLevel(string name, MenuLevel parent, Action<List<MenuListOption>> AddOptions)
            {
                Id = NextId++;
                Name = name;
                ParentMenu = parent;
                this.AddOptions = AddOptions;
            }
            public override bool Equals(object obj)
            {
                return (obj as MenuLevel)?.Id == Id;
            }
            public override int GetHashCode()
            {
                return Id;
            }
            public override string ToString()
            {
                return Name;
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

        //declared on class scope so we can also trigger it when the back button is pressed
        private readonly MenuListOption OptionBack;
        //declared on class scope so we can toggle Enabled
        private readonly MenuListOption OptionDisconnect;

        private readonly MenuLevel Menu_None = new MenuLevel("", null, (list)=> { });
        private readonly MenuLevel Menu_ServerList;
        private readonly MenuLevel Menu_ServerSelected;
        private readonly MenuLevel Menu_ServerAdd;
        private readonly MenuLevel Menu_ServerRemove;

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
        private static Hook MenuInitHook = null;
        private static readonly List<Tuple<string, Action>> CustomMenuOptions = new List<Tuple<string, Action>>();
        private static void InitFakeMenuLevel()
        {
            Type MainMenuType = typeof(Fez).Assembly.GetType("FezGame.Components.MainMenu");
            Type MenuBaseType = typeof(Fez).Assembly.GetType("FezGame.Components.MenuBase");
            Type MenuLevelType = typeof(Fez).Assembly.GetType("FezGame.Structure.MenuLevel");

            void CreateAndAddCustomLevels(object MenuBase)
            {
                const BindingFlags privBind = BindingFlags.NonPublic | BindingFlags.Instance;

                // prepare main menu object
                object MenuRoot = null;
                if (MenuBase.GetType() == MainMenuType)
                {
                    MenuRoot = MainMenuType.GetField("RealMenuRoot", privBind).GetValue(MenuBase);
                }

                if (MenuBase.GetType() != MainMenuType || MenuRoot == null)
                {
                    MenuRoot = MenuBaseType.GetField("MenuRoot", privBind).GetValue(MenuBase);
                }

                if (MenuRoot == null)
                {
                    Logger.Log("FezMultiplayerMod", LogSeverity.Warning, "Unable to create multiplayer menu!");
                    return;
                }

                MenuLevelType.GetField("IsDynamic").SetValue(MenuRoot, true);

                CustomMenuOptions.ForEach(tuple =>
                {
                    string optionText = tuple.Item1;
                    object CustomLevel = Activator.CreateInstance(MenuLevelType);
                    MenuLevelType.GetField("IsDynamic").SetValue(CustomLevel, true);
                    MenuLevelType.GetProperty("Title").SetValue(CustomLevel, optionText, null);
                    MenuLevelType.GetField("Parent").SetValue(CustomLevel, MenuRoot);
                    MenuLevelType.GetField("Oversized").SetValue(CustomLevel, true);
                    // add created menu level to the main menu
                    int modsIndex = ((IList)MenuLevelType.GetField("Items").GetValue(MenuRoot)).Count - 2;
                    MenuLevelType.GetMethod("AddItem", new Type[] { typeof(string), typeof(Action), typeof(int) })
                        .Invoke(MenuRoot, new object[] { optionText, (Action) delegate{
                                MenuBaseType.GetMethod("ChangeMenuLevel").Invoke(MenuBase, new object[] { CustomLevel, false });
                                tuple.Item2();
                        }, modsIndex});
                    ;
                });

                // needed to refresh the menu before the transition to it happens (pause menu)
                MenuBaseType.GetMethod("RenderToTexture", privBind).Invoke(MenuBase, new object[] { });

            }

            if (MenuInitHook == null)
            {
                MenuInitHook = new Hook(
                    MenuBaseType.GetMethod("Initialize"),
                    new Action<Action<object>, object>((orig, self) =>
                    {
                        orig(self);
                        CreateAndAddCustomLevels(self);
                    })
                );
            }
        }
        private static void AddFakeMenuLevel(string text, Action onSelect)
        {
            CustomMenuOptions.Add(Tuple.Create(text, onSelect));
        }

        public ServerListMenu(Game game, MultiplayerClient client) : base(game)
        {
            DrawOrder = 2300;
            Instance = this;
            drawer = new SpriteBatch(GraphicsDevice);

            InitFakeMenuLevel();
            AddFakeMenuLevel("@MULTIPLAYER", () => HasFocus = true);

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

            //declared on class scope so we can be ran it when the back button is pressed
            OptionBack = new MenuListOption("Back", MenuBack);
            //declared on class scope so we can toggle Enabled
            OptionDisconnect = new MenuListOption("Disconnect from server", LeaveServer) { Enabled = false };

            MenuListOption OptionAdd = new MenuListOption("Add", () => Menu_ServerAdd);
            MenuListOption OptionRemove = new MenuListOption("Remove", () => Menu_ServerRemove);
            MenuListOption OptionJoin = new MenuListOption("Join", JoinServer);
            MenuListOption OptionRefreshLAN = new MenuListOption("Refresh LAN servers", ForceRefreshOptionsList);

            Menu_ServerList = new MenuLevel("Server List", parent: Menu_None, list =>
            {
                list.Add(OptionDisconnect);
                list.Add(OptionAdd);
                list.Add(OptionRefreshLAN);
                list.AddRange(ServerList);
            });
            Menu_ServerSelected = new MenuLevel("Selected Server", parent: Menu_ServerList, list =>
            {
                list.Add(OptionJoin);
                if (!selectedInfo.GetType().IsAssignableFrom(typeof(LANServerInfo)))
                {
                    list.Add(OptionRemove);
                }
            });
            Menu_ServerAdd = new MenuLevel("Add Server", parent: Menu_ServerList, list =>
            {
                //TODO
            });
            Menu_ServerRemove = new MenuLevel("Remove Server?", parent: Menu_ServerSelected, list =>
            {
                //TODO
            });
            CurrentMenuLevel = Menu_ServerList;

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

        private MenuLevel __currentMenu = null;
        private MenuLevel CurrentMenuLevel
        {
            get => __currentMenu;
            set
            {
                if(value == null)
                {
                    System.Diagnostics.Debugger.Launch();
                    System.Diagnostics.Debugger.Break();
                    return;
                }
                __currentMenu = value;
                currentIndex = 0;
                ForceRefreshOptionsList();
                if (CurrentMenuLevel == Menu_None)
                {
                    HasFocus = false;
                }
            }
        }
        private ServerInfo selectedInfo = null;
        private void SelectServer(ServerInfo info)
        {
            selectedInfo = info;
            CurrentMenuLevel = Menu_ServerSelected;
        }

        private List<MenuListOption> GetListOptions()
        {
            List<MenuListOption> list = new List<MenuListOption>();
            CurrentMenuLevel.AddOptions(list);
            list.Add(OptionBack);
            return list;
        }
        private void MenuBack()
        {
            CurrentMenuLevel = CurrentMenuLevel.ParentMenu;
        }

        private void ForceRefreshOptionsList()
        {
            SinceLastUpdateList += UpdateInterval;
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
        private static TimeSpan SinceLastUpdateList = UpdateInterval;

        private bool __hasFocus = false;
        private bool HasFocus
        {
            get => __hasFocus;
            set
            {
                if (value == true)
                {
                    CurrentMenuLevel = Menu_ServerList;
                }
                __hasFocus = value;
            }
        }
        private int currentIndex = 0;
        public override void Update(GameTime gameTime)
        {
            if(!ServiceHelper.FirstLoadDone)
            {
                return;
            }
            //TODO set hasFocus when the fake menu is selected
            if (Microsoft.Xna.Framework.Input.Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.F2))
            {
                HasFocus = true;
            }
            SinceLastUpdateList += gameTime.ElapsedGameTime;
            if(SinceLastUpdateList > FirstUpdateInterval)
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
            if (HasFocus)
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
                    MenuBack();
                }
                if (InputManager.Jump == FezButtonState.Pressed || InputManager.Start == FezButtonState.Pressed)
                {
                    cachedMenuListOptions.ElementAt(currentIndex).Action.Invoke();
                }
            }
        }
        private void DrawTextRichShadow(string text, Vector2 position, Vector2? scale = null, Color? color = null, Color? shadow = null)
        {
            if(scale == null)
            {
                scale = Vector2.One;
            }

            RichTextRenderer.DrawString(drawer, Fonts, text, position + Vector2.One, shadow ?? Color.Black, Color.Transparent, scale.Value);
            RichTextRenderer.DrawString(drawer, Fonts, text, position, color ?? Color.White, Color.Transparent, scale.Value);
        }
        public override void Draw(GameTime gameTime)
        {
            if (HasFocus && cachedMenuListOptions != null && drawer != null)
            {
                drawer.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                Vector2 position = Vector2.Zero;
                position.Y = GraphicsDevice.Viewport.Height * 0.15f;
                int i = 0;
                {
                    const string underlineStart = "\x1B[21m";
                    const string underlinePadding = "    ";
                    const string resetFormatting = "\x1B[0m";
                    const string nameStart = underlineStart + underlinePadding;
                    const string nameEnd = underlinePadding + resetFormatting;

                    string menuTitle = nameStart + CurrentMenuLevel.Name + nameEnd;

                    Vector2 titleScale = new Vector2(1.5f);
                    Vector2 lineSize = RichTextRenderer.MeasureString(Fonts, menuTitle) * titleScale;
                    position.X = GraphicsDevice.Viewport.Width / 2 - lineSize.X / 2;
                    DrawTextRichShadow(menuTitle, position, titleScale);
                    //drawTextRichShadow(menuTitle, position);
                    position.Y += lineSize.Y;
                }
                foreach (var option in cachedMenuListOptions)
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

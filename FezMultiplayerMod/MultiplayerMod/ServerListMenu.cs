using Common;
using FezEngine.Components;
using FezEngine.Services;
using FezEngine.Structure.Input;
using FezEngine.Tools;
using FezGame.Services;
using FezGame.Structure;
using FezSharedTools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
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
using static FezEngine.Structure.SoundEffectExtensions;

namespace FezGame.MultiplayerMod
{
    internal static class RectangleExtentions
    {
        public static Rectangle Inset(this Rectangle rect, float inset)
        {
            return rect.Inset(inset, inset);
        }
        public static Rectangle Inset(this Rectangle rect, float inline, float block)
        {
            return new Rectangle((int)(rect.X + inline),
                (int)(rect.Y + block),
                (int)(rect.Width - inline * 2),
                (int)(rect.Height - block * 2));
        }
        public static Rectangle Inset(this Rectangle rect, int inline, int block)
        {
            return new Rectangle(rect.X + inline,
                rect.Y + block,
                rect.Width - inline * 2,
                rect.Height - block * 2);
        }
        public static Rectangle OffsetOrigin(this Rectangle rect, int offX, int offY)
        {
            return new Rectangle(rect.X + offX,
                rect.Y + offY,
                rect.Width,
                rect.Height);
        }
        public static Rectangle OffsetOriginForRotateRect(this Rectangle rect)
        {
            return new Rectangle(rect.X + rect.Width / 2,
                rect.Y + rect.Height / 2,
                rect.Width,
                rect.Height);
        }
    }
    public class ServerInfo
    {
        private const char GroupSeparator = '\x1D';
        public string Name;
        public readonly IPEndPoint Endpoint;
        public ServerInfo(string text, IPEndPoint endpoint)
        {
            this.Name = text;
            this.Endpoint = endpoint;
        }
        public override string ToString()
        {
            return Name + GroupSeparator + Endpoint;
        }
        public static ServerInfo Parse(string str)
        {
            string[] parts = str.Split(GroupSeparator);
            if (parts.Length != 2 || !IniTools.TryParseIPEndPoint(parts[1], out IPEndPoint endpoint))
            {
                throw new ArgumentException("Invalid ServerInfo Format");
            }
            return new ServerInfo(parts[0], endpoint);
        }
    }
    internal sealed class ServerListMenu : DrawableGameComponent
    {
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
            public readonly Action OnMoveToOtherOption;
            public bool Enabled = true;
            internal Rectangle BoundingClientRect = new Rectangle(-100, -100, 0, 0);
            internal int Index = -1;

            public MenuListOption()
            {
            }
            public MenuListOption(string text, Action action, Action onMoveToOtherOption = null)
            {
                this.DisplayText = text;
                this.Action = action;
                this.OnMoveToOtherOption = onMoveToOtherOption;
            }
            public MenuListOption(string text, Func<MenuLevel> submenuSupplier, Action onMoveToOtherOption = null, Action onSelect = null)
            {
                this.DisplayText = text;
                this.Action = () => { Instance.CurrentMenuLevel = submenuSupplier.Invoke(); onSelect?.Invoke(); };
                this.OnMoveToOtherOption = onMoveToOtherOption;
            }
        }
        private class MenuListTextInput
        {
            private static readonly string framedTextEscapeCode = $"{RichTextRenderer.C1_8BitCodes.CSI}{RichTextRenderer.SGRParameters.Framed}{RichTextRenderer.CSICommands.SGR}";
            private static readonly string textResetFormattingEscapeCode = $"{RichTextRenderer.C1_8BitCodes.CSI}{RichTextRenderer.SGRParameters.Reset}{RichTextRenderer.CSICommands.SGR}";
            public static int TextboxPadRight
            {
                get => TextInputLogicComponent.TextboxPadRight;
                set => TextInputLogicComponent.TextboxPadRight = value;
            }
            public string Value
            {
                get => hiddenInputElement.Value;
                set => hiddenInputElement.Value = value;
            }

            public readonly MenuListOption MenuListOption;
            private readonly TextInputLogicComponent hiddenInputElement;

            public MenuListTextInput(Game game, string label, Action<string> OnUpdate = null)
            {
                string textboxInitialText = framedTextEscapeCode.PadRight(TextboxPadRight) + textResetFormattingEscapeCode;
                string baseName = label + framedTextEscapeCode;
                hiddenInputElement = new TextInputLogicComponent(game);
                ServiceHelper.AddComponent(hiddenInputElement);
                MenuListOption = new MenuListOption(label + textboxInitialText, () => hiddenInputElement.HasFocus = true, () => hiddenInputElement.HasFocus = false);
                hiddenInputElement.OnUpdate += (onlyCaretBlinking) =>
                {
                    MenuListOption.DisplayText = baseName + hiddenInputElement.DisplayValue + textResetFormattingEscapeCode;
                    if (!onlyCaretBlinking)
                    {
                        OnUpdate?.Invoke(hiddenInputElement.Value);
                    }
                };
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
        private IContentManagerProvider CMProvider { get; set; }
        private IInputManager InputManager { get; set; }
        private IFontManager Fonts { get; set; }
        private IMouseStateManager MouseState { get; set; }
        #endregion

        public static ServerListMenu Instance;
        private readonly SpriteBatch drawer;
        private readonly ServerDiscoverer serverDiscoverer;

        private readonly List<ServerInfo> ServerInfoList = new List<ServerInfo>();
        private readonly ConcurrentDictionary<IPEndPoint, LANServerInfo> LANServerInfoList = new ConcurrentDictionary<IPEndPoint, LANServerInfo>();
        private readonly MultiplayerClient mp;

        //declared on class scope so we can add it to every menu
        private readonly MenuListOption OptionBack;
        //declared on class scope so we can toggle Enabled
        private readonly MenuListOption OptionDisconnect;

        private readonly MenuLevel Menu_None = new MenuLevel("", null, (list) => { });
        private readonly MenuLevel Menu_ChangeName;
        private readonly MenuLevel Menu_ServerList;
        private readonly MenuLevel Menu_ServerSelected;
        private readonly MenuLevel Menu_ServerAdd;
        private readonly MenuLevel Menu_ServerRemove;

        private readonly MenuListTextInput NameTextbox;
        private readonly MenuListTextInput AddressTextbox;

        /// <summary>
        /// Called when the user adds or removes a server from the server list. <br />
        /// Supplies the new server list, and does not include LAN servers.
        /// </summary>
        public event Action<ReadOnlyCollection<ServerInfo>> OnServerListChange = (serverList) => { };

        public event Action<string> OnPlayerNameChange = (newName) => { };

        /// <summary>
        /// Loads the server list from the supplied <paramref name="settings"/> into <see cref="ServerInfoList"/>
        /// </summary>
        /// <param name="settings">The settings object from which to load the server list</param>
        public void LoadServerSettings(MultiplayerClientSettings settings)
        {
            //load the server list from the supplied settings into ServerInfoList
            ServerInfoList.AddRange(settings.ServerList);
        }
        private static readonly Hook MenuInitHook = null;
        private static readonly Hook MenuUpdateHook = null;
        private static readonly Hook MenuUpOneLevelHook = null;
        private static readonly Tuple<string, Action> MultiplayerMenu = new Tuple<string, Action>("@MULTIPLAYER", () => Instance.HasFocus = true);
        private static event Action OnMenuUpOneLevel = () => { };
        private static readonly FieldInfo MenuCursorSelectable;
        private static readonly FieldInfo MenuCursorClicking;
        private static object MenuBaseInstance;
        private static bool CursorClicking = false;
        private static bool CursorSelectable = false;
        private static void SetMenuCursorState(bool selectable, bool clicking)
        {
            CursorSelectable = selectable;
            CursorClicking = selectable && clicking;
            MenuCursorSelectable.SetValue(MenuBaseInstance, selectable);
            MenuCursorClicking.SetValue(MenuBaseInstance, selectable && clicking);
        }
        private static Action<bool> SetMenuTrapInput = _ => { };
        static ServerListMenu()
        {
            const BindingFlags privBind = BindingFlags.NonPublic | BindingFlags.Instance;
            Type MainMenuType = typeof(Fez).Assembly.GetType("FezGame.Components.MainMenu");
            Type MenuBaseType = typeof(Fez).Assembly.GetType("FezGame.Components.MenuBase");
            Type MenuLevelType = typeof(Fez).Assembly.GetType("FezGame.Structure.MenuLevel");
            FieldInfo SinceMouseMovedInternal = MenuBaseType.GetField("SinceMouseMoved", BindingFlags.Instance | BindingFlags.NonPublic);
            MenuCursorSelectable = MenuBaseType.GetField("CursorSelectable");
            MenuCursorClicking = MenuBaseType.GetField("CursorClicking");

            void CreateAndAddCustomLevels(object MenuBase)
            {
                object MenuRoot = null;
                // prepare main menu object
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
                    SharedTools.LogWarning("FezMultiplayerMod", "Unable to create multiplayer menu!");
                    return;
                }

                MenuLevelType.GetField("IsDynamic").SetValue(MenuRoot, true);

                {
                    string optionText = MultiplayerMenu.Item1;
                    object CustomLevel = Activator.CreateInstance(MenuLevelType);
                    MenuLevelType.GetField("IsDynamic").SetValue(CustomLevel, true);
                    MenuLevelType.GetProperty("Title").SetValue(CustomLevel, optionText, null);
                    MenuLevelType.GetField("Parent").SetValue(CustomLevel, MenuRoot);
                    MenuLevelType.GetField("Oversized").SetValue(CustomLevel, true);
                    SetMenuTrapInput = (b) => { MenuLevelType.GetProperty("TrapInput").SetValue(CustomLevel, b, null); };
                    // add created menu level to the main menu
                    int modsIndex = ((IList)MenuLevelType.GetField("Items").GetValue(MenuRoot)).Count - 2;
                    MenuLevelType.GetMethod("AddItem", new Type[] { typeof(string), typeof(Action), typeof(int) })
                        .Invoke(MenuRoot, new object[] { optionText, (Action) delegate{
                                MenuBaseType.GetMethod("ChangeMenuLevel").Invoke(MenuBase, new object[] { CustomLevel, false });
                                MultiplayerMenu.Item2();
                        }, modsIndex});
                    ;
                }

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
                        MenuBaseInstance = self;
                        CreateAndAddCustomLevels(self);
                    })
                );
                MenuUpdateHook = new Hook(
                    MenuBaseType.GetMethod("Update"),
                    new Action<Action<object, GameTime>, object, GameTime>((orig, self, gametime) =>
                    {
                        orig(self, gametime);
                        if (Instance.HasFocus)
                        {
                            SinceMouseMovedInternal.SetValue(self, 3f);
                        }
                    })
                );
            }
            if (MenuUpOneLevelHook == null)
            {
                MenuUpOneLevelHook = new Hook(
                    MenuBaseType.GetMethod("UpOneLevel", privBind),
                    new Action<Action<object, object>, object, object>((orig, self, menulevel) =>
                    {
                        orig(self, menulevel);
                        OnMenuUpOneLevel();
                    })
                );
            }
        }
        private static SoundEffect sCancel, sConfirm, sCursorUp, sCursorDown;

        public ServerListMenu(Game game, MultiplayerClient client) : base(game)
        {
            DrawOrder = 2300;
            Instance = this;
            drawer = new SpriteBatch(GraphicsDevice);

            OnMenuUpOneLevel += () =>
            {
                HasFocus = false;
                CurrentMenuLevel = Menu_None;
            };

            _ = Waiters.Wait(() =>
            {
                return ServiceHelper.FirstLoadDone;
            },
            () =>
            {
                CMProvider = ServiceHelper.Get<IContentManagerProvider>();
                InputManager = ServiceHelper.Get<IInputManager>();
                Fonts = ServiceHelper.Get<IFontManager>();
                MouseState = ServiceHelper.Get<IMouseStateManager>();

                ContentManager contentManager = CMProvider.Global;
                sCancel = contentManager.Load<SoundEffect>("Sounds/Ui/Menu/Cancel");
                sConfirm = contentManager.Load<SoundEffect>("Sounds/Ui/Menu/Confirm");
                sCursorUp = contentManager.Load<SoundEffect>("Sounds/Ui/Menu/CursorUp");
                sCursorDown = contentManager.Load<SoundEffect>("Sounds/Ui/Menu/CursorDown");
            });
            mp = client;

            //declared on class scope so we can add it to every menu
            OptionBack = new MenuListOption("Back", () => MenuBack());
            //declared on class scope so we can toggle Enabled
            OptionDisconnect = new MenuListOption("Disconnect from server", LeaveServer) { Enabled = false };

            MenuListOption OptionAdd = new MenuListOption("Add", () => Menu_ServerAdd, onSelect: () => { NameTextbox.Value = ""; AddressTextbox.Value = ""; });
            MenuListOption OptionRemove = new MenuListOption("Remove", () => Menu_ServerRemove);
            MenuListOption OptionRemoveConfirm = new MenuListOption("Confirm Removal", RemoveServerConfirmed);
            MenuListOption OptionAddServer = new MenuListOption("Add Server", AddServerConfirmed);
            MenuListOption OptionJoin = new MenuListOption("Join", JoinServer);
            MenuListOption OptionRefreshLAN = new MenuListOption("Refresh LAN servers", ForceRefreshOptionsList);

            MenuListOption OptionChangeName = new MenuListOption("Change Name", () => Menu_ChangeName, onSelect: () => NameTextbox.Value = mp.MyPlayerName);
            MenuListOption OptionChangeNameConfirm = new MenuListOption("Confirm Name", ChangeNameConfirmed);

            const int textboxPadRight = 30;
            MenuListTextInput.TextboxPadRight = textboxPadRight;
            NameTextbox = new MenuListTextInput(game, "Name: ");
            AddressTextbox = new MenuListTextInput(game, "Address: ", (value) =>
            {
                //Check the IPEndPoint is valid
                OptionAddServer.Enabled = IniTools.TryParseIPEndPoint(value, out IPEndPoint _, true);
            });
            OptionAddServer.Enabled = false;

            Menu_ServerList = new MenuLevel("Server List", parent: Menu_None, list =>
            {
                list.Add(OptionDisconnect);
                list.Add(OptionAdd);
                list.Add(OptionRefreshLAN);
                list.Add(OptionChangeName);
                list.AddRange(ServerList);
            });
            Menu_ServerSelected = new MenuLevel("Selected Server", parent: Menu_ServerList, list =>
            {
                list.Add(OptionJoin);
                if (selectedInfo.GetType().Name != typeof(LANServerInfo).Name)
                {
                    list.Add(OptionRemove);
                }
            });
            Menu_ServerAdd = new MenuLevel("Add Server", parent: Menu_ServerList, list =>
            {
                list.Add(NameTextbox.MenuListOption);
                list.Add(AddressTextbox.MenuListOption);
                list.Add(OptionAddServer);
            });
            Menu_ServerRemove = new MenuLevel("Remove Server?", parent: Menu_ServerSelected, list =>
            {
                list.Add(OptionRemoveConfirm);
            });

            Menu_ChangeName = new MenuLevel("Enter a new Name", parent: Menu_ServerList, list =>
            {
                list.Add(NameTextbox.MenuListOption);
                list.Add(OptionChangeNameConfirm);
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
                if (value == null)
                {
                    System.Diagnostics.Debugger.Launch();
                    System.Diagnostics.Debugger.Break();
                    return;
                }
                __currentMenu = value;
                currentIndex = 0;
                scrollY = scrollTop;
                ForceRefreshOptionsList();
                SetMenuTrapInput(CurrentMenuLevel != Menu_ServerList);
                if (CurrentMenuLevel == Menu_None)
                {
                    HasFocus = false;
                    SetMenuTrapInput(false);
                }
                selectorOrigin = null;
                selectorScale = null;
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
            if (CurrentMenuLevel != Menu_ServerList)
            {
                list.Add(OptionBack);
            }
            return list;
        }
        private void MenuBack(bool emitSound = true)
        {
            CurrentMenuItem?.OnMoveToOtherOption?.Invoke();
            if(emitSound && CurrentMenuLevel != Menu_ServerList)
            {
                sCancel.Emit();
            }
            CurrentMenuLevel = CurrentMenuLevel.ParentMenu;
            SetMenuTrapInput(CurrentMenuLevel != Menu_ServerList && CurrentMenuLevel != Menu_None);
        }

        private void AddServerConfirmed()
        {
            string name = NameTextbox.Value;
            string address = AddressTextbox.Value;
            try
            {
                _ = IniTools.TryParseIPEndPoint(address, out IPEndPoint endpoint);
                ServerInfoList.Add(new ServerInfo(name, endpoint));
                OnServerListChange(ServerInfoList.AsReadOnly());
                MenuBack(emitSound: false);
            }
            catch (ArgumentException) { }
        }
        private void RemoveServerConfirmed()
        {
            ServerInfoList.Remove(selectedInfo);
            OnServerListChange(ServerInfoList.AsReadOnly());
            CurrentMenuLevel = Menu_ServerList;
        }

        private void ChangeNameConfirmed()
        {
            string newName = NameTextbox.Value;
            mp.MyPlayerName = newName;
            OnPlayerNameChange(newName);
            CurrentMenuLevel = Menu_ServerList;
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
        private bool justGotFocus = false;
        private bool HasFocus
        {
            get => __hasFocus;
            set
            {
                if (value == true)
                {
                    CurrentMenuLevel = Menu_ServerList;
                    justGotFocus = true;
                }
                __hasFocus = value;
            }
        }
        private int currentIndex = 0;
        private MenuListOption CurrentMenuItem
        {
            get
            {
                if (cachedMenuListOptions == null)
                {
                    return null;
                }
                if (currentIndex < 0)
                {
                    currentIndex = 0;
                }
                int maxIndex = cachedMenuListOptions.Count - 1;
                if (currentIndex > maxIndex)
                {
                    currentIndex = maxIndex;
                }
                return cachedMenuListOptions.ElementAt(currentIndex);
            }
        }
        private static readonly TimeSpan menuChangeDelay = TimeSpan.FromMilliseconds(100);
        private float SinceMouseMoved = 3f;
        private bool mouseWasDown = false;
        private const float scrollTop = -selectedItemPaddingBlock;
        private float scrollY = scrollTop;
        private float contentHeight = 0;
        private Rectangle MenuFrameRect = new Rectangle();
        private Rectangle ScrollBarTrackHitbox = new Rectangle();
        private Rectangle ScrollBarThumbHitbox = new Rectangle();
        private Rectangle ScrollBarArrowUpHitbox = new Rectangle();
        private Rectangle ScrollBarArrowDownHitbox = new Rectangle();
        public static float ScrollDirection = -1;//TODO make customizable?
        private static readonly double scrollButtonRepeatInterval = 0.1d;
        private static readonly double scrollButtonRepeatDelay = 0.3d;
        private bool ScrollBarThumbHasHover = false;
        private bool ScrollBarThumbHasFocus = false;
        private class ScrollButtonState
        {
            internal double sinceScrollButtonHeld = 0;
            internal double sinceScrollButtonLastRepeat = 0;
            internal bool isheld = false;
            internal bool focus = false;
            internal bool containsPointer = false;
            internal readonly int sign;
            public ScrollButtonState(int sign)
            {
                this.sign = sign;
            }
        }
        private static readonly ScrollButtonState scrollButtonUp = new ScrollButtonState(-1), scrollButtonDown = new ScrollButtonState(1);
        public override void Update(GameTime gameTime)
        {
            if (!ServiceHelper.FirstLoadDone)
            {
                return;
            }
            SinceLastUpdateList += gameTime.ElapsedGameTime;
            if (SinceLastUpdateList > FirstUpdateInterval)
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
            SinceMouseMoved += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (MouseState.Movement.X != 0 || MouseState.Movement.Y != 0)
            {
                SinceMouseMoved = 0f;
            }
            if (MouseState.LeftButton.State != 0)
            {
                SinceMouseMoved = 0f;
            }
            bool mouseDown = Microsoft.Xna.Framework.Input.Mouse.GetState().LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
            if (HasFocus)
            {
                void OnChangeSelectedOption()
                {
                    if (CurrentMenuItem.Index == 0)
                    {
                        scrollY = scrollTop;
                        return;
                    }
                    // adjust scroll based on current selection
                    float menuFrameTop = MenuFrameRect.Y;
                    float menuFrameBottom = menuFrameTop + MenuFrameRect.Height;
                    float currScrollBottom = menuFrameBottom + scrollY;
                    float currItemTop = CurrentMenuItem.BoundingClientRect.Y - selectedItemPaddingBlock;
                    float currItemBottom = currItemTop + CurrentMenuItem.BoundingClientRect.Height + selectedItemPaddingBlock;

                    // If the top of the current item is above the visible area, scroll up
                    if (currItemTop < menuFrameTop)
                    {
                        scrollY += currItemTop - menuFrameTop;
                    }
                    // If the bottom of the current item is below the visible area, scroll down
                    else if (currItemBottom > menuFrameBottom)
                    {
                        scrollY += currItemBottom - menuFrameBottom;
                    }
                }
                bool mouseMoved = MouseState.Movement != Point.Zero;
                if (InputManager.Up == FezButtonState.Pressed)
                {
                    if (currentIndex > 0)
                    {
                        CurrentMenuItem?.OnMoveToOtherOption?.Invoke();
                        sCursorUp.Emit();
                        currentIndex--;
                        OnChangeSelectedOption();
                    }
                }
                int maxIndex = cachedMenuListOptions.Count - 1;
                if (InputManager.Down == FezButtonState.Pressed)
                {
                    if (currentIndex < maxIndex)
                    {
                        CurrentMenuItem?.OnMoveToOtherOption?.Invoke();
                        sCursorDown.Emit();
                        currentIndex++;
                        OnChangeSelectedOption();
                    }
                }
                //apparently most standard mice use 120 for one notch on the scroll wheel
                const float WheelDelta = 120f;
                float scrollDistanceBase = RichTextRenderer.MeasureString(Fonts, "A").Y;
                scrollY += ScrollDirection * MouseState.WheelTurns / WheelDelta * scrollDistanceBase;

                if ((InputManager.CancelTalk == FezButtonState.Pressed) || InputManager.Back == FezButtonState.Pressed)
                {
                    MenuBack();
                }
                Point positionPt = MouseState.PositionInViewport();
                MenuListOption hoveredOption = cachedMenuListOptions.FirstOrDefault(option =>
                    option.BoundingClientRect.Contains(positionPt)
                );
                bool cursorInFrame = MenuFrameRect.Contains(positionPt);
                bool hasHoveredOption = cursorInFrame && hoveredOption != null;
                if (mouseMoved && hasHoveredOption && cursorInFrame)
                {
                    if (currentIndex != hoveredOption.Index)
                    {
                        CurrentMenuItem?.OnMoveToOtherOption?.Invoke();
                        currentIndex = hoveredOption.Index;
                        sCursorUp.Emit();
                    }
                }
                void AttempScrollButtonPress(bool containsPointer, ScrollButtonState scrollButtonState)
                {
                    int sign = scrollButtonState.sign;
                    if (mouseDown && containsPointer)
                    {
                        scrollButtonState.sinceScrollButtonHeld += gameTime.ElapsedGameTime.TotalSeconds;
                        scrollButtonState.sinceScrollButtonLastRepeat += gameTime.ElapsedGameTime.TotalSeconds;
                        if (!scrollButtonState.isheld)
                        {
                            scrollButtonState.focus = true;
                            scrollButtonState.isheld = true;
                            scrollY += scrollDistanceBase * sign;
                        }
                        else
                        {
                            if (scrollButtonState.sinceScrollButtonHeld > scrollButtonRepeatDelay)
                            {
                                if (scrollButtonState.sinceScrollButtonLastRepeat > scrollButtonRepeatInterval)
                                {
                                    scrollButtonState.sinceScrollButtonLastRepeat = 0d;
                                    scrollY += scrollDistanceBase * sign;
                                }
                            }
                        }
                    }
                    else
                    {
                        scrollButtonState.sinceScrollButtonHeld = 0d;
                        scrollButtonState.sinceScrollButtonLastRepeat = 0d;
                    }
                    if (!mouseDown)
                    {
                        scrollButtonState.focus = false;
                    }
                    scrollButtonState.isheld = mouseDown;
                    scrollButtonState.containsPointer = containsPointer;
                }
                AttempScrollButtonPress(ScrollBarArrowUpHitbox.Contains(positionPt), scrollButtonUp);
                AttempScrollButtonPress(ScrollBarArrowDownHitbox.Contains(positionPt), scrollButtonDown);
                ScrollBarThumbHasHover = ScrollBarThumbHitbox.Contains(positionPt);
                if (mouseDown)
                {
                    if (ScrollBarThumbHasFocus || ScrollBarThumbHasHover)
                    {
                        ScrollBarThumbHasFocus = true;
                        scrollY += MouseState.Movement.Y;
                        // cursor dragging scrollbar support
                    }
                    else if (!(scrollButtonUp.focus || scrollButtonDown.focus) && ScrollBarTrackHitbox.Contains(positionPt))
                    {
                        //cursor clicking scrollbar support
                        scrollY += MenuFrameRect.Height * Math.Sign(positionPt.Y - (ScrollBarThumbHitbox.Y + ScrollBarThumbHitbox.Height / 2));
                    }
                }
                else
                {
                    ScrollBarThumbHasFocus = false;
                }
                scrollY = MathHelper.Clamp(scrollY, -selectedItemPaddingBlock, Math.Max(scrollTop, contentHeight - MenuFrameRect.Height + selectedItemPaddingBlock));

                SetMenuCursorState(hasHoveredOption, mouseDown);
                if (InputManager.Jump == FezButtonState.Pressed || InputManager.Start == FezButtonState.Pressed
                    || (mouseDown && !mouseWasDown && hasHoveredOption))
                {
                    MenuListOption menuitem = CurrentMenuItem;
                    if (!justGotFocus && menuitem.Enabled && SinceLastUpdateList > menuChangeDelay)
                    {
                        menuitem.Action.Invoke();
                        if (menuitem.DisplayText.Equals(OptionBack.DisplayText))
                        {
                        }
                        else
                        {
                            sConfirm.Emit();
                        }
                    }
                }
            }
            mouseWasDown = mouseDown;
            justGotFocus = false;
        }
        private const int selectedItemBorderThickness = 1;
        private const int selectedItemPaddingBlock = 1 + selectedItemBorderThickness;
        private const int selectedItemOutlinePaddingInlineEmFrac = 3;
        private Texture2D CanClickCursor;
        private Texture2D PointerCursor;
        private Texture2D ClickedCursor;
        private Texture2D arrowTexture;
        private Texture2D arrowTexture2;

        private static readonly Color LighterGray = new Color(233, 233, 233);
        private static readonly Color scrollBarBgColor = Color.Gray;

        private static readonly Color scrollButtonIdleColor = Color.White;
        private static readonly Color scrollArrowIdleColor = Color.Black;

        private static readonly Color scrollButtonHoverColor = LighterGray;
        private static readonly Color scrollArrowHoverColor = Color.DarkBlue;

        private static readonly Color scrollButtonHeldColor = Color.LightGray;
        private static readonly Color scrollArrowHeldColor = Color.DarkBlue;

        private static readonly Color scrollButtonFocusColor = LighterGray;
        private static readonly Color scrollArrowFocusColor = Color.Blue;

        private static readonly Color scrollThumbIdleColor = scrollButtonIdleColor;
        private static readonly Color scrollThumbHoverColor = scrollButtonHoverColor;
        private static readonly Color scrollThumbFocusColor = scrollButtonHeldColor;

        private static Vector2? selectorOrigin = null, selectorScale = null;
        public override void Draw(GameTime gameTime)
        {
            if (PointerCursor == null && CMProvider?.Global != null)
            {
                ContentManager contentManager = CMProvider.Global;
                PointerCursor = contentManager.Load<Texture2D>("Other Textures/cursor/CURSOR_POINTER");
                CanClickCursor = contentManager.Load<Texture2D>("Other Textures/cursor/CURSOR_CLICKER_A");
                ClickedCursor = contentManager.Load<Texture2D>("Other Textures/cursor/CURSOR_CLICKER_B");
                arrowTexture = contentManager.Load<Texture2D>("Other Textures/glyphs/LeftArrow");
                arrowTexture2 = contentManager.Load<Texture2D>("Other Textures/glyphs/RightArrow");
            }
            if (GraphicsDevice != null)
            {
                //magic numbers
                var frameScale = new Vector2(512f, 256f) * base.GraphicsDevice.GetViewScale() * 2;
                var frameOrigin = new Vector2((base.GraphicsDevice.Viewport.Width - frameScale.X) / 2, (base.GraphicsDevice.Viewport.Height - frameScale.Y) / 2);
                MenuFrameRect = new Rectangle((int)frameOrigin.X, (int)frameOrigin.Y, (int)frameScale.X, (int)frameScale.Y);
            }
            if (HasFocus && cachedMenuListOptions != null && drawer != null)
            {
                drawer.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                Vector2 position = Vector2.Zero;
                Vector2 titleLineSize;
                //draw title
                {
                    const string underlineStart = "\x1B[21m";
                    const string underlinePadding = "    ";
                    const string resetFormatting = "\x1B[0m";
                    const string nameStart = underlineStart + underlinePadding;
                    const string nameEnd = underlinePadding + resetFormatting;

                    string menuTitle = nameStart + CurrentMenuLevel.Name + nameEnd;

                    Vector2 titleScale = new Vector2(1.5f);
                    titleLineSize = RichTextRenderer.MeasureString(Fonts, menuTitle) * titleScale;
                    position.Y = MenuFrameRect.Y;
                    position.X = (GraphicsDevice.Viewport.Width / 2) - (titleLineSize.X / 2);
                    drawer.DrawTextRichShadow(Fonts, menuTitle, position, titleScale);
                    MenuFrameRect.Y += (int)titleLineSize.Y;
                    MenuFrameRect.Height -= (int)titleLineSize.Y;
                }
                drawer.End();

                const float lineHeightModifier = 8;

                Viewport viewport = GraphicsDevice.Viewport;
                //restrict drawing to inside menu frame
                GraphicsDevice.Viewport = new Viewport(MenuFrameRect);
                drawer.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                position = Vector2.Zero;
                position.Y = 0;
                int i = 0;
                position.Y -= scrollY;
                foreach (MenuListOption option in cachedMenuListOptions)
                {
                    bool selected = i == currentIndex;
                    string optionName = option.DisplayText;
                    Color textColor = Color.White;
                    Color bgColor = selected ? Color.Gray : Color.Black;
                    if (!option.Enabled)
                    {
                        textColor = Color.Gray;
                        bgColor = Color.Black;
                    }
                    string text = $"{(selected ? ">" : " ")} {optionName} {(selected ? "<" : " ")}";
                    Vector2 lineSize = RichTextRenderer.MeasureString(Fonts, text);
                    position.X = (GraphicsDevice.Viewport.Width / 2) - (lineSize.X / 2);
                    if (text.Contains($"{RichTextRenderer.C1_8BitCodes.CSI}{RichTextRenderer.SGRParameters.Framed}{RichTextRenderer.CSICommands.SGR}")
                    || text.Contains($"{RichTextRenderer.ESC}{RichTextRenderer.C1_EscapeSequences.CSI}{RichTextRenderer.SGRParameters.Framed}{RichTextRenderer.CSICommands.SGR}"))
                    {
                        position.Y += lineHeightModifier;
                    }
                    if (i == 0)
                    {
                        position.Y += selectedItemPaddingBlock;
                    }
                    option.BoundingClientRect = new Rectangle((int)position.X + MenuFrameRect.X, (int)position.Y + MenuFrameRect.Y, (int)lineSize.X, (int)lineSize.Y);
                    option.Index = i;
                    RichTextRenderer.DrawString(drawer, Fonts, text, position + Vector2.One, bgColor);
                    RichTextRenderer.DrawString(drawer, Fonts, text, position, textColor);
                    position.Y += lineSize.Y;
                    ++i;
                }
                contentHeight = position.Y + scrollY;
                bool overflowY = contentHeight > MenuFrameRect.Height;
                bool overflowTop = scrollY > scrollTop;
                bool overflowBottom = position.Y > MenuFrameRect.Height;
                if (overflowY)
                {
                    //draw scroll bar
                    int vHeight = GraphicsDevice.Viewport.Height;
                    int vWidth = GraphicsDevice.Viewport.Width;
                    int scrollBarSizeHeight = vHeight;
                    int scrollBarSizeWidth = (int)Math.Max(vWidth * 0.03, 5);
                    float scrollBarSizeWidthInnerOffsetInline = (float)Math.Max(vWidth * 0.003, 1);
                    int scrollBarArrowSize = scrollBarSizeWidth;
                    float scrollBarSizeWidthInnerOffsetBlock = scrollBarSizeWidth + scrollBarSizeWidthInnerOffsetInline;
                    float scrollBarSizePercent = MenuFrameRect.Height / contentHeight;
                    float scrollBarBottomEdgePercent = MenuFrameRect.Height / position.Y;
                    float scrollBarTopEdgePercent = scrollBarBottomEdgePercent - scrollBarSizePercent;

                    int scrollBarOriginX = vWidth - scrollBarSizeWidth;
                    int scrollBarOriginX_adjusted = scrollBarOriginX + MenuFrameRect.X;
                    int scrollBarOriginY_adjusted = MenuFrameRect.Y;

                    Color getArrowColor(bool hasMouseFocus, bool hasMouseInside)
                    {
                        return hasMouseFocus ? (hasMouseInside ? scrollArrowHeldColor : scrollArrowFocusColor) : (hasMouseInside ? scrollArrowHoverColor : scrollArrowIdleColor);
                    }
                    Color getArrowBackColor(bool hasMouseFocus, bool hasMouseInside)
                    {
                        return hasMouseFocus ? (hasMouseInside ? scrollButtonHeldColor : scrollButtonFocusColor) : (hasMouseInside ? scrollButtonHoverColor : scrollButtonIdleColor);
                    }

                    ScrollBarTrackHitbox = new Rectangle(scrollBarOriginX_adjusted,
                        scrollBarOriginY_adjusted,
                        scrollBarSizeWidth,
                        scrollBarSizeHeight).Inset(0, scrollBarArrowSize);
                    drawer.DrawRect(ScrollBarTrackHitbox.Inset(0, -scrollBarArrowSize).OffsetOrigin(-MenuFrameRect.X, -MenuFrameRect.Y),
                        scrollBarBgColor);

                    ScrollBarThumbHitbox = new Rectangle(scrollBarOriginX_adjusted,
                        scrollBarOriginY_adjusted + (int)(scrollBarSizeHeight * scrollBarTopEdgePercent),
                        scrollBarSizeWidth,
                        (int)(scrollBarSizeHeight * scrollBarSizePercent)
                    ).Inset(0, scrollBarSizeWidthInnerOffsetBlock);
                    drawer.DrawRect(ScrollBarThumbHitbox.Inset(scrollBarSizeWidthInnerOffsetInline, 0).OffsetOrigin(-MenuFrameRect.X, -MenuFrameRect.Y),
                        ScrollBarThumbHasFocus ? scrollThumbFocusColor : (ScrollBarThumbHasHover ? scrollThumbHoverColor : scrollThumbIdleColor));

                    var ScrollBarArrowUpRect = new Rectangle(
                        scrollBarOriginX,
                        0,
                        scrollBarArrowSize,
                        scrollBarArrowSize
                    );
                    ScrollBarArrowUpHitbox = ScrollBarArrowUpRect.OffsetOrigin(MenuFrameRect.X, MenuFrameRect.Y);
                    bool upScrollInside = scrollButtonUp.containsPointer;
                    bool upScrollFocus = scrollButtonUp.focus;
                    ScrollBarArrowUpRect = ScrollBarArrowUpRect.Inset(scrollBarSizeWidthInnerOffsetInline);
                    drawer.DrawRect(ScrollBarArrowUpRect, getArrowBackColor(upScrollFocus, upScrollInside));
                    ScrollBarArrowUpRect = ScrollBarArrowUpRect.Inset(scrollBarSizeWidthInnerOffsetInline).OffsetOriginForRotateRect();
                    Color upArrowColor = getArrowColor(upScrollFocus, upScrollInside);
                    drawer.Draw(arrowTexture, ScrollBarArrowUpRect, null, upArrowColor, (float)(Math.PI / 2), new Vector2(arrowTexture.Width / 2, arrowTexture.Height / 2), SpriteEffects.None, 0);

                    Rectangle ScrollBarArrowDownRect = new Rectangle(
                        scrollBarOriginX,
                        scrollBarSizeHeight - scrollBarArrowSize,
                        scrollBarArrowSize,
                        scrollBarArrowSize
                    );
                    ScrollBarArrowDownHitbox = ScrollBarArrowDownRect.OffsetOrigin(MenuFrameRect.X, MenuFrameRect.Y);
                    bool downScrollInside = scrollButtonDown.containsPointer;
                    bool downScrollFocus = scrollButtonDown.focus;
                    ScrollBarArrowDownRect = ScrollBarArrowDownRect.Inset(scrollBarSizeWidthInnerOffsetInline);
                    drawer.DrawRect(ScrollBarArrowDownRect, getArrowBackColor(downScrollFocus, downScrollInside));
                    ScrollBarArrowDownRect = ScrollBarArrowDownRect.Inset(scrollBarSizeWidthInnerOffsetInline).OffsetOriginForRotateRect();
                    Color downArrowColor = getArrowColor(downScrollFocus, downScrollInside);
                    drawer.Draw(arrowTexture2, ScrollBarArrowDownRect, null, downArrowColor, (float)(Math.PI / 2), new Vector2(arrowTexture2.Width / 2, arrowTexture2.Height / 2), SpriteEffects.None, 0);
                    if (overflowTop)
                    {
                        //TODO indicate overflow on top edge of container? 
                        float width = titleLineSize.X;
                        float widthDiff = width / 20f;
                        Color color = Color.White;
                        for (int ii = 1; width > 0; ++ii)
                        {
                            drawer.DrawRect(new Vector2((MenuFrameRect.Width - width) / 2f, ii), width, 1, color);
                            width -= widthDiff;
                            color = Color.White * (float)Math.Pow(0.85f, ii);
                        }
                    }
                    if (overflowBottom)
                    {
                        // indicate overflow on bottom edge of container
                        float width = titleLineSize.X;
                        float widthDiff = width / 20f;
                        Color color = Color.White;
                        for (int ii = 1; width > 0; ++ii)
                        {
                            drawer.DrawRect(new Vector2((MenuFrameRect.Width - width) / 2f, MenuFrameRect.Height - ii), width, 1, color);
                            width -= widthDiff;
                            color = Color.White * (float)Math.Pow(0.85f, ii);
                        }
                    }
                }
                MenuListOption menuitem = CurrentMenuItem;
                if (menuitem != null)
                {
                    int paddingInline = menuitem.BoundingClientRect.Height / selectedItemOutlinePaddingInlineEmFrac;
                    int paddingBlock = 0;
                    Vector2 origin = new Vector2(menuitem.BoundingClientRect.X - paddingInline - MenuFrameRect.X, menuitem.BoundingClientRect.Y - paddingBlock - MenuFrameRect.Y + scrollY);
                    Vector2 scale = new Vector2(menuitem.BoundingClientRect.Width + (2 * paddingInline), menuitem.BoundingClientRect.Height + (2 * paddingBlock));
                    if(!selectorOrigin.HasValue)
                    {
                        selectorOrigin = origin;
                    }
                    if(!selectorScale.HasValue)
                    {
                        selectorScale = scale;
                    }
                    selectorOrigin = Vector2.Lerp(selectorOrigin.Value, origin, 0.3f);
                    selectorScale = Vector2.Lerp(selectorScale.Value, scale, 0.3f);
                    origin = selectorOrigin.Value;
                    origin.Y -= scrollY;
                    drawer.DrawRectWireframe(origin,
                        selectorScale.Value.X,
                        selectorScale.Value.Y,
                        selectedItemBorderThickness,
                        Color.White);
                }
                drawer.End();
                GraphicsDevice.Viewport = viewport;

                //fix to get the cursor to appear on top of the menu
                if (ClickedCursor != null)
                {
                    drawer.BeginPoint();
                    float num3 = GraphicsDevice.GetViewScale() * 2f;
                    Point point = MouseState.PositionInViewport();
                    drawer.Draw(CursorClicking ? ClickedCursor : (CursorSelectable ? CanClickCursor : PointerCursor), new Vector2((float)point.X - num3 * 11.5f, (float)point.Y - num3 * 8.5f), null, new Color(1f, 1f, 1f, FezMath.Saturate(1f - (SinceMouseMoved - 2f))), 0f, Vector2.Zero, num3, SpriteEffects.None, 0f);
                    drawer.End();
                }
            }
        }
    }
}

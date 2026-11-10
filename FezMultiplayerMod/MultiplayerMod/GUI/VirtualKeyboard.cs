using FezEngine.Components;
using FezEngine.Structure.Input;
using FezEngine.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FezGame.MultiplayerMod
{
    /// <summary>
    /// Virtual Keyboard so users can enter text using only a gamepad
    /// </summary>
    internal class VirtualKeyboard : DrawableGameComponent
    {
        private IInputManager InputManager { get; set; }
        private IFontManager FontManager { get; set; }

        private char[][] CharGrid = [];
        private record class Position(int Row, int Col)
        {
            public int Row { get; set; } = Row;
            public int Col { get; set; } = Col;
        }

        private readonly Position position = new(0, 0);

        internal VirtualKeyboard(Game game) : base(game)
        {
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
            DrawOrder = ServerListMenu.Instance.DrawOrder + 1;//TODO?
        }

        public event Action<char> OnInput = (char ch) => { };
        private static double blinkStartTime = 0d;
        private GamepadState gamepad = null;
        public override void Update(GameTime gameTime)
        {
            if (!Visible)
            {
                return;
            }
            gamepad = InputManager.ActiveGamepad;
            if (!gamepad.Connected)
            {
                return;
            }
            double totalSeconds = gameTime.TotalGameTime.TotalSeconds;
            if (InputManager.Jump == FezButtonState.Pressed || InputManager.Start == FezButtonState.Pressed)
            {
                char selection = GetChar(position);
                if (selection != '\0')
                {
                    //TODO make this call TextInputEXT_TextInput on the relevant TextInputLogicComponent
                    OnInput(selection);
                }
            }
            bool AnyPressed(params TimedButtonState[] buttons)
            {
                return buttons.Any(state => state.State == FezButtonState.Pressed);
            }
            if (AnyPressed(gamepad.DPad.Down, gamepad.LeftStick.Down, gamepad.RightStick.Down))
            {
                position.Row += 1;
            }
            if (AnyPressed(gamepad.DPad.Up, gamepad.LeftStick.Up, gamepad.RightStick.Up))
            {
                position.Row -= 1;
            }
            if (AnyPressed(gamepad.DPad.Left, gamepad.LeftStick.Left, gamepad.RightStick.Left))
            {
                position.Col -= 1;
            }
            if (AnyPressed(gamepad.DPad.Right, gamepad.LeftStick.Right, gamepad.RightStick.Right))
            {
                position.Col += 1;
            }
        }
        private static readonly Dictionary<char, string> CharToStringCache = new()
        {
            ['\0'] = "",
            ['\x7F'] = "DEL",
            ['\x08'] = "BACK",
            [TextInputLogicComponent.CLEAR_CHAR] = "CLEAR",
            ['\n'] = "ENTER",
        };
        private static string GetString(char ch)
        {
            if (!CharToStringCache.TryGetValue(ch, out string cachedString))
            {
                cachedString = ch.ToString();
                CharToStringCache[ch] = cachedString;
            }

            return cachedString;
        }
        private const double cursorBlinksPerSecond = 0.8d;
        public override void Draw(GameTime gameTime)
        {
            if (this.disposing || !Visible || !ServiceHelper.FirstLoadDone || FontManager == null)
            {
                return;
            }
            if (gamepad == null || !gamepad.Connected)
            {
                return;
            }

            double totalSeconds = gameTime.TotalGameTime.TotalSeconds;
            bool blinkActive = (((totalSeconds - blinkStartTime) * cursorBlinksPerSecond) % 1.0d) < 0.5d;

            SpriteBatch batch = new SpriteBatch(Game.GraphicsDevice);
            batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            //TODO draw the CharGrid
            for (int r = 0; r < CharGrid.Length; r++)
            {
                char[] Row = CharGrid[r];
                Vector2 pos = Vector2.Zero;
                Color backColor = Color.White;
                Color textColor = Color.White;

                for (int c = 0; c < Row.Length; c++)
                {
                    char ch = Row[c];
                    string str = GetString(ch);

                    //TODO draw the buttons and stuff

                    //batch.DrawRect(blah blah blah);

                    if (position.Row == r && position.Col == c)
                    {
                        //TODO draw current selection box

                        if (InputManager.Jump != FezButtonState.Up || InputManager.Start != FezButtonState.Up)
                        {
                            //TODO draw active selection box
                        }
                    }
                    batch.DrawTextRichShadow(FontManager, str, pos);
                }
            }
            batch.End();
        }

        private void ConstrainPosition()
        {
            position.Row = Math.Clamp(position.Row, 0, CharGrid.Length - 1);
            position.Col = Math.Clamp(position.Col, 0, CharGrid[position.Row].Length - 1);
        }
        private char GetChar(Position position)
        {
            ConstrainPosition();
            return CharGrid[position.Row][position.Col];
        }

        public void SetCharGrid(VKLayout layout)
        {
            CharGrid = layout.LayoutData.CharGrid;
        }
        internal readonly struct LayoutData(char[][] charGrid)
        {
            internal char[][] CharGrid { get; init; } = charGrid;
        }
        public record VKLayout
        {
            private const char DEL = '\x7F';
            private const char BCK = '\x08';
            private const char ENTR = '\n';
            private const char CLR = TextInputLogicComponent.CLEAR_CHAR;
            public static readonly VKLayout Alphanumeric = Alphanumeric_upper;
            public static readonly VKLayout Alphanumeric_upper = new([
                ['0','1','2','3','4','5','6','7','8','9'],
                ['A','B','C','D','E','F','G','H','I','J'],
                ['K','L','M','N','O','P','Q','R','S','T'],
                ['U','V','W','X','Y','Z','?','!',' ',ENTR],
            ]);
            public static readonly VKLayout Alphanumeric_lower = new([
                ['a','b','c','d','e','f','g','h','i','j'],
                ['k','l','m','n','o','p','q','r','s','t'],
                ['u','v','w','x','y','z','?','!',' ',ENTR],
            ]);
            public static readonly VKLayout Symbols = new([
                [',','.',':',';','!','?','_','-','+','='],
                ['<','>','{','}','(',')','|','/','\"','\'',],
                ['~','@','#','$','%','^','&','*',' ',ENTR],
            ]);
            public static readonly VKLayout IPv4 = IPv4_alt1;
            public static readonly VKLayout IPv4_alt1 = new([
                ['1','2','3'],
                ['4','5','6'],
                ['7','8','9'],
                ['0','.',ENTR],
                [CLR,BCK,ENTR],
            ]);
            public static readonly VKLayout IPv4_alt2 = new([
                ['7','8','9'],
                ['4','5','6'],
                ['1','2','3'],
                ['0','.',BCK],
                [CLR,ENTR,ENTR],
            ]);
            public static readonly VKLayout IPv6 = IPv6_alt1;
            public static readonly VKLayout IPv6_alt1 = new([
                ['A','B','C'],
                ['D','E','F'],
                ['1','2','3'],
                ['4','5','6'],
                ['7','8','9'],
                ['0','.',':'],
                [CLR,BCK,ENTR],
            ]);
            public static readonly VKLayout IPv6_alt2 = new([
                ['1','2','3','A','B'],
                ['4','5','6','C','D'],
                ['7','8','9','E','F'],
                ['0','.',':','\0','\0'],
                ['\0','\0',CLR,BCK,ENTR],
            ]);

            internal LayoutData LayoutData;
            private VKLayout(char[][] matrix)
            {
                LayoutData = new LayoutData { CharGrid = matrix };
            }
        }
    }
}
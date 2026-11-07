using FezEngine.Components;
using FezEngine.Structure.Input;
using FezEngine.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace FezGame.MultiplayerMod
{
    internal class VirtualKeyboard : DrawableGameComponent
    {
        private IInputManager InputManager { get; set; }
        private IFontManager FontManager { get; set; }

        private char[][] CharGrid = [];

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
        public override void Update(GameTime gameTime)
        {
            if (!Visible)
            {
                return;
            }
            GamepadState gamepad = InputManager.ActiveGamepad;
            if (!gamepad.Connected)
            {
                return;
            }
            double totalSeconds = gameTime.TotalGameTime.TotalSeconds;
            if (InputManager.Jump == FezButtonState.Pressed || InputManager.Start == FezButtonState.Pressed)
            {

            }
            //TODO somehow call TextInputEXT_TextInput on the relevant TextInputLogicComponent
        }
        private const double cursorBlinksPerSecond = 0.8d;
        public override void Draw(GameTime gameTime)
        {
            if (this.disposing || !Visible || !ServiceHelper.FirstLoadDone || FontManager == null)
            {
                return;
            }

            double totalSeconds = gameTime.TotalGameTime.TotalSeconds;
            bool blinkActive = (((totalSeconds - blinkStartTime) * cursorBlinksPerSecond) % 1.0d) < 0.5d;

            //TODO
        }

        public void SetCharGrid()
        {
            CharGrid = VKLayout.Alphanumeric.LayoutData.CharGrid;
        }
        private readonly struct LayoutData(char[][] charGrid)
        {
            internal char[][] CharGrid { get; init; } = charGrid;
        }
        private record VKLayout
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
            public static readonly VKLayout IPv4 = new([
                ['1','2','3'],
                ['4','5','6'],
                ['7','8','9'],
                ['0','.',ENTR],
                [CLR,BCK,ENTR],
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
                ['0','.',':'],
                [CLR,BCK,ENTR],
            ]);

            internal LayoutData LayoutData;
            private VKLayout(char[][] matrix)
            {
                LayoutData = new LayoutData { CharGrid = matrix };
            }
        }
    }
}
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
        private static IFontManager FontManager { get; set; }

        private char[][] CharGrid = [];
        private static readonly Color BackColor = Color.Gray;
        private static readonly Color TextColor = Color.White;
        private class KeyDrawingMetadata
        {
            private string Str { get; init; }
            private Vector2 TextOrigin { get; init; }
            private float TextScale { get; init; }
            private char KeyChar { get; init; }
            private Rectangle Rect { get; init; }
            internal Position Pos { get; init; }
            public KeyDrawingMetadata(char keyChar, Rectangle rect, Position pos)
            {
                KeyChar = keyChar;
                Rect = rect;
                Pos = pos;
                Str = GetString(KeyChar);
                var txtSize = RichTextRenderer.MeasureString(FontManager, Str);
                var rectCenterP = rect.Center;
                var rectCenter = new Vector2(rectCenterP.X, rectCenterP.Y);
                float scaleX = rect.Width / txtSize.X;
                float scaleY = rect.Height / txtSize.Y;
                TextScale = Math.Min(1.0f, Math.Min(scaleX, scaleY));

                TextOrigin = rectCenter - txtSize * TextScale / 2;
            }

            public void Draw(GameTime gameTime, SpriteBatch batch, bool selected, bool isDown, Vector2 offset)
            {

                //TODO draw the buttons and stuff
                batch.DrawRect(Rect.OffsetOrigin((int)offset.X, (int)offset.Y), BackColor);

                if (selected)
                {
                    //TODO draw current selection box

                    if (isDown)
                    {
                        //TODO draw active selection box
                    }
                }
                batch.DrawTextRichShadow(FontManager, Str, TextOrigin + offset, TextScale, TextColor);
            }
        }
        public Rectangle BoardRect { get; private set; } = Rectangle.Empty;
        private readonly List<KeyDrawingMetadata> KeyBoxes = [];
        private Rectangle CalculateBoardPosition(int rows, int cols)
        {
            if (rows == 0 || cols == 0) return Rectangle.Empty;

            // Get the available screen boundaries from XNA
            var viewport = Game.GraphicsDevice.Viewport;
            int screenWidth = viewport.Width;
            int screenHeight = viewport.Height;

            int maxWidth = screenWidth / 2;
            int maxHeight = screenHeight / 2;

            int cellSize = Math.Min(maxWidth / cols, maxHeight / rows);

            int boardWidth = cellSize * cols;
            int boardHeight = cellSize * rows;

            // Snap board to bottom right corner
            int boardX = screenWidth - boardWidth;
            int boardY = screenHeight - boardHeight;

            Rectangle boardRect = new Rectangle(boardX, boardY, boardWidth, boardHeight);
            return boardRect;
        }

        private void RebuildKeyBoxes()
        {
            KeyBoxes.Clear();
            if (CharGrid == null)
            {
                return;
            }

            const int gap = 2;

            int cols = CharGrid.Max(c => c?.Length ?? 0);
            int rows = CharGrid.Length;

            /*
             * boardRect is supposed to be like whatever fits in viewport
             * while keeping the elements in a 1:1 aspect ratio
             */
            Rectangle boardRect = CalculateBoardPosition(rows, cols);

            // Total space taken up by gaps between cells
            int totalGapWidth = gap * (cols - 1);
            int totalGapHeight = gap * (rows - 1);

            // Size of each cell after subtracting the gaps
            int cellWidth = (boardRect.Width - totalGapWidth) / cols;
            int cellHeight = (boardRect.Height - totalGapHeight) / rows;

            for (int r = 0; r < CharGrid.Length; r++)
            {
                char[] Row = CharGrid[r];
                Vector2 pos = Vector2.Zero;

                for (int c = 0; c < Row.Length; c++)
                {
                    char ch = Row[c];

                    int pixelX = boardRect.X + c * (cellWidth + gap);
                    int pixelY = boardRect.Y + r * (cellHeight + gap);

                    var keyPos = new Position(pixelX, pixelY);
                    var keyRect = new Rectangle(
                        pixelX,
                        pixelY,
                        cellWidth,
                        cellHeight
                    );

                    KeyBoxes.Add(new KeyDrawingMetadata(ch, keyRect, keyPos));
                }
            }
        }
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
            GraphicsDevice.DeviceReset -= GraphicsDevice_DeviceReset;

            base.Dispose();
        }

        private readonly SpriteBatch drawer;
        public override void Initialize()
        {
            DrawOrder = ServerListMenu.Instance.DrawOrder + 1;//TODO?
            GraphicsDevice.DeviceReset += GraphicsDevice_DeviceReset;
        }

        private void GraphicsDevice_DeviceReset(object sender, EventArgs e)
        {
            RebuildKeyBoxes();
        }

        public event Action<char> OnInput = (char ch) => { };
        private static double blinkStartTime = 0d;
        private GamepadState gamepad = null;
        public override void Update(GameTime gameTime)
        {
            if (this.disposing || !Visible || !ServiceHelper.FirstLoadDone || FontManager == null)
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

            bool isDown = InputManager.Jump != FezButtonState.Up || InputManager.Start != FezButtonState.Up;
            //draw the CharGrid
            Vector2 offset = Vector2.Zero;
            foreach (var boxMeta in KeyBoxes)
            {
                bool currentSelection = position == boxMeta.Pos;
                boxMeta.Draw(gameTime, batch, currentSelection, isDown, offset);
            }
            Vector2 debugTextPos = new(50, 50);
            batch.DrawString(FontManager.Big, position.ToString(), debugTextPos, Color.White);
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
            RebuildKeyBoxes();
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
            public static VKLayout Alphanumeric => Alphanumeric_upper;
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
            public static VKLayout IPv4 => IPv4_alt1;
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
            public static VKLayout IPv6 => IPv6_alt1;
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
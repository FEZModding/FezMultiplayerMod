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
    /// <remarks>
    /// Note: Intentially does not support mouse input. This is because most modern systems already have an On-Screen Keyboard that supports the mouse.
    /// </remarks>
    internal class VirtualKeyboard : DrawableGameComponent
    {
        private IInputManager InputManager { get; set; }
        private static IFontManager FontManager { get; set; }

        private char[][] CharGrid = [];
        private static readonly Color BackColor = Color.Gray * 0.9f;
        private static readonly Color TextColor = Color.White;
        private static readonly float KeyTextMinPaddingPercentage = 0.8f;
        private class KeyDrawingMetadata
        {
            private string Str { get; init; }
            private Vector2 TextOrigin { get; init; }
            private float TextScale { get; init; }
            private char KeyChar { get; init; }
            private Rectangle Rect { get; init; }
            internal GridIndex Pos { get; init; }
            public KeyDrawingMetadata(char keyChar, Rectangle rect, GridIndex pos)
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
                TextScale = Math.Min(1f, Math.Min(scaleX, scaleY) * KeyTextMinPaddingPercentage);

                TextOrigin = rectCenter - txtSize * TextScale / 2f;
            }

            public void Draw(GameTime gameTime, SpriteBatch batch, bool selected, bool isDown, Vector2 offset)
            {
                var rect = Rect.OffsetOrigin((int)offset.X, (int)offset.Y);
                //TODO draw the buttons and stuff
                batch.DrawRect(rect, BackColor);

                if (selected)
                {
                    //TODO draw current selection box
                    batch.DrawRectWireframe(rect, 2f, Color.White);

                    if (isDown)
                    {
                        batch.DrawRectWireframe(rect, 5f, Color.Gold);
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

            const float MaxScreenWidthPercent = 0.8f;
            int maxWidth = (int)(screenWidth * MaxScreenWidthPercent);
            const float bottomPadPercent = 0.05f;
            const float MaxScreenHeightPercent = 0.45f;
            int bottomPad = (int)(screenHeight * bottomPadPercent);
            int maxHeight = (int)(screenHeight * (MaxScreenHeightPercent - bottomPadPercent));

            int cellSize = Math.Min(maxWidth / cols, maxHeight / rows);

            int boardWidth = cellSize * cols;
            int boardHeight = cellSize * rows;

            // Snap board to bottom center
            int boardX = (screenWidth - boardWidth) / 2;
            int boardY = screenHeight - boardHeight - bottomPad;

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

                    var keyPos = new GridIndex(r, c);
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
        private record class GridIndex(int Row, int Col)
        {
            public int Row { get; set; } = Row;
            public int Col { get; set; } = Col;
        }

        private readonly GridIndex position = new(0, 0);

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
            DrawOrder = ServerListMenu.Instance.DrawOrder + 1;
            GraphicsDevice.DeviceReset += GraphicsDevice_DeviceReset;
        }

        private void GraphicsDevice_DeviceReset(object sender, EventArgs e)
        {
            RebuildKeyBoxes();
        }

        public event Action<char> OnInput = (char ch) => { };
        private GamepadState gamepad = null;
        public TextInputLogicComponent attachedTextInputComponent = null;
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
                string selection_str = GetString(selection);
                Common.Logger.Log(nameof(VirtualKeyboard), selection_str);
                if (selection != '\0')
                {
                    bool isPrivateUse = char.IsBetween(selection, '\uE000', '\uF8FF');
                    if (isPrivateUse)
                    {
                        switch ((SpecialPrivateUseChars)selection)
                        {
                        case SpecialPrivateUseChars.ChangeVK_Alphanumeric_lower:
                            this.SetLayout(VKLayout.Alphanumeric_lower);
                            break;
                        case SpecialPrivateUseChars.ChangeVK_Alphanumeric_upper:
                            this.SetLayout(VKLayout.Alphanumeric_upper);
                            break;
                        case SpecialPrivateUseChars.ChangeVK_Symbols:
                            this.SetLayout(VKLayout.Symbols);
                            break;
                        case SpecialPrivateUseChars.CaretMove_Left:
                            attachedTextInputComponent?.MoveCaret(-1);
                            break;
                        case SpecialPrivateUseChars.CaretMove_Right:
                            attachedTextInputComponent?.MoveCaret(1);
                            break;
                        default:
                            break;
                        }
                    }
                    else
                    {
                        //this calls TextInputEXT_TextInput on the relevant TextInputLogicComponent
                        OnInput(selection);
                    }
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
            ConstrainPosition();
        }
        /// <summary>
        /// Special Private Use Characters used by this class for fuctionality purposes. 
        /// </summary>
        /// <remarks>
        /// values must be between '\uE000' and '\uF8FF'
        /// </remarks>
        private enum SpecialPrivateUseChars
        {
            ChangeVK_Alphanumeric_upper = '\xE100',
            ChangeVK_Alphanumeric_lower = '\xE101',
            ChangeVK_Symbols = '\xE102',
            CaretMove_Left = '\xE201',
            CaretMove_Right = '\xE202',
        }
        private static readonly Dictionary<char, string> CharToStringCache = new()
        {
            ['\0'] = "",
            ['\x7F'] = "DEL",
            ['\x08'] = "BACK",
            [TextInputLogicComponent.CLEAR_CHAR] = "CLEAR",
            [(char)SpecialPrivateUseChars.ChangeVK_Alphanumeric_lower] = "LOWER",
            [(char)SpecialPrivateUseChars.ChangeVK_Alphanumeric_upper] = "UPPER",
            [(char)SpecialPrivateUseChars.ChangeVK_Symbols] = "SYMBOL",
            [(char)SpecialPrivateUseChars.CaretMove_Left] = "LEFT",
            [(char)SpecialPrivateUseChars.CaretMove_Right] = "RIGHT",
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
        private Vector2 offset = Vector2.Zero;
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

            drawer.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);

            bool isDown = InputManager.Jump != FezButtonState.Up || InputManager.Start != FezButtonState.Up;
            //draw the CharGrid
            foreach (var boxMeta in KeyBoxes)
            {
                bool currentSelection = position == boxMeta.Pos;
                boxMeta.Draw(gameTime, drawer, currentSelection, isDown, offset);
            }

#if DEBUG
            Vector2 debugTextPos = new(GraphicsDevice.Viewport.Width / 4f * 3f, GraphicsDevice.Viewport.Height / 2f);
            drawer.DrawString(FontManager.Big, position.ToString(), debugTextPos, Color.White);
#endif
            drawer.End();
        }

        private void ConstrainPosition()
        {
            if (CharGrid.Length == 0)
            {
                position.Row = 0;
                position.Col = 0;
                return;
            }
            int maxRow = Math.Max(0, CharGrid.Length - 1);
            position.Row = Math.Clamp(position.Row, 0, maxRow);
            if (CharGrid[position.Row].Length == 0)
            {
                return;
            }
            int maxCol = Math.Max(0, CharGrid[position.Row].Length - 1);
            position.Col = Math.Clamp(position.Col, 0, maxCol);
        }
        private char GetChar(GridIndex position)
        {
            ConstrainPosition();
            return CharGrid[position.Row][position.Col];
        }

        public void SetLayout(VKLayout layout)
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
            private const char DEL = '\x7f';
            private const char BCK = '\x08';
            private const char ENTR = '\n';
            private const char NUL = '\0';
            private const char CLR = TextInputLogicComponent.CLEAR_CHAR;
            private const char UPR = (char)SpecialPrivateUseChars.ChangeVK_Alphanumeric_upper;
            private const char LWR = (char)SpecialPrivateUseChars.ChangeVK_Alphanumeric_lower;
            private const char SYM = (char)SpecialPrivateUseChars.ChangeVK_Symbols;
            private const char LFT = (char)SpecialPrivateUseChars.CaretMove_Left;
            private const char RGT = (char)SpecialPrivateUseChars.CaretMove_Right;
            public static VKLayout Alphanumeric => Alphanumeric_upper;
            //TODO make an alphanumeric keyboard that has a shift button?
            public static readonly VKLayout Alphanumeric_upper = new([
                ['0', '1', '2', '3', '4', '5', '6', '7','8', '9'],
                ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H','I', 'J'],
                ['K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R','S', 'T'],
                ['U', 'V', 'W', 'X', 'Y', 'Z', ' ', LWR,BCK, ENTR],
            ]);
            public static readonly VKLayout Alphanumeric_lower = new([
                ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9'],
                ['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j'],
                ['k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't'],
                ['u', 'v', 'w', 'x', 'y', 'z', ' ', SYM, BCK, ENTR],
            ]);
            public static readonly VKLayout Symbols = new([
                [',', '.', ':', ';', '!', '?', '_', '-', '+', '='],
                ['<', '>', '{', '}', '(', ')', '[', ']','\"','\''],
                ['~', '@', '#', '$', '%', '^', '&', '*', NUL, NUL],
                ['/','\\', '|', NUL, NUL, NUL, NUL, UPR, BCK, ENTR],
            ]);
            public static VKLayout IPv4 => IPv4_alt1;
            public static readonly VKLayout IPv4_alt1 = new([
                ['1', '2', '3'],
                ['4', '5', '6'],
                ['7', '8', '9'],
                ['0', '.', ':'],
                [CLR, BCK,ENTR],
            ]);
            public static readonly VKLayout IPv4_alt2 = new([
                ['7', '8', '9'],
                ['4', '5', '6'],
                ['1', '2', '3'],
                ['0', '.', ':'],
                [CLR, BCK,ENTR],
            ]);
            public static VKLayout IPv6 => IPv6_alt2;
            public static readonly VKLayout IPv6_alt1 = new([
                ['A', 'B', 'C'],
                ['D', 'E', 'F'],
                ['1', '2', '3'],
                ['4', '5', '6'],
                ['7', '8', '9'],
                ['0', '.', ':'],
                [CLR, BCK,ENTR],
            ]);
            public static readonly VKLayout IPv6_alt2 = new([
                ['1', '2', '3', 'A', 'B'],
                ['4', '5', '6', 'C', 'D'],
                ['7', '8', '9', 'E', 'F'],
                ['0', '.', ':', LFT, RGT],
                [CLR, NUL, DEL, BCK, ENTR],
            ]);

            internal LayoutData LayoutData;
            private VKLayout(char[][] matrix)
            {
                LayoutData = new LayoutData { CharGrid = matrix };
            }
        }
    }
}
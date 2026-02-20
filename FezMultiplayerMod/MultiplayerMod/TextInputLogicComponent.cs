using FezEngine.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;//Note: TextInputEXT is only in FNA
using System;

namespace FezGame.MultiplayerMod
{
    internal class TextInputLogicComponent : GameComponent
    {
        public static int TextboxPadRight = 30;
        private string value = "";
        private bool showCaret = false;
        private const string caret = "|";
        private int caretPosition = 0;
        private readonly double caretBlinksPerSecond = 0.8d;

        public string Value
        {
            get => value;
            set
            {
                this.value = value;
                _ = ConstrainCaretPosition();
                OnUpdate(false);
            }
        }

        public int MaxLength = 10000;
        public event Action<bool> OnUpdate = (onlyCaret) => { };
        /// <summary>
        /// If this textbox has focus
        /// </summary>
        private bool hasFocus = false;
        /// <summary>
        /// If this textbox has focus
        /// </summary>
        public bool HasFocus
        {
            get => hasFocus;
            set
            {
                hasFocus = value;
                if (value)
                {
                    //ensure text input is on
                    TextInputEXT.StartTextInput();
                }
                showCaret = true;
                OnUpdate(false);
            }
        }

        private int ConstrainCaretPosition()
        {
            return caretPosition = Math.Min(Math.Max(0, caretPosition), Value.Length);
        }

        public static double keyRepeatDelay = 0.3d;
        public static double keyRepeatInterval = 0.03d;
        private readonly KeyRepeatState left, right;
        public TextInputLogicComponent(Game game) : base(game)
        {
            TextInputEXT.TextInput += TextInputEXT_TextInput;

            left = new KeyRepeatState(Keys.Left, (totalSeconds) =>
            {
                caretPosition -= 1;
                showCaret = true;
                blinkStartTime = totalSeconds;
                OnUpdate(false);
            });
            right = new KeyRepeatState(Keys.Right, (totalSeconds) =>
            {
                caretPosition += 1;
                showCaret = true;
                blinkStartTime = totalSeconds;
                OnUpdate(false);
            });
        }

        private void RemoveFromValue(int startIndex, int count)
        {
            string removedText = Value.Substring(startIndex, count);
            Value = Value.Remove(startIndex, count);
            if (fonts != null)
            {
                scrollX -= RichTextRenderer.MeasureString(fonts, removedText).X;
                scrollX = Math.Max(0f, scrollX);
            }
        }
        private void InsertIntoValueAtCaret(string text)
        {
            Value = Value.Insert(caretPosition, text);
            caretPosition += text.Length;
        }

        private void TextInputEXT_TextInput(char ch)
        {
            if (!HasFocus)
            {
                return;
            }
            switch (ch)
            {
            case '\x01'://Start of Heading
            case '\x02'://Start of Text ("Home" key)
                caretPosition = 0;
                break;
            case '\x03'://End of Text ("End" key)
                caretPosition = Value.Length;
                break;
            case '\x04'://End of Transmission
            case '\x05'://Enquiry
            case '\x06'://Acknowledge
            case '\x07'://Bell, Alert
                break;
            case '\x08'://Backspace
                if (caretPosition > 0)
                {
                    int lastCaretPosition = caretPosition;
                    RemoveFromValue(caretPosition - 1, 1);
                    if (caretPosition > 0 && lastCaretPosition <= Value.Length)
                    {
                        caretPosition -= 1;
                    }
                }
                break;
            case '\t'://Horizontal Tab (\x09)
            case '\n'://Line Feed (\x0A)
            case '\v'://Vertical Tabulation (\x0B)
            case '\f'://Form Feed (\x0C)
            case '\r'://Carriage Return (\x0D)
                break;
            case '\x0E'://Shift Out
            case '\x0F'://Shift In
            case '\x10'://Data Link Escape
            case '\x11'://Device Control One
            case '\x12'://Device Control Two
            case '\x13'://Device Control Three
            case '\x14'://Device Control Four
            case '\x15'://Negative Acknowledge
                break;
            case '\x16'://Synchronous Idle (Ctrl + V)
                string paste = SDL2.SDL.SDL_GetClipboardText();
                InsertIntoValueAtCaret(paste);
                break;
            case '\x17'://End of Transmission Block
            case '\x18'://Cancel
            case '\x19'://End of medium
            case '\x1A'://Substitute
            case '\x1B'://Escape
            case '\x1C'://File Separator
            case '\x1D'://Group Separator
            case '\x1E'://Record Separator
            case '\x1F'://Unit Separator
                break;
            case '\x7F'://Delete
                if (caretPosition < Value.Length)
                {
                    RemoveFromValue(caretPosition, 1);
                }
                break;
            default:
                if (Value.Length < MaxLength)
                {
                    InsertIntoValueAtCaret(ch.ToString());
                }
                break;
            }
            OnUpdate(true);
        }

        private static double blinkStartTime = 0d;
        public override void Update(GameTime gameTime)
        {
            if (!HasFocus)
            {
                left.ClearKeyState();
                right.ClearKeyState();
                return;
            }
            double totalSeconds = gameTime.TotalGameTime.TotalSeconds;
            KeyboardState keyboard = Keyboard.GetState();
            left.UpdateKeyState(keyboard, totalSeconds);
            right.UpdateKeyState(keyboard, totalSeconds);
            ConstrainCaretPosition();
        }
        private float scrollX = 0;
        private IFontManager fonts = null;
        public void Draw(SpriteBatch drawer, IFontManager fonts, GameTime gameTime, Rectangle drawRect, Color bgColor, Color textColor, out bool hasOverflowLeft, out bool hasOverflowRight)
        {
            this.fonts = fonts;
            double totalSeconds = gameTime.TotalGameTime.TotalSeconds;
            bool newShowCaretValue = (((totalSeconds - blinkStartTime) * caretBlinksPerSecond) % 1.0d) < 0.5d;
            if (newShowCaretValue != showCaret)
            {
                showCaret = newShowCaretValue;
                OnUpdate(true);
            }
            ConstrainCaretPosition();
            float caretX = RichTextRenderer.MeasureString(fonts, Value.StripAnsiEscapeSequences().Substring(0, caretPosition)).X;
            float textWidth = RichTextRenderer.MeasureString(fonts, Value).X;
            float caretWidth = RichTextRenderer.MeasureString(fonts, caret).X;
            float viewWidth = drawRect.Width;
            if (caretX + caretWidth > scrollX + viewWidth)
            {
                scrollX = caretX - viewWidth + caretWidth;
            }
            else if (caretX < scrollX)
            {
                scrollX = caretX;
            }
            hasOverflowLeft = scrollX > 0;
            hasOverflowRight = (scrollX + viewWidth) < textWidth;
            Vector2 position = new Vector2(drawRect.X, drawRect.Y);
            if (HasFocus)
            {
                RichTextRenderer.DrawString(drawer, fonts, caret, new Vector2(caretX - scrollX + position.X, position.Y), showCaret ? textColor : bgColor);
            }
            position.X -= scrollX;
            RichTextRenderer.DrawString(drawer, fonts, Value, position + Vector2.One, bgColor);
            RichTextRenderer.DrawString(drawer, fonts, Value, position, textColor);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                TextInputEXT.TextInput -= TextInputEXT_TextInput;
            }
        }

        private class KeyRepeatState
        {
            public double lastRepeatTimeStamp = 0d;
            public double sinceLastRepeat = 0d;
            public double keyHeldStartTimeStamp = 0d;
            public double sinceKeyHeld = 0d;
            public bool isKeyHeld = false;
            public readonly Keys key;
            private readonly Action<double> OnKeyPress;

            public KeyRepeatState(Keys key, Action<double> OnKeyPress)
            {
                this.key = key;
                this.OnKeyPress = OnKeyPress;
            }

            public void ClearKeyState()
            {
                isKeyHeld = false;
                sinceKeyHeld = 0d;
            }
            public void UpdateKeyState(KeyboardState keyboardState, double totalSeconds)
            {
                KeyState keyState = keyboardState[key];
                if (isKeyHeld)
                {
                    sinceKeyHeld = totalSeconds - keyHeldStartTimeStamp;
                    sinceLastRepeat = totalSeconds - lastRepeatTimeStamp;
                }
                if (keyState == KeyState.Down)
                {
                    if (!isKeyHeld)
                    {
                        keyHeldStartTimeStamp = totalSeconds;
                        OnKeyPress(totalSeconds);
                        isKeyHeld = true;
                    }
                    else
                    {
                        if (sinceKeyHeld > keyRepeatDelay)
                        {
                            if (sinceLastRepeat > keyRepeatInterval)
                            {
                                OnKeyPress(totalSeconds);
                                lastRepeatTimeStamp = totalSeconds;
                            }
                        }
                    }
                }
                else
                {
                    isKeyHeld = false;
                    sinceKeyHeld = 0d;
                }
            }
        }
    }
}
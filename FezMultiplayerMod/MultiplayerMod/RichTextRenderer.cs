using FezEngine.Components;
using FezEngine.Services;
using FezEngine.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FezGame.MultiplayerMod
{
    //TODO test this actually works
    //TODO Implement this
    /// <summary>
    /// Uses format codes to stylize, format, and display text.
    /// See ANSI escape codes or ECMA-48
    /// </summary>
    /// <remarks>
    /// <a href="https://www.ecma-international.org/publications-and-standards/standards/ecma-48/">https://www.ecma-international.org/publications-and-standards/standards/ecma-48/</a>
    /// </remarks>
    public class RichTextRenderer
    {
        private struct FontData
        {
            public SpriteFont Font { get; }
            public float Scale { get; }

            public FontData(SpriteFont font, float size)
            {
                Font = font;
                Scale = size;
            }
        }
        private struct TokenizedText
        {
            public string Text { get; }
            public Color Color { get; }
            public FontData FontData { get; }

            public TokenizedText(string text, Color color, FontData fontdata)
            {
                Text = text;
                Color = color;
                FontData = fontdata;
            }
        }
        private static List<FontData> Fonts = new List<FontData>();//TODO populate list
        /*
         * Japanese:
		 *     Big = CMProvider.Global.Load<SpriteFont>("Fonts/Japanese Big");
		 *     BigFactor = 0.34125f;
		 * Korean:
		 *     Big = CMProvider.Global.Load<SpriteFont>("Fonts/Korean Big");
		 *     BigFactor = 0.34125f;
		 * Chinese:
		 *     Big = CMProvider.Global.Load<SpriteFont>("Fonts/Chinese Big");
		 *     BigFactor = 0.34125f;
         * Other:
		 *     Big = CMProvider.Global.Load<SpriteFont>("Fonts/Latin Big");
		 *     BigFactor = 2f;
         */

        //TODO test these
        private string[] testStrings = {
            "\x1B[31mThis is red text\x1B[0m and this is normal.",
            "\x1B[1mBold Text\x1B[0m then \x1B[34mBlue Text\x1B[0m, returning to normal.",
            "\x1B[31;1mRed and bold\x1B[0m but normal here. \x1B[32mGreen text\x1B[0m.",
            "Some text \x1B[32mGreen\x1B[0m, then some text \x1B[35Hello but this won't change.",
            "dark: \x1B[30mBlack\x1B[0m, \x1B[31mRed\x1B[0m, \x1B[32mGreen\x1B[0m, \x1B[33mYellow\x1B[0m, \x1B[34mBlue\x1B[0m, \x1B[35mMagenta\x1B[0m, \x1B[36mCyan\x1B[0m, \x1B[37mWhite\x1B[0m.",
            "light: \x1B[90mBlack\x1B[0m, \x1B[91mRed\x1B[0m, \x1B[92mGreen\x1B[0m, \x1B[93mYellow\x1B[0m, \x1B[94mBlue\x1B[0m, \x1B[95mMagenta\x1B[0m, \x1B[96mCyan\x1B[0m, \x1B[97mWhite\x1B[0m.",
            "dark: \x1B[30mBk\x1B[0m, \x1B[31mRd\x1B[0m, \x1B[32mGn\x1B[0m, \x1B[33mYl\x1B[0m, \x1B[34mBl\x1B[0m, \x1B[35mMg\x1B[0m, \x1B[36mCy\x1B[0m, \x1B[37mWh\x1B[0m.",
            "light: \x1B[90mBk\x1B[0m, \x1B[91mRd\x1B[0m, \x1B[92mGn\x1B[0m, \x1B[93mYl\x1B[0m, \x1B[94mBl\x1B[0m, \x1B[95mMg\x1B[0m, \x1B[96mCy\x1B[0m, \x1B[97mWh\x1B[0m.",

            "\x1B[90m0 \x1B[91m1 \x1B[92m2 \x1B[93m3 \x1B[94m4 \x1B[95m5 \x1B[96m6 \x1B[97m7 \x1B[98m8 \x1B[99m9 \x1B[9ama \x1B[9bmb \x1B[9cmc \x1B[9dmd \x1B[9eme \x1B[9fmf",
        };
        private static List<TokenizedText> TokenizeChars(string text, FontData defaultFontData, Color defaultColor)
        {
            List<TokenizedText> tokens = new List<TokenizedText>();
            FontData lastFont = defaultFontData;
            FontData currentFont = defaultFontData;
            Color currentColor = defaultColor;
            string currentToken = "";
            for (int i = 0; i < text.Length; ++i)//changed from foreach so we can look ahead from the current position 
            {
                char c = text[i];

                //check for special characters to change currentColor and whatever other presentation options we want to include; see "Select Graphic Rendition"
                if (c == '\x1B')//ANSI escape codes
                {
                    // Check if the next character is '['
                    if (i + 1 < text.Length && text[i + 1] == '[')
                    {
                        // Control Sequence Identifier

                        // Start collecting the escape sequence
                        int start = i;
                        int i_temp = i;
                        i_temp += 2; // Skip over the escape and the '['

                        // Collect until we find a letter or the end of the string
                        while (i_temp < text.Length && (c = text[i_temp]) >= '\x40' && c <= '\x7F')
                        {
                            i_temp++;
                        }

                        if (i_temp < text.Length)
                        {
                            // Note: some of the escape codes have a space
                            if(text[i_temp - 1] == '\x20')
                            {
                                switch (text[i_temp])
                                {
                                /*
                                 * See Table 4 in ECMA-48 ( https://www.ecma-international.org/publications-and-standards/standards/ecma-48/ )
                                 * Note in ECMA-48 the "Representation" text is in a format where 02/00 is character \x20, 04/11 is \x4B, 04/15 is \x4F, etc.
                                **/
                                //TODO populate switch?
                                default:
                                    break;
                                }
                            }
                            switch (text[i_temp])
                            {
                            /*
                             * See Table 3 in ECMA-48 ( https://www.ecma-international.org/publications-and-standards/standards/ecma-48/ )
                             * Note in ECMA-48 the "Representation" text is in a format where 02/00 is character \x20, 04/11 is \x4B, 04/15 is \x4F, etc.
                            **/
                            case 'm':
                                // Capture the full escape sequence, including the ESC and '['
                                string escapeSequence = text.Substring(start, i_temp - start + 1);

                                // Parse the escape sequence to change font/color attributes
                                ParseSGREscape(escapeSequence, in defaultFontData, in defaultColor, ref currentFont, ref currentColor);
                                break;
                            //TODO add more cases for other stuff?
                            case '\\'://SET ADDITIONAL CHARACTER SEPARATION
                            default:
                                break;
                            }
                            // We already processed up to the 'm', so we can skip to the next character
                            i = i_temp;
                            continue;
                        }
                    }
                }
                currentFont = GetFirstSupportedFont(defaultFontData, c);
                if (currentFont.Equals(lastFont))
                {
                    currentToken += c;
                }
                else
                {
                    tokens.Add(new TokenizedText(currentToken, currentColor, currentFont));
                    currentToken = "";
                    lastFont = currentFont;
                }
            }

            // Flush the remaining token if any
            if (!string.IsNullOrEmpty(currentToken))
            {
                tokens.Add(new TokenizedText(currentToken, currentColor, currentFont));
            }

            return tokens;
        }
        public Vector2 MeasureString(FontManager fontManager, string text)
        {
            return MeasureString(fontManager.Big, fontManager.BigFactor, text);
        }
        public Vector2 MeasureString(SpriteFont defaultFont, float defaultFontScale, string text)
        {
            FontData defaultFontData = new FontData(defaultFont, defaultFontScale);
            Vector2 size = Vector2.Zero;
            string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            string line;
            for (int i = 0; i < lines.Length; ++i)
            {
                line = lines[i];
                Vector2 linesize = Vector2.Zero;
                TokenizeChars(line, defaultFontData, Color.White).ForEach((TokenizedText token) =>
                {
                    FontData fontData = token.FontData;
                    Vector2 tokensize = fontData.Font.MeasureString(token.Text.ToString()) * fontData.Scale;
                    linesize.X += tokensize.X + fontData.Font.Spacing;
                    linesize.Y = Math.Max(linesize.Y, tokensize.Y);
                });
                size.X = Math.Max(linesize.X, size.X);
                size.Y += linesize.Y;
                //check if there's more lines
                if (i + 1 < lines.Length)
                {
                    size.Y += defaultFontData.Font.LineSpacing * defaultFontData.Scale;
                }
            }
            return size;
        }
        public void DrawString(SpriteBatch batch, FontManager fontManager, string text, Vector2 position, Color defaultColor, float scale)
        {
            DrawString(batch, fontManager.Big, fontManager.BigFactor, text, position, defaultColor, scale, 0);
        }
        public void DrawString(SpriteBatch batch, SpriteFont defaultFont, float defaultFontScale, string text, Vector2 position, Color defaultColor, float scale)
        {
            DrawString(batch, defaultFont, defaultFontScale, text, position, defaultColor, scale, 0);
        }
        public void DrawString(SpriteBatch batch, SpriteFont defaultFont, float defaultFontScale, string text, Vector2 position, Color defaultColor, float scale, float layerDepth)
        {
            /*
             * Note: currently, I think tokens are drawn with vertical-align: top
             * Because of this, it is very important that the font scale and values in List<FontData> are correct, and all fonts appear the same scale
             */
            Vector2 currentPositionOffset = Vector2.Zero;
            FontData defaultFontData = new FontData(defaultFont, defaultFontScale);
            string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            string line;
            for (int i = 0; i < lines.Length; ++i)
            {
                line = lines[i];
                Vector2 linesize = Vector2.Zero;
                TokenizeChars(line, defaultFontData, defaultColor).ForEach((TokenizedText token) =>
                {
                    FontData fontData = token.FontData;
                    Vector2 tokensize = fontData.Font.MeasureString(token.Text.ToString()) * fontData.Scale;
                    batch.DrawString(fontData.Font, token.Text, position + currentPositionOffset, token.Color, 0f, Vector2.Zero, fontData.Scale * scale, SpriteEffects.None, layerDepth);
                    linesize.X += tokensize.X + fontData.Font.Spacing;
                    linesize.Y = Math.Max(linesize.Y, tokensize.Y);
                    currentPositionOffset.X += linesize.X;
                });
                currentPositionOffset.Y += linesize.Y;
                //check if there's more lines
                if (i + 1 < lines.Length)
                {
                    currentPositionOffset.X = 0;
                    currentPositionOffset.Y += defaultFontData.Font.LineSpacing * defaultFontData.Scale;
                }
            }
        }

        private static FontData GetFirstSupportedFont(FontData defaultFontData, char ch)
        {
            if (FontSupportsCharacter(defaultFontData.Font, ch))
            {
                return defaultFontData;
            }
            else
            {
                foreach (FontData fontData in Fonts)
                {
                    if (FontSupportsCharacter(fontData.Font, ch))
                    {
                        return fontData;
                    }
                }
                return defaultFontData;
            }
        }
        private static bool FontSupportsCharacter(SpriteFont font, char ch)
        {
            return font.Characters.Contains(ch);
        }
        private static void ParseSGREscape(string escapeSequence, in FontData defaultFontData, in Color defaultColor, ref FontData currentFont, ref Color currentColor)
        {
            string parameters = escapeSequence.Substring(2, escapeSequence.Length - 3); // Exclude the ESC and '[' and 'm'
            var codes = parameters.Split(';');

            for (int i = 0; i < codes.Length; i++)
            {
                if (int.TryParse(codes[i], out int codeValue))
                {
                    switch (codeValue)
                    {
                    case 0: // Reset
                        currentFont = defaultFontData; // Reset to default font
                        currentColor = defaultColor; // Reset to default color
                        break;
                    case 1: // Bold
                        break;
                    case 2: // Faint
                        break;
                    case 3: // Italic
                        break;
                    case 4: // Underline
                        break;
                    case 5: // Slow blink
                        break;
                    case 6: // Rapid blink
                        break;
                    case 7: // Negative image
                        break;
                    case 8: // Obfuscate / concealed characters
                        break;
                    case 9: // Strikethrough
                        break;
                    //cases 10 to 20 are fonts
                    case 21: // Double-underline
                        break;
                    case 22: // Neither bold nor faint
                        break;
                    case 23: // Not italic
                        break;
                    case 24: // Not underlined
                        break;
                    case 25: // Not blinking
                        break;
                    case 26: // Proportional spacing (Not supporting this)
                        break;
                    case 27: // Not negative image
                        break;
                    case 28: // Revealed characters / Not obfuscated
                        break;
                    case 29: // Not strikethrough
                        break;

                    // Using Windows XP Console colors
                    // Note these are the same as the first 16 colors from TryGetColorFrom8BitIndex
                    // Standard colors
                    case 30: currentColor = Color.Black; break;   // Dark Black
                    case 31: currentColor = Color.Maroon; break;  // Dark Red
                    case 32: currentColor = Color.Green; break;   // Dark Green
                    case 33: currentColor = Color.Olive; break;   // Dark Yellow
                    case 34: currentColor = Color.Navy; break;    // Dark Blue
                    case 35: currentColor = Color.Purple; break;  // Dark Magenta
                    case 36: currentColor = Color.Teal; break;    // Dark Cyan
                    case 37: currentColor = Color.Silver; break;  // Dark White

                    case 38: // Start of 8-bit or true color
                        if (i + 1 < codes.Length && int.TryParse(codes[i + 1], out int colorType))
                        {
                            if (colorType == 5) // 8-bit color index
                            {
                                if (i + 2 < codes.Length && int.TryParse(codes[i + 2], out int colorIndex))
                                {
                                    if (TryGetColorFrom8BitIndex(colorIndex, out Color newColor))
                                    {
                                        currentColor = newColor;
                                    };
                                    i += 2; // Skip the next two parameters
                                }
                            }
                            else if (colorType == 2) // 24-bit true color
                            {
                                if (i + 4 < codes.Length &&
                                    int.TryParse(codes[i + 1], out int r) &&
                                    int.TryParse(codes[i + 2], out int g) &&
                                    int.TryParse(codes[i + 3], out int b))
                                {
                                    currentColor = new Color(MathHelper.Clamp(r, 0, 255), MathHelper.Clamp(g, 0, 255), MathHelper.Clamp(b, 0, 255));
                                    i += 4; // Skip the next four parameters
                                }
                            }
                        }
                        break;
                    case 39: currentColor = defaultColor; break;

                    // cases 40 to 49 are for backgrounds, in the same order as 30 to 39
                    case 50: // Cancel proportional spacing (Not supporting this)
                        break;
                    case 51: // Framed
                        break;
                    case 52: // Encircled
                        break;
                    case 53: // Overlined
                        break;
                    case 54: // Not framed, not encircled
                        break;
                    case 55: // Not overlined
                        break;
                    // cases 56 to 59 are not defined
                    case 60: // ideogram underline or right side line
                        break;
                    case 61: // ideogram double underline or double line on the right side
                        break;
                    case 62: // ideogram overline or left side line
                        break;
                    case 63: // ideogram double overline or double line on the left side
                        break;
                    case 64: // ideogram stress marking
                        break;
                    case 65: // cancels the effect of the rendition aspects established by parameter values 60 to 64
                        break;
                    //nothing defined for 
                    // Bright colors
                    case 90: currentColor = Color.Gray; break;    // Bright Black (Gray)
                    case 91: currentColor = Color.Red; break;     // Bright Red
                    case 92: currentColor = Color.Lime; break;    // Bright Green
                    case 93: currentColor = Color.Yellow; break;  // Bright Yellow
                    case 94: currentColor = Color.Blue; break;    // Bright Blue
                    case 95: currentColor = Color.Magenta; break; // Bright Magenta
                    case 96: currentColor = Color.Cyan; break;    // Bright Cyan
                    case 97: currentColor = Color.White; break;   // Bright White
                    // cases 100 to 107 are for backgrounds, in the same order as 90 to 97

                    //nothing defined after 107

                    default:
                        break;
                    }
                }
            }
        }
        /// <summary>
        /// Tries to get a Color corresponding to the given index.
        /// </summary>
        /// <param name="index">The index to retrieve the color for, expected to be between 0 and 255.</param>
        /// <param name="color">The Color corresponding to the index, or Color.Transparent if the index is invalid.</param>
        /// <returns>True if the color was successfully retrieved; otherwise, false.</returns>
        private static bool TryGetColorFrom8BitIndex(int index, out Color color)
        {
            // Check for valid index range
            if (index < 0 || index > 255)
            {
                color = Color.Transparent; // or any default color
                return false; // Indicate failure
            }
            switch (index)
            {
            //The first 16 colors are the same as the "Standard colors" and "Bright colors" 
            case 0: color = Color.Black; return true;
            case 1: color = Color.Maroon; return true;
            case 2: color = Color.Green; return true;
            case 3: color = Color.Olive; return true;
            case 4: color = Color.Navy; return true;
            case 5: color = Color.Purple; return true;
            case 6: color = Color.Teal; return true;
            case 7: color = Color.Silver; return true;
            case 8: color = Color.Gray; return true;
            case 9: color = Color.Red; return true;
            case 10: color = Color.Lime; return true;
            case 11: color = Color.Yellow; return true;
            case 12: color = Color.Blue; return true;
            case 13: color = Color.Magenta; return true;
            case 14: color = Color.Cyan; return true;
            case 15: color = Color.White; return true;
            default:
                {
                    if (index >= 16 && index < 232)
                    {
                        // 6x6x6 Color Cube
                        int rgbIndex = index - 16;
                        int r = (rgbIndex / 36) * 51; // Scale 0 to 255 for Red
                        int g = ((rgbIndex / 6) % 6) * 51; // Scale 0 to 255 for Green
                        int b = (rgbIndex % 6) * 51; // Scale 0 to 255 for Blue
                        color = new Color(r, g, b);
                        return true;
                    }
                    else // 232-255 are grayscale
                    {
                        int grayValue = ((index - 232) * 10) + 8; // Scale to 0-255
                        color = new Color(grayValue, grayValue, grayValue);
                        return true;
                    }
                }
            }
        }
    }
}

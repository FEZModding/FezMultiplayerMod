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
    //TODO Implement this
    /// <summary>
    /// Uses format codes to stylize, format, and display text.
    /// See ANSI escape codes or ECMA-48
    /// </summary>
    /// <remarks>
    /// <a href="https://www.ecma-international.org/publications-and-standards/standards/ecma-48/">https://www.ecma-international.org/publications-and-standards/standards/ecma-48/</a>
    /// </remarks>
    public sealed class RichTextRenderer
    {
        private struct FontData
        {
            public SpriteFont Font { get; }
            /// <summary>
            /// The amount to scale this font by so it looks the same size as the other fonts
            /// </summary>
            public float Scale { get; }

            public FontData(SpriteFont font, float size)
            {
                Font = font;
                Scale = size;
            }
        }
        /// <summary>
        /// Stuff like Underline, Strikethrough, Overline
        /// </summary>
        [Flags]
        private enum TextDecoration
        {
            None = 0,
            Underline = 0b1,
            Strikethrough = 0b10,
            Overline = 0b100,
        }
        private sealed class TokenStyle
        {
            /// <summary>
            /// The color to use for this token
            /// </summary>
            public Color Color { get; set; }
            /// <summary>
            /// The font to use for this token
            /// </summary>
            public FontData FontData { get; set; }
            /// <summary>
            /// TODO implement text decorations <br />
            /// Stuff like underline, overline, strikethrough <br />
            /// See https://developer.mozilla.org/en-US/docs/Web/CSS/text-decoration
            /// </summary>
            public TextDecoration Decoration { get; set; }
            /// <summary>
            /// TODO implement font weight (maybe draw this many times, shifting over by 1 px every time) <br />
            /// For potential reference, see https://developer.mozilla.org/en-US/docs/Web/CSS/font-weight
            /// </summary>
            public ushort FontWeight { get; set; }
            /// <summary>
            /// TODO implement slanted text <br />
            /// TODO determine what angle unit to use for slanted text(radians or degrees) <br />
            /// Text slant, in angle units. See https://developer.mozilla.org/en-US/docs/Web/CSS/font-style
            /// </summary>
            public float ObliqueAngle { get; set; }

            public TokenStyle(Color color, FontData fontdata, TextDecoration decoration, ushort weight, float slantAngle)
            {
                Color = color;
                FontData = fontdata;
                Decoration = decoration;
                FontWeight = weight;
                ObliqueAngle = slantAngle;
            }

            public TokenStyle Clone()
            {
                return (TokenStyle)MemberwiseClone();
            }
        }
        private struct TokenizedText
        {
            /// <summary>
            /// The text in this token
            /// </summary>
            public string Text { get; }
            public TokenStyle Style { get; }

            public TokenizedText(string text, TokenStyle tokenStyle)
            {
                Text = text;
                Style = tokenStyle.Clone();
            }
        }
        /// <summary>
        /// Simple list for keeping track of how many fonts are loaded, and for enumerating through the fonts.
        /// </summary>
        private static readonly List<FontData> Fonts = new List<FontData>();
        /// <summary>
        /// For looking up fonts by name
        /// </summary>
        private static readonly Dictionary<string, FontData> FontsByName = new Dictionary<string, FontData>();

        /// <summary>
        /// Indicated if the fonts are loaded into Fonts
        /// </summary>
        private static bool FontsNeedLoading = true;
        /// <summary>
        /// Loads the fonts using the specified ContentManager, and returns the total number of fonts loaded.
        /// </summary>
        /// <param name="CM"></param>
        /// <returns>The number of fonts loaded</returns>
        /// <remarks>
        /// Note subsequent calls to this method will not trigger reloading of the fonts, but instead just returns the number of fonts already loaded.
        /// </remarks>
        private static int LoadFonts(Microsoft.Xna.Framework.Content.ContentManager CM)
        {
            if (FontsNeedLoading)
            {
                FontsNeedLoading = false;
                new List<Tuple<string, float>>{
                    //Don't use small fonts because they don't look that great
                    new Tuple<string, float>("Chinese Big", 0.34125f ),
                    new Tuple<string, float>("Japanese Big", 0.34125f ),
                    new Tuple<string, float>("Korean Big", 0.34125f ),
                    new Tuple<string, float>("Latin Big", 2f),
                }.ForEach((langdat) =>
                {
                    string lang = langdat.Item1;
                    float size = langdat.Item2;
                    SpriteFont font = CM.Load<SpriteFont>($"Fonts/{lang}");
                    FontData fontdata = new FontData(font, size);
                    Fonts.Add(fontdata);
                    FontsByName.Add(lang, fontdata);
                });
            }
            return Fonts.Count;
        }
        /// <summary>
        /// Loads the fonts using the Global ContentManager, and returns the total number of fonts loaded.
        /// </summary>
        /// <param name="CMProvider"></param>
        /// <returns>The number of fonts loaded</returns>
        /// <remarks>
        /// Note subsequent calls to this method will not trigger reloading of the fonts, but instead just returns the number of fonts already loaded.
        /// </remarks>
        public static int LoadFonts(IContentManagerProvider CMProvider)
        {
            return LoadFonts(CMProvider.Global);//Should always use Global
        }
        public static Vector2 MeasureString(IFontManager fontManager, string text)
        {
            return MeasureString(fontManager.Big, fontManager.BigFactor, text);
        }
        public static Vector2 MeasureString(SpriteFont defaultFont, float defaultFontScale, string text)
        {
            return IterateLines(defaultFont, defaultFontScale, text, Color.White, (token, positionOffset) => { });
        }
        public static void DrawString(SpriteBatch batch, IFontManager fontManager, string text, Vector2 position, Color defaultColor, float scale)
        {
            DrawString(batch, fontManager.Big, fontManager.BigFactor, text, position, defaultColor, scale, 0);
        }
        public static void DrawString(SpriteBatch batch, SpriteFont defaultFont, float defaultFontScale, string text, Vector2 position, Color defaultColor, float scale)
        {
            DrawString(batch, defaultFont, defaultFontScale, text, position, defaultColor, scale, 0);
        }
        public static void DrawString(SpriteBatch batch, SpriteFont defaultFont, float defaultFontScale, string text, Vector2 position, Color defaultColor, float scale, float layerDepth)
        {
            /*
             * Note: currently, I think tokens are drawn with vertical-align: top
             * Because of this, it is very important that the font scale and values in List<FontData> are correct, and all fonts appear the same scale
             */

            _ = IterateLines(defaultFont, defaultFontScale, text, Color.White, (token, positionOffset) =>
            {
                FontData fontData = token.Style.FontData;
                batch.DrawString(fontData.Font, token.Text, position + positionOffset, token.Style.Color, 0f, Vector2.Zero, fontData.Scale * scale, SpriteEffects.None, layerDepth);
            });
        }

        //TODO add more tests
        public static readonly string[] testStrings = {
            "\x1B[31mThis is red text\x1B[0m and this is normal.",
            "\x1B[1mBold Text\x1B[0m then \x1B[34mBlue Text\x1B[0m, returning to normal.",
            "\x1B[31;1mRed and bold\x1B[0m but normal here. \x1B[32mGreen text\x1B[0m.",
            "Some text \x1B[32mGreen\x1B[0m, then some text \x1B[35Hello but this won't change.",
            "Multifont test: [Y\u3042\u3044\xE9\u56DB\u5B89\uAFB8\uD658\uFF1FZ\u4E0AW]",
            "Multifont test 2: [\u3046e\u56DB\uAFB8\u3044\u5B89\uD658]",
            "Colored multifont test: [Y\x1B[31m\u3042\u3044\xE9\x1B[93m\u56DB\u5B89\x1B[96m\uAFB8\uD658\x1B[90m\uFF1FZ\x1B[0m\u4E0AW]",
            "dark: \x1B[30mBlack\x1B[0m, \x1B[31mRed\x1B[0m, \x1B[32mGreen\x1B[0m, \x1B[33mYellow\x1B[0m, \x1B[34mBlue\x1B[0m, \x1B[35mMagenta\x1B[0m, \x1B[36mCyan\x1B[0m, \x1B[37mWhite\x1B[0m.",
            "light: \x1B[90mBlack\x1B[0m, \x1B[91mRed\x1B[0m, \x1B[92mGreen\x1B[0m, \x1B[93mYellow\x1B[0m, \x1B[94mBlue\x1B[0m, \x1B[95mMagenta\x1B[0m, \x1B[96mCyan\x1B[0m, \x1B[97mWhite\x1B[0m.",
            "8bit colors: \x1B[38;5;237mGrayish\x1B[38;5;10mLime\x1B[38;5;213mPink\x1B[38;5;208mOrange.",
            "true bit colors: \x1B[38;2;255;0;255mMagenta.",
        };
        private static Vector2 IterateLines(SpriteFont defaultFont, float defaultFontScale, string text, Color defaultColor, Action<TokenizedText, Vector2> onToken)
        {
            /*
             * Note: currently, I think tokens are drawn with vertical-align: top
             * Because of this, it is very important that the font scale and values in List<FontData> are correct, and all fonts appear the same scale
             */
            /// The total size required for the text
            Vector2 size = Vector2.Zero;
            /// The starting position of the current token
            Vector2 currentPositionOffset = Vector2.Zero;
            FontData defaultFontData = new FontData(defaultFont, defaultFontScale);
            string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            string line;
            TokenStyle currentStyle = new TokenStyle(defaultColor, defaultFontData, TextDecoration.None, 1, 0f);

            for (int i = 0; i < lines.Length; ++i)
            {
                line = lines[i];
                Vector2 linesize = Vector2.Zero;
                TokenizeChars(line, defaultFontData, defaultColor,
                        currentStyle
                        ).ForEach((TokenizedText token) =>
                        {
                            FontData fontData = token.Style.FontData;
                            Vector2 tokensize;
                            try
                            {
                                tokensize = fontData.Font.MeasureString(token.Text.ToString()) * fontData.Scale;
                            }
                            catch (Exception e)
                            {
                                tokensize = fontData.Font.MeasureString("" + (fontData.Font.DefaultCharacter ?? ' ')) * fontData.Scale;
                                System.Diagnostics.Debugger.Launch();
                            }
                            onToken(token, currentPositionOffset);
                            linesize.Y = Math.Max(linesize.Y, tokensize.Y);
                            float tokenSizeXWithSpacing = tokensize.X + fontData.Font.Spacing;
                            linesize.X += tokenSizeXWithSpacing;
                            currentPositionOffset.X += tokenSizeXWithSpacing;
                        });
                size.X = Math.Max(linesize.X, size.X);
                size.Y += linesize.Y;
                currentPositionOffset.Y += linesize.Y;
                //check if there's more lines
                if (i + 1 < lines.Length)
                {
                    float scaledLineSpacing = defaultFontData.Font.LineSpacing * defaultFontData.Scale;
                    currentPositionOffset.X = 0;
                    size.Y += scaledLineSpacing;
                    currentPositionOffset.Y += scaledLineSpacing;
                }
            }
            return size;
        }
        private static List<TokenizedText> TokenizeChars(string text, FontData defaultFontData, Color defaultColor,
                TokenStyle currentStyle)
        {
            List<TokenizedText> tokens = new List<TokenizedText>();
            string currentToken = "";
            for (int i = 0; i < text.Length; ++i)//changed from foreach so we can look ahead from the current position 
            {
                char c = text[i];

                //check for special characters to change currentColor and whatever other presentation options we want to include; see "Select Graphic Rendition"
                if (c == '\x1B')//ANSI escape codes
                {
                    //flush current token
                    tokens.Add(new TokenizedText(currentToken, currentStyle));
                    currentToken = "";

                    /*
                     * Note: 
                     * This code only handles Presentation control functions that I (Jenna Sloan) deemed possible to implement in the XNA framework.
                     * As such, it does not support:
                     *     Delimiters,
                     *     Introducers,
                     *     Shift functions,
                     *     Format effectors,
                     *     Editor functions,
                     *     Cursor control functions,
                     *     Device control functions,
                     *     Information separators,
                     *     Area definition,
                     *     any codes that require areas,
                     *     Mode setting,
                     *     Transmission control functions,
                     *     Miscellaneous control functions
                     * with the exception of PRIVATE USE ONE and PRIVATE USE TWO, which may be used for something at a later date.
                     * 
                     * Anyways, I manually went through all the escape codes and picked out ones that I though would fit with XNA's DrawString.
                     */

                    if (i + 1 < text.Length)
                    {
                        switch (text[i + 1])
                        {
                        case '[':
                            // CSI (Control Sequence Identifier) codes

                            // Start collecting the escape sequence
                            int start = i;
                            int i_temp = i;
                            i_temp += 2; // Skip over the escape and the '['

                            // Collect until we find a letter or the end of the string
                            while (i_temp < text.Length && !((c = text[i_temp]) >= '\x40' && c <= '\x7F'))
                            {
                                i_temp++;
                            }

                            if (i_temp < text.Length)
                            {
                                // Note: some of the escape codes have a space as the penultimate character
                                if (text[i_temp - 1] == '\x20')
                                {
                                    switch (text[i_temp])
                                    {
                                    /*
                                     * See Table 4 in ECMA-48 ( https://www.ecma-international.org/publications-and-standards/standards/ecma-48/ )
                                     * Note in ECMA-48 the "Representation" text is in a format where 02/00 is character \x20, 04/11 is \x4B, 04/15 is \x4F, etc.
                                    **/
                                    //TODO support all these empty switch cases?
                                    case '\x42'://GSM - GRAPHIC SIZE MODIFICATION
                                        break;
                                    case '\x43'://GSS - GRAPHIC SIZE SELECTION
                                        break;
                                    case '\x44'://FNT - FONT SELECTION
                                        break;
                                    case '\x45'://TSS - THIN SPACE SPECIFICATION
                                        break;
                                    case '\x46'://JFY - JUSTIFY
                                        break;
                                    case '\x47'://SPI - SPACING INCREMENT
                                        break;
                                    case '\x48'://QUAD - QUAD
                                        break;
                                    case '\x49'://SSU - SELECT SIZE UNIT
                                        break;
                                    case '\x4A'://PFS - PAGE FORMAT SELECTION
                                        break;
                                    case '\x4B'://SHS - SELECT CHARACTER SPACING
                                        break;
                                    case '\x4C'://SVS - SELECT LINE SPACING
                                        break;

                                    case '\x53'://SPD - SELECT PRESENTATION DIRECTIONS
                                        break;

                                    case '\x5A'://PEC - PRESENTATION EXPAND OR CONTRACT
                                        break;
                                    case '\x5B'://SSW - SET SPACE WIDTH
                                        break;
                                    case '\\'://SACS - SET ADDITIONAL CHARACTER SEPARATION
                                        break;
                                    case '\x5D'://SAPV - SELECT ALTERNATIVE PRESENTATION VARIANTS
                                        break;
                                    case '\x5E'://STAB - SELECTIVE TABULATION
                                        break;
                                    case '\x5F'://GCC - GRAPHIC CHARACTER COMBINATION
                                        break;

                                    case '\x65'://SCO - SELECT CHARACTER ORIENTATION
                                        break;
                                    case '\x66'://SRCS - SET REDUCED CHARACTER SEPARATION
                                        break;
                                    case '\x67'://SCS - SET CHARACTER SPACING
                                        break;
                                    case '\x68'://SLS - SET LINE SPACING
                                        break;

                                    case '\x6B'://SCP - SELECT CHARACTER PATH
                                        break;

                                    default:
                                        //Not supported escape code
                                        break;
                                    }
                                }
                                else
                                {
                                    switch (text[i_temp])
                                    {
                                    /*
                                     * See Table 3 in ECMA-48 ( https://www.ecma-international.org/publications-and-standards/standards/ecma-48/ )
                                     * Note in ECMA-48 the "Representation" text is in a format where 02/00 is character \x20, 04/11 is \x4B, 04/15 is \x4F, etc.
                                    **/
                                    //TODO add support for reversed text?
                                    case '\x5B'://SRS - START REVERSED STRING
                                        break;
                                    case '\x5D'://SDS - START DIRECTED STRING
                                        break;
                                    case 'm':
                                        // Capture the full escape sequence, including the ESC and '['
                                        string escapeSequence = text.Substring(start, i_temp - start + 1);

                                        // Parse the escape sequence to change style data
                                        ParseSGREscape(escapeSequence, in defaultFontData, in defaultColor, currentStyle);
                                        break;
                                    default:
                                        //Not supported escape code
                                        break;
                                    }
                                }
                                // skip to the next character
                                i = i_temp;
                                continue;
                            }
                            break;
                        //ESC codes
                        case 'Q'://PRIVATE USE ONE
                        case 'R'://PRIVATE USE TWO
                        default:
                            //Not supported escape code
                            break;
                        }
                    }
                }
                FontData nextFont = GetFirstSupportedFont(defaultFontData, c);
                if (nextFont.Equals(currentStyle.FontData))
                {
                    currentToken += c;
                }
                else
                {
                    tokens.Add(new TokenizedText(currentToken, currentStyle));
                    currentToken = "" + c;
                    currentStyle.FontData = nextFont;
                }
                currentStyle.FontData = nextFont;
            }

            // Flush the remaining token if any
            if (!string.IsNullOrEmpty(currentToken))
            {
                tokens.Add(new TokenizedText(currentToken, currentStyle));
            }

            return tokens;
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
        private static void ParseSGREscape(string escapeSequence, in FontData defaultFontData, in Color defaultColor,
                TokenStyle currentStyle)
        {
            string parameters = escapeSequence.Substring(2, escapeSequence.Length - 3); // Exclude the ESC and '[' and 'm'
            var codes = parameters.Split(';');

            for (int i = 0; i < codes.Length; i++)
            {
                if (int.TryParse(codes[i], out int codeValue))
                {
                    switch (codeValue)
                    {
                    //TODO add support for more of these
                    case 0: // Reset
                        currentStyle.FontData = defaultFontData; // Reset to default font
                        currentStyle.Color = defaultColor; // Reset to default color
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
                    case 30: currentStyle.Color = Color.Black; break;   // Dark Black
                    case 31: currentStyle.Color = Color.Maroon; break;  // Dark Red
                    case 32: currentStyle.Color = Color.Green; break;   // Dark Green
                    case 33: currentStyle.Color = Color.Olive; break;   // Dark Yellow
                    case 34: currentStyle.Color = Color.Navy; break;    // Dark Blue
                    case 35: currentStyle.Color = Color.Purple; break;  // Dark Magenta
                    case 36: currentStyle.Color = Color.Teal; break;    // Dark Cyan
                    case 37: currentStyle.Color = Color.Silver; break;  // Dark White

                    case 38: // Start of 8-bit or true color
                        if (i + 1 < codes.Length && int.TryParse(codes[i + 1], out int colorType))
                        {
                            if (colorType == 5) // 8-bit color index
                            {
                                if (i + 2 < codes.Length && int.TryParse(codes[i + 2], out int colorIndex))
                                {
                                    if (TryGetColorFrom8BitIndex(colorIndex, out Color newColor))
                                    {
                                        currentStyle.Color = newColor;
                                    };
                                    i += 2; // Skip the next two parameters
                                }
                            }
                            else if (colorType == 2) // 24-bit true color
                            {
                                if (i + 4 < codes.Length &&
                                    int.TryParse(codes[i + 2], out int r) &&
                                    int.TryParse(codes[i + 3], out int g) &&
                                    int.TryParse(codes[i + 4], out int b))
                                {
                                    currentStyle.Color = new Color(MathHelper.Clamp(r, 0, 255) / 255f, MathHelper.Clamp(g, 0, 255) / 255f, MathHelper.Clamp(b, 0, 255) / 255f);
                                    i += 4; // Skip the next four parameters
                                }
                            }
                        }
                        break;
                    case 39: currentStyle.Color = defaultColor; break;

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
                    case 90: currentStyle.Color = Color.Gray; break;    // Bright Black (Gray)
                    case 91: currentStyle.Color = Color.Red; break;     // Bright Red
                    case 92: currentStyle.Color = Color.Lime; break;    // Bright Green
                    case 93: currentStyle.Color = Color.Yellow; break;  // Bright Yellow
                    case 94: currentStyle.Color = Color.Blue; break;    // Bright Blue
                    case 95: currentStyle.Color = Color.Magenta; break; // Bright Magenta
                    case 96: currentStyle.Color = Color.Cyan; break;    // Bright Cyan
                    case 97: currentStyle.Color = Color.White; break;   // Bright White
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

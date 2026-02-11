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
    public static class TypeExtensions
    {
        private static Texture2D _texture;
        public static void DrawRect(this SpriteBatch batch, Rectangle rect, Color color)
        {
            if (_texture == null)
            {
                _texture = new Texture2D(batch.GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
                _texture.SetData(new[] { Color.White });
            }
            batch.Draw(_texture, new Vector2(rect.X, rect.Y), null, color, 0f, Vector2.Zero, new Vector2(rect.Width, rect.Height), SpriteEffects.None, 0f);
        }
        public static void DrawRect(this SpriteBatch batch, Vector2 start, float width, float height, Color color)
        {
            if (_texture == null)
            {
                _texture = new Texture2D(batch.GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
                _texture.SetData(new[] { Color.White });
            }
            batch.Draw(_texture, start, null, color, 0f, Vector2.Zero, new Vector2(width, height), SpriteEffects.None, 0f);
        }
        public static void DrawRectWireframe(this SpriteBatch batch, Vector2 boxOrigin, Vector2 boxSize, float lineThickness, Color color)
        {
            batch.DrawRectWireframe(boxOrigin, boxSize.X, boxSize.Y, lineThickness, color);
        }
        public static void DrawRectWireframe(this SpriteBatch batch, Vector2 boxOrigin, float boxWidth, float boxHeight, float lineThickness, Color color)
        {
            //top
            batch.DrawRect(boxOrigin, boxWidth, lineThickness, color);
            //left
            batch.DrawRect(boxOrigin, lineThickness, boxHeight, color);
            //bottom
            batch.DrawRect(boxOrigin + new Vector2(0, boxHeight - lineThickness), boxWidth, lineThickness, color);
            //right
            batch.DrawRect(boxOrigin + new Vector2(boxWidth - lineThickness, 0), lineThickness, boxHeight, color);
        }

        public static bool HasValue(this Type type, char value)
        {
            return type.GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public).Any(field =>
            {
                return field.FieldType.IsAssignableFrom(typeof(char)) && value == (char)field.GetValue(null);
            });
        }
        public static bool HasValue(this Type type, int value)
        {
            return type.GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public).Any(field =>
            {
                return field.FieldType.IsAssignableFrom(typeof(int)) && value == (int)field.GetValue(null);
            });
        }
        public static void DrawTextRichShadow(this SpriteBatch batch, IFontManager fontManager, string text, Vector2 position, float? scale, Color? color = null, Color? shadow = null)
        {
            Vector2 scaleV;
            if (scale == null)
            {
                scaleV = Vector2.One;
            }
            else
            {
                scaleV = new Vector2(scale.Value);
            }

            RichTextRenderer.DrawString(batch, fontManager, text, position + Vector2.One, shadow ?? Color.Black, Color.Transparent, scaleV);
            RichTextRenderer.DrawString(batch, fontManager, text, position, color ?? Color.White, Color.Transparent, scaleV);
        }
        public static void DrawTextRichShadow(this SpriteBatch batch, IFontManager fontManager, string text, Vector2 position, Vector2? scale = null, Color? color = null, Color? shadow = null)
        {
            if (scale == null)
            {
                scale = Vector2.One;
            }

            RichTextRenderer.DrawString(batch, fontManager, text, position + Vector2.One, shadow ?? Color.Black, Color.Transparent, scale.Value);
            RichTextRenderer.DrawString(batch, fontManager, text, position, color ?? Color.White, Color.Transparent, scale.Value);
        }
        private static readonly System.Text.RegularExpressions.Regex escapeCodeRegex = new System.Text.RegularExpressions.Regex(@"(\x1B\[|\x9B)([0-9\x3A;]*)(\x20?[\x40-\x7F])", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        public static string StripAnsiEscapeSequences(this string str)
        {
            return escapeCodeRegex.Replace(str, "");
        }
        public static int LengthWithoutAnsiEscapeSequences(this string str)
        {
            return str.StripAnsiEscapeSequences().Length;
        }
        /// <summary>
        /// Pad right, accounting for Ansi Escape Sequences
        /// </summary>
        /// <param name="str"></param>
        /// <param name="totalWidth">total width ignoring escape sequences</param>
        /// <returns></returns>
        public static string PadRightAnsi(this string str, int totalWidth)
        {
            int dif = str.Length - str.LengthWithoutAnsiEscapeSequences();
            return str.PadRight(totalWidth + dif);
        }
    }
    /// <summary>
    /// Uses format codes to stylize, format, and display text.
    /// See ANSI escape codes or ECMA-48
    /// </summary>
    /// <remarks>
    /// See ANSI escape codes or <a href="https://www.ecma-international.org/publications-and-standards/standards/ecma-48/">ECMA-48</a> for more formatting information.
    /// </remarks>
    public sealed class RichTextRenderer
    {
        #region PublicEnums
        public const char ESC = '\x1B';
        public static class C1_EscapeSequences
        {
            public static readonly char
            ControlSequenceIntroducer = '[',
            CSI = ControlSequenceIntroducer,
            PrivateUseOne = 'Q',
            PU1 = PrivateUseOne,
            PrivateUseTwo = 'R',
            PU2 = PrivateUseTwo;
        }
        public static class C1_8BitCodes
        {
            public static readonly char
            ControlSequenceIntroducer = '\x9B',
            CSI = ControlSequenceIntroducer,
            PrivateUseOne = '\x91',
            PU1 = PrivateUseOne,
            PrivateUseTwo = '\x92',
            PU2 = PrivateUseTwo;
        }
        /// <summary>
        /// CSI Commands with no Intermediate byte
        /// </summary>
        public static class CSICommands
        {
            public static readonly char
            StartReversedString = '\x5B',
            SRS = StartReversedString,
            StartDirectedString = '\x5D',
            SDS = StartDirectedString,
            SelectGraphicRendition = 'm',
            SGR = SelectGraphicRendition;
        }
        /// <summary>
        /// CSI Commands with \x20 as the Intermediate byte
        /// </summary>
        public static class CSICommands20
        {
            public static readonly char
            GraphicSizeModification = '\x42',
            GSM = GraphicSizeModification,
            GraphicSizeSelection = '\x43',
            GSS = GraphicSizeSelection,
            FontSelection = '\x44',
            FNT = FontSelection,
            ThinSpaceSpecification = '\x45',
            TSS = ThinSpaceSpecification,
            SpacingIncrement = '\x47',
            SPI = SpacingIncrement,

            SelectSizeUnit = '\x49',
            SSU = SelectSizeUnit,

            SelectPresentationDirections = '\x53',
            SPD = SelectPresentationDirections,

            PresentationExpandOrContract = '\x5A',
            PEC = PresentationExpandOrContract,
            SetSpaceWidth = '\x5B',
            SSW = SetSpaceWidth,
            SetAdditionalCharacterSeparation = '\\',
            SACS = SetAdditionalCharacterSeparation,

            SelectAlternativePresentationVariants = '\x5D',
            SAPV = SelectAlternativePresentationVariants,

            GraphicCharacterCombination = '\x5F',
            GCC = GraphicCharacterCombination,

            SelectCharacterOrientation = '\x65',
            SCO = SelectCharacterOrientation,
            SetReducedCharacterSeparation = '\x66',
            SRCS = SetReducedCharacterSeparation,
            SetCharacterSpacing = '\x67',
            SCS = SetCharacterSpacing,
            SetLineSpacing = '\x68',
            SLS = SetLineSpacing,

            SelectCharacterPath = '\x6B',
            SCP = SelectCharacterPath;
        }
        /// <summary>
        /// Select Graphic Rendition parameters 
        /// </summary>
        public static class SGRParameters
        {
            public const int
            Reset = 0,
            Bold = 1,
            Faint = 2,
            Italic = 3,
            Underline = 4,
            SlowBlink = 5,
            RapidBlink = 6,
            NegativeImage = 7,
            Obfuscate = 8,
            ConcealedCharacters = Obfuscate,
            Strikethrough = 9,

            //cases 10 to 20 are fonts

            DoubleUnderline = 21,
            NeitherBoldNorFaint = 22,
            NotItalic = 23,
            NotUnderlined = 24,
            NotBlinking = 25,
            ProportionalSpacing = 26,
            NotNegativeImage = 27,
            NotObfuscated = 28,
            RevealedCharacters = NotObfuscated,
            NotStrikethrough = 29,
            // Standard colors
            ColorDarkBlack = 30,
            ColorDarkRed = 31,
            ColorDarkGreen = 32,
            ColorDarkYellow = 33,
            ColorDarkBlue = 34,
            ColorDarkMagenta = 35,
            ColorDarkCyan = 36,
            ColorDarkWhite = 37,
            ColorStartTrueColor = 38,
            ResetFgColor = 39,
            // cases 40 to 49 are for backgrounds, in the same order as 30 to 39
            BgColorDarkBlack = 40,
            BgColorDarkRed = 41,
            BgColorDarkGreen = 42,
            BgColorDarkYellow = 43,
            BgColorDarkBlue = 44,
            BgColorDarkMagenta = 45,
            BgColorDarkCyan = 46,
            BgColorDarkWhite = 47,
            BgColorStartTrueColor = 48,
            ResetBgColor = 49,

            CancelProportionalSpacing = 50,
            Framed = 51,
            Encircled = 52,
            Overlined = 53,
            NotFramedNotEncircled = 54,
            NotOverlined = 55,

            // cases 56 to 59 are not defined

            IdeogramUnderline = 60,
            IdeogramDoubleUnderline = 61,
            IdeogramOverline = 62,
            IdeogramDoubleOverline = 63,
            IdeogramStressMarking = 64,
            NoIdeogramEffects = 65,

            //nothing defined for 66 to 89

            // Bright colors
            ColorBrightBlack = 90,
            ColorBrightRed = 91,
            ColorBrightGreen = 92,
            ColorBrightYellow = 93,
            ColorBrightBlue = 94,
            ColorBrightMagenta = 95,
            ColorBrightCyan = 96,
            ColorBrightWhite = 97,

            //nothing defined for 98 to 99

            // cases 100 to 107 are for backgrounds, in the same order as 90 to 97
            BgColorBrightBlack = 100,
            BgColorBrightRed = 101,
            BgColorBrightGreen = 102,
            BgColorBrightYellow = 103,
            BgColorBrightBlue = 104,
            BgColorBrightMagenta = 105,
            BgColorBrightCyan = 106,
            BgColorBrightWhite = 107;

            //nothing defined after 107
        }
        #endregion
        #region PrivateStructsAndEnums
        /// <summary>
        /// Contains a <see cref="SpriteFont"/> and the float value used to make this font appear at the same scale as other fonts. <br />
        /// These values should match up with the possible values seen in <see cref="FontManager"/> for <see cref="FontManager.Big"/> and <see cref="FontManager.BigFactor"/>
        /// </summary>
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
            #pragma warning disable IDE0055
            Underline       = 0b000000001,
            Strikethrough   = 0b000000010,
            Overline        = 0b000000100,
            DoubleUnderline = 0b000001000 | Underline,
            Framed          = 0b000010000,
            Encircled       = 0b000100000,
            DoubleOverline  = 0b001000000 | Overline,
            Concealed       = 0b010000000,
            StressMarking   = 0b100000000,
            #pragma warning restore IDE0055
        }
        /// <summary>
        /// Represents all the text styles to use for this token.
        /// </summary>
        private sealed class TokenStyle
        {
            /// <summary>
            /// The color to use for the text of this token
            /// </summary>
            public Color Color { get; set; }
            /// <summary>
            /// The color to use for the background of this token
            /// </summary>
            public Color BackgroundColor { get; set; }
            /// <summary>
            /// The color to use for the <see cref="Decoration"/> of this token.
            /// </summary>
            public Color? DecorationColor { get; set; }
            /// <summary>
            /// The font to use for this token
            /// </summary>
            public FontData FontData { get; set; }
            /// <summary>
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
            /// Text slant, in degrees. See https://developer.mozilla.org/en-US/docs/Web/CSS/font-style
            /// </summary>
            public float ObliqueAngle { get; set; }
            /// <summary>
            /// The duration, in seconds, for the text to be on/off. <br />
            /// A value of 0 or lower indicates the text does not blink.
            /// </summary>
            public float BlinkDuration { get; set; }

            public TokenStyle(Color color, Color backgroundColor, Color? decorationColor, FontData fontdata, TextDecoration decoration, ushort weight, float slantAngle)
            {
                this.Color = color;
                this.BackgroundColor = backgroundColor;
                this.DecorationColor = decorationColor;
                this.FontData = fontdata;
                this.Decoration = decoration;
                this.FontWeight = weight;
                this.ObliqueAngle = slantAngle;
            }

            public TokenStyle Clone()
            {
                return (TokenStyle)MemberwiseClone();
            }
        }
        /// <summary>
        /// Contains the text and style for a token.
        /// </summary>
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
        #endregion

        static RichTextRenderer()
        {
            _ = Waiters.Wait(() => FezMultiplayerMod.Instance?.GraphicsDevice != null,
            () =>
            {
                const int size = 1000;
                const float cx = size / 2f;
                const float cy = size / 2f;
                const double circleThicknessHalved = size / 100.0;
                const double circleRadius = size / 2.0 - circleThicknessHalved;//ensure circle is entirely in the target area
                const double outerRadius = circleRadius + circleThicknessHalved;
                const double innerRadius = circleRadius - circleThicknessHalved;

                GraphicsDevice graphicsDevice = FezMultiplayerMod.Instance.GraphicsDevice;
                CircleTexture = new Texture2D(graphicsDevice, size, size, false, SurfaceFormat.Color);

                Color[] colors = new Color[size * size];

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        int index = x + y * size;
                        // Calculate distance from the center
                        float dx = x - cx;
                        float dy = y - cy;
                        double distance = Math.Sqrt(dx * dx + dy * dy);
                        if (distance >= innerRadius && distance <= outerRadius)
                        {
                            // Inside the circle
                            colors[index] = Color.White;
                        }
                        else
                        {
                            // Outside the circle - transparent background
                            colors[index] = Color.Transparent;
                        }
                    }
                }

                CircleTexture.SetData(colors);
            });
        }
        private static Texture2D CircleTexture = null;
        private static void DrawEllipse(SpriteBatch batch, Color color, in Vector2 position, in float height, in float width)
        {
            if (CircleTexture != null)
            {

                //Vector2 scale = new Vector2(width / CircleTexture.Width, height / CircleTexture.Height);
                //batch.Draw(CircleTexture, position, null, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

                int x = FezMath.Round(position.X);
                int y = FezMath.Round(position.Y);
                int w = (int)Math.Ceiling(width);
                int h = (int)Math.Ceiling(height);

                batch.Draw(CircleTexture, new Rectangle(x, y, w, h), new Rectangle(0, 0, CircleTexture.Width, CircleTexture.Height), color);
            }
        }

        #region Fonts
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

        /// <summary>
        /// Gets the first supported font for the given character, prefering <paramref name="defaultFontData"/> if possible, 
        /// </summary>
        /// <param name="defaultFontData">The default font to use</param>
        /// <param name="ch">The character to find a supported font for</param>
        /// <returns>
        ///         <paramref name="defaultFontData"/> if that font supports the character, else the first compatible font in <see cref="Fonts"/>.
        ///         If no compatible font is found, returns defaultFontData.
        /// </returns>
        private static FontData GetFirstSupportedFont(in FontData defaultFontData, in char ch)
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
        /// <summary>
        /// Returns <c>true</c> if the supplied <see cref="SpriteFont"/> supports the provided character.
        /// </summary>
        /// <param name="font">The font to check</param>
        /// <param name="ch">The character to check</param>
        /// <returns><c>true</c> if the supplied <see cref="SpriteFont"/> supports the provided character; false otherwise.</returns>
        private static bool FontSupportsCharacter(in SpriteFont font, in char ch)
        {
            return font.Characters.Contains(ch);
        }
        #endregion
        #region MeasureString
        /// <inheritdoc cref="MeasureString(SpriteFont, float, string)"/>
        /// <inheritdoc cref="DrawString(SpriteBatch, IFontManager, string, Vector2, Color, Color, float, float)"/>
        public static Vector2 MeasureString(IFontManager fontManager, string text)
        {
            return MeasureString(fontManager.Big, fontManager.BigFactor, text);
        }
        /// <summary>
        /// Measures the size of the specified text, taking into account any ANSI escape codes that may have affected the size of the text. <br />
        /// <br />
        /// <inheritdoc cref="RichTextRenderer"/>
        /// </summary>
        /// <param name="text">The text to measure</param>
        /// <inheritdoc cref="DrawString(SpriteBatch, SpriteFont, float, string, Vector2, Color, Color, Vector2, float)"/>
        /// <returns>A <see cref="Vector2"/> representing the size of the text.</returns>
        public static Vector2 MeasureString(SpriteFont defaultFont, float defaultFontScale, string text)
        {
            return ProcessECMA48EscapeCodes(defaultFont, defaultFontScale, text, Color.White, Color.Transparent, Vector2.One, (token, positionOffset, tokens, tokenIndex) => { });
        }
        #endregion
        #region DrawStringOverloads
        /// <inheritdoc cref="DrawString(SpriteBatch, IFontManager, string, Vector2, Color, Color, Vector2, float)"/>
        public static void DrawString(SpriteBatch batch, IFontManager fontManager, string text, Vector2 position, Color defaultColor)
        {
            DrawString(batch, fontManager, text, position, defaultColor, Color.Transparent);
        }
        /// <param name="fontManager">The fontManager to use to get the information about the default font to use for this text.</param>
        /// <inheritdoc cref="DrawString(SpriteBatch, SpriteFont, float, string, Vector2, Color, Color, Vector2, float)"/>
        public static void DrawString(SpriteBatch batch, IFontManager fontManager, string text, Vector2 position, Color defaultColor, Color defaultBGColor, float scale = 1, float layerDepth = 0f)
        {
            DrawString(batch, fontManager.Big, fontManager.BigFactor, text, position, defaultColor, defaultBGColor, scale, layerDepth);
        }

        /// <param name="fontManager">The fontManager to use to get the information about the default font to use for this text.</param>
        /// <inheritdoc cref="DrawString(SpriteBatch, SpriteFont, float, string, Vector2, Color, Color, Vector2, float)"/>
        public static void DrawString(SpriteBatch batch, IFontManager fontManager, string text, Vector2 position, Color defaultColor, Color defaultBGColor, Vector2 scale, float layerDepth = 0f)
        {
            DrawString(batch, fontManager.Big, fontManager.BigFactor, text, position, defaultColor, defaultBGColor, scale, layerDepth);
        }

        /// <inheritdoc cref="DrawString(SpriteBatch, SpriteFont, float, string, Vector2, Color, Color, Vector2, float)"/>
        public static void DrawString(SpriteBatch batch, SpriteFont defaultFont, float defaultFontScale, string text, Vector2 position, Color defaultColor)
        {
            DrawString(batch, defaultFont, defaultFontScale, text, position, defaultColor, Color.Transparent);
        }

        /// <inheritdoc cref="DrawString(SpriteBatch, SpriteFont, float, string, Vector2, Color, Color, Vector2, float)"/>
        public static void DrawString(SpriteBatch batch, SpriteFont defaultFont, float defaultFontScale, string text, Vector2 position, Color defaultColor, Color defaultBGColor, float scale = 1f, float layerDepth = 0f)
        {
            DrawString(batch, defaultFont, defaultFontScale, text, position, defaultColor, defaultBGColor, new Vector2(scale, scale), layerDepth);
        }
        #endregion

        /// <param name="batch">The <see cref="SpriteBatch"/> to use to draw the text.</param>
        /// <param name="defaultFont">
        ///         The default font to use for the text. <br />
        ///         See also: <seealso cref="FontManager.Big"/>
        /// </param>
        /// <param name="defaultFontScale">
        ///         The scaling factor to use for the font provided in the parameter <paramref name="defaultFont"/> so it appears the same size as the other fonts. <br />
        ///         See also: <see cref="FontManager.BigFactor"/>
        /// </param>
        /// <param name="text">The text to draw</param>
        /// <param name="position">The top-left position to draw the text</param>
        /// <param name="defaultColor">The default color to use for the text</param>
        /// <param name="defaultBGColor">The default background color to use for the text</param>
        /// <param name="scale">The scale by which to render the text (Note: Do NOT confuse this with <paramref name="defaultFontScale"/>)</param>
        /// <param name="layerDepth">To be honest, I don't really know what this does, but the default value is 0f</param>
        /// <remarks>
        /// Note: This method assumes all the characters in a given font are the same height.<br />
        /// <br/>
        /// <include cref="RichTextRenderer"/>
        /// </remarks>
        /// <inheritdoc cref="RichTextRenderer"/>
        public static void DrawString(SpriteBatch batch, SpriteFont defaultFont, float defaultFontScale, string text, Vector2 position, Color defaultColor, Color defaultBGColor, Vector2 scale, float layerDepth = 0f)
        {
            if (batch is null)
            {
                throw new ArgumentNullException(nameof(batch));
            }
            if (defaultFont is null)
            {
                throw new ArgumentNullException(nameof(defaultFont));
            }
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            /*
             * Note: currently, I think tokens are drawn with vertical-align: top
             * Because of this, it is very important that the font scale and values in List<FontData> are correct, and all fonts appear the same scale
             */

            float lineheight = defaultFont.MeasureString("o").Y * defaultFontScale * scale.Y;

            //Might have to tweak these values 
            const float lineThickness_LineheightPercentage = 0.03f;
            const float underlineOffset_LineheightPercentage = 1 - lineThickness_LineheightPercentage;
            const float overlineOffset_LineheightPercentage = 0;// -lineThickness_LineheightPercentage - 0.005f;
            const float strikethroughOffset_LineheightPercentage = 0.50f;
            const float doublelineOffsetOffset_LineheightPercentage = 0.01f;

            // figure out the vertical positions of where the underline and overline and strikethrough should go
            // Y position offsets
            float underlineOffset = lineheight * underlineOffset_LineheightPercentage;
            float overlineOffset = lineheight * overlineOffset_LineheightPercentage;
            float strikethroughOffset = lineheight * strikethroughOffset_LineheightPercentage;
            float scaledMinLineSize = Math.Max(1f, scale.Y);
            float lineThickness = Math.Max(scaledMinLineSize, lineheight * lineThickness_LineheightPercentage);
            float doublelineOffsetOffset = Math.Max(lineThickness + scaledMinLineSize, lineheight * doublelineOffsetOffset_LineheightPercentage);

            _ = ProcessECMA48EscapeCodes(defaultFont, defaultFontScale, text, defaultColor, defaultBGColor, scale, (token, positionOffset, tokens, tokenIndex) =>
            {
                TextDecoration decoration = token.Style.Decoration;

                FontData fontData = token.Style.FontData;
                Color textColor = token.Style.Color;
                Color backgroundColor = token.Style.BackgroundColor;
                Color decorationColor = token.Style.DecorationColor ?? textColor;

                Vector2 offsetPosition = position + positionOffset;

                float letterSpacing = 1;//TODO

                /// TODO implement font weight (maybe draw this many times, shifting over by 1 px every time)
                ushort weight = token.Style.FontWeight;
                /// TODO draw slanted text
                float slant = token.Style.ObliqueAngle;
                /// TODO draw blinking text somehow
                float blink = token.Style.BlinkDuration;

                Vector2 tokenSize = fontData.Font.MeasureString(token.Text) * fontData.Scale * scale;
                float tokenWidth = tokenSize.X + letterSpacing;

                batch.DrawRect(offsetPosition, tokenSize.X, tokenSize.Y, backgroundColor);

                bool IsConcealed = decoration.HasFlag(TextDecoration.Concealed);

                //Draw the characters and decorations if they're not concealed
                if (!IsConcealed)
                {
                    batch.DrawString(fontData.Font, token.Text, offsetPosition, textColor, 0f, Vector2.Zero, fontData.Scale * scale, SpriteEffects.None, layerDepth);

                    //Draw text decorations
                    if (decoration.HasFlag(TextDecoration.Underline))
                    {
                        Vector2 underlinePosition = offsetPosition + underlineOffset * Vector2.UnitY;
                        // draw line with width of token starting at position underlinePosition
                        batch.DrawRect(underlinePosition, tokenWidth, lineThickness, decorationColor);
                        if (decoration.HasFlag(TextDecoration.DoubleUnderline))
                        {
                            underlinePosition += doublelineOffsetOffset * Vector2.UnitY;
                            // draw line with width of token starting at position underlinePosition
                            batch.DrawRect(underlinePosition, tokenWidth, lineThickness, decorationColor);
                        }
                    }
                    if (decoration.HasFlag(TextDecoration.Strikethrough))
                    {
                        Vector2 strikethroughPosition = offsetPosition + strikethroughOffset * Vector2.UnitY;
                        // draw line with width of token starting at position strikethroughPosition
                        batch.DrawRect(strikethroughPosition, tokenWidth, lineThickness, decorationColor);
                    }
                    if (decoration.HasFlag(TextDecoration.Overline))
                    {
                        Vector2 overlinePosition = offsetPosition + overlineOffset * Vector2.UnitY;
                        // draw line with width of token starting at position overlinePosition
                        batch.DrawRect(overlinePosition, tokenWidth, lineThickness, decorationColor);
                        if (decoration.HasFlag(TextDecoration.DoubleOverline))
                        {
                            overlinePosition -= doublelineOffsetOffset * Vector2.UnitY;
                            // draw line with width of token starting at position overlinePosition
                            batch.DrawRect(overlinePosition, tokenWidth, lineThickness, decorationColor);
                        }
                    }
                    if (decoration.HasFlag(TextDecoration.StressMarking))
                    {
                        //TODO idk what this is supposed to look like
                    }
                }
                //draw the frame and circle regardless of the concealed status 
                float padding = lineThickness;
                Vector2 boxOrigin = offsetPosition - new Vector2(padding + lineThickness, padding + lineThickness);
                float boxBottomRightOffset = padding * 2 + lineThickness * 2;
                // draw the outline of a box around the characters
                float boxWidth = tokenSize.X + boxBottomRightOffset;
                float boxHeight = tokenSize.Y + boxBottomRightOffset;
                if (decoration.HasFlag(TextDecoration.Framed))
                {
                    //top
                    batch.DrawRect(boxOrigin, boxWidth, lineThickness, decorationColor);
                    //left; check the previous token isn't also framed
                    if (tokenIndex <= 0 || !tokens[tokenIndex - 1].Style.Decoration.HasFlag(TextDecoration.Framed))
                    {
                        //left
                        batch.DrawRect(boxOrigin, lineThickness, boxHeight, decorationColor);
                    }
                    //bottom
                    batch.DrawRect(boxOrigin + new Vector2(0, boxHeight - lineThickness), boxWidth, lineThickness, decorationColor);
                    //right; check the next token isn't also framed
                    if (tokenIndex + 1 >= tokens.Count || !tokens[tokenIndex + 1].Style.Decoration.HasFlag(TextDecoration.Framed))
                    {
                        //right
                        batch.DrawRect(boxOrigin + new Vector2(boxWidth - lineThickness, 0), lineThickness, boxHeight, decorationColor);
                    }
                }
                //check the previous token isn't also circled
                bool isCircleStart = (tokenIndex <= 0 || !tokens[tokenIndex - 1].Style.Decoration.HasFlag(TextDecoration.Encircled));
                //draw circle around entire encircled area
                if (isCircleStart && decoration.HasFlag(TextDecoration.Encircled))
                {
                    int circleStartTokenIndex = tokenIndex;
                    int circleEndTokenIndex = tokens.Count - 1;
                    for (int i = circleStartTokenIndex; i < tokens.Count; ++i)
                    {
                        if (!tokens[i].Style.Decoration.HasFlag(TextDecoration.Encircled))
                        {
                            circleEndTokenIndex = i;
                            break;
                        }
                    }
                    //draw the outline of an ellipse around the characters
                    float height = boxHeight;
                    float width = boxWidth + tokens
                            .Where((t, i) => (circleStartTokenIndex < i && i < circleEndTokenIndex))
                            .Sum(t =>
                            {
                                FontData f = t.Style.FontData;
                                return (f.Font.MeasureString(t.Text) * f.Scale * scale).X;
                            });
                    DrawEllipse(batch, decorationColor, in boxOrigin, in height, in width);
                }
            });
        }
        //TODO add more tests
        public static readonly Dictionary<int, string[]> testStrings = new Dictionary<int, string[]>() {
            {1, new string[]{ "\x1B[10\x20\x68",//set line spacing
                "\x1B[31mThis is red text\x1B[0m and this is normal.",
                "\x1B[1mBold Text\x1B[0m then \x1B[34mBlue Text\x1B[0m, returning to normal.",
                $"{C1_8BitCodes.ControlSequenceIntroducer}31;1mRed and bold\x1B[0m but normal here. \x1B[32mGreen text\x1B[0m.",
                "Some text \x1B[32mGreen\x1B[0m, then some text \x1B[35Hello but this won't change.",
                "Multifont test: [\u3046e\u56DB\uAFB8\u3044\u5B89\uD658]",
                "Colored multifont test: [Y\x1B[31m\u3042\u3044\xE9\x1B[93m\u56DB\u5B89\x1B[96m\uAFB8\uD658\x1B[90m\uFF1FZ\x1B[0m\u4E0AW]",
                "dark: \x1B[30mBlack\x1B[0m, \x1B[31mRed\x1B[0m, \x1B[32mGreen\x1B[0m, \x1B[33mYellow\x1B[0m, \x1B[34mBlue\x1B[0m, \x1B[35mMagenta\x1B[0m, \x1B[36mCyan\x1B[0m, \x1B[37mWhite\x1B[0m.",
                "light: \x1B[90mBlack\x1B[0m, \x1B[91mRed\x1B[0m, \x1B[92mGreen\x1B[0m, \x1B[93mYellow\x1B[0m, \x1B[94mBlue\x1B[0m, \x1B[95mMagenta\x1B[0m, \x1B[96mCyan\x1B[0m, \x1B[97mWhite\x1B[0m.",
                "8bit colors: \x1B[38;5;237mGrayish\x1B[38;5;10mLime\x1B[38;5;213mPink\x1B[38;5;208mOrange.",
                "true bit colors: \x1B[38;2;255;0;255mMagenta.",
            }},
            {2, new string[]{ "\x1B[10\x20\x68",//set line spacing
                $"{C1_8BitCodes.ControlSequenceIntroducer}21;53mThis text has both double underlined and has an overline\x1B[0m",
                "\x1B[21mdouble underlined\x1B[24m, \x1B[53moverlined\x1B[55m, \x1B[9mstrikethrough\x1B[29m",
                "\x1B[63mdouble overline\x1B[55m, \x1B[9mstrikethrough\x1B[29m, \x1B[21mdouble underlined\x1B[24m",
                $"{C1_8BitCodes.ControlSequenceIntroducer}63;9;21mThis text has all double overline, strikethrough, and double underline\x1B[0m",
                "Decorated colored multifont test: [\x1B[63;9;21mY\x1B[31m\u3042\u3044\xE9\x1B[93m\u56DB\u5B89\x1B[96m\uAFB8\uD658\x1B[90m\uFF1FZ\x1B[38;2;255;0;255m\u4E0AW\x1B[0m]",
                "\x1B[51mFramed\x1B[54m, \x1B[52mEncircled\x1B[54m, and \x1B[51;52mboth\x1B[54m\x1B[0m",
            }}
        };
        private static readonly ushort DefaultFontWeight = 1;
        /// <summary>
        /// One second on, one second off. See <see cref="TokenStyle.BlinkDuration"/>
        /// </summary>
        private static readonly float TextBlinkDurationSlow = 1f;
        /// <summary>
        /// half second on, half second off. See <see cref="TokenStyle.BlinkDuration"/>
        /// </summary>
        /// <remarks>
        /// This value should be less than or equal to <see cref="TextBlinkDurationSlow"/>, but 
        /// always greater than 0.4f to comply with the WCAG limit of 3 flashes per second.
        /// </remarks>
        private static readonly float TextBlinkDurationFast = 0.5f;

        /// <summary>
        /// Iterates through the provided <paramref name="text"/> line by line, parsing ECMA-48 escape codes, and calling <paramref name="onToken"/> on each token, in order of occurance.
        /// </summary>
        /// <param name="text">The text to process the escape codes on</param>
        /// <param name="onToken">
        ///         The action to perform on each token,<br />
        ///         accepting a <see cref="TokenizedText"/> representing the calculated presentation settings and text for the token, <br />
        ///         a <see cref="Vector2"/> representing the calculated top-left corner of the token,<br />
        ///         a <see cref="List&lt;TokenizedText&gt;"/> containing all the <see cref="TokenizedText"/> for the current line of text, <br />
        ///         and an <see cref="int"/> indicating the index of the current <see cref="TokenizedText"/> in the aforementioned list. 
        /// </param>
        /// <inheritdoc cref="MeasureString(SpriteFont, float, string)"/>
        /// <inheritdoc cref="DrawString(SpriteBatch, IFontManager, string, Vector2, Color, Color, float, float)"/>
        private static Vector2 ProcessECMA48EscapeCodes(in SpriteFont defaultFont, in float defaultFontScale,
                in string text, in Color defaultColor, in Color defaultBGColor, in Vector2 scale,
                Action<TokenizedText, Vector2, List<TokenizedText>, int> onToken)
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
            TokenStyle currentStyle = new TokenStyle(defaultColor, Color.Transparent, null, defaultFontData, TextDecoration.None, DefaultFontWeight, 0f);

            float defaultLineSpacing = defaultFontData.Font.LineSpacing * defaultFontData.Scale;
            float currentLineSpacing = defaultLineSpacing;
            SizeUnit sizeUnit = SizeUnit.Character;

            for (int i = 0; i < lines.Length; ++i)
            {
                line = lines[i];
                Vector2 linesize = Vector2.Zero;
                List<TokenizedText> tokens = TokenizeChars(line, in defaultFontData, in defaultColor, in defaultBGColor, in defaultLineSpacing,
                        currentStyle, ref currentLineSpacing, ref sizeUnit
                        );
                for (int currentTokenIndex = 0; currentTokenIndex < tokens.Count; ++currentTokenIndex)
                {
                    TokenizedText token = tokens[currentTokenIndex];
                    FontData fontData = token.Style.FontData;
                    Vector2 tokensize;
                    try
                    {
                        tokensize = fontData.Font.MeasureString(token.Text.ToString()) * fontData.Scale * scale;
                    }
                    catch (Exception e)
                    {
                        tokensize = fontData.Font.MeasureString("" + (fontData.Font.DefaultCharacter ?? ' ')) * fontData.Scale * scale;
                        FezSharedTools.SharedTools.LogWarning(typeof(RichTextRenderer).Name, e.ToString());
                        System.Diagnostics.Debugger.Launch();
                    }
                    onToken(token, currentPositionOffset, tokens, currentTokenIndex);
                    linesize.Y = Math.Max(linesize.Y, tokensize.Y);
                    float tokenSizeXWithSpacing = tokensize.X + fontData.Font.Spacing;
                    linesize.X += tokenSizeXWithSpacing;
                    currentPositionOffset.X += tokenSizeXWithSpacing;
                };
                size.X = Math.Max(linesize.X, size.X);
                size.Y += linesize.Y;
                currentPositionOffset.Y += linesize.Y;
                //check if there's more lines
                if (i + 1 < lines.Length)
                {
                    currentPositionOffset.X = 0;
                    size.Y += currentLineSpacing;
                    currentPositionOffset.Y += currentLineSpacing;
                }
            }
            return size;
        }
        private enum SizeUnit
        {
            Character = 0,
            Pixel = 7
        }
        /// <summary>
        /// Parses all ECMA-48 escape codes in the provided <paramref name="text"/>,
        /// returning a <see cref="List{TokenizedText}"/> of <see cref="TokenizedText"/> objects containing information on the presentation data and text for each token.
        /// </summary>
        /// <param name="text">The line of text to process the escape codes on</param>
        /// <param name="defaultFontData">The default font (for the current font, see <paramref name="currentStyle"/>)</param>
        /// <param name="defaultColor">The default color (for the current color, see <paramref name="currentStyle"/>)</param>
        /// <param name="defaultBGColor">The default background color (for the current background color, see <paramref name="currentStyle"/>)</param>
        /// <param name="defaultLineSpacing">The default line spacing (for the current line spacing, see <paramref name="currentLineSpacing"/>)</param>
        /// <param name="currentStyle">
        ///         The current style used to store and use for the next tokens. <br />
        ///         The values in the provided object will be updated according to any valid ECMA-48 escape codes.
        /// </param>
        /// <param name="currentLineSpacing">The current line spacing to use for the tokens on this and subsequent lines. </param>
        /// <returns>A <see cref="List{TokenizedText}"/> of <see cref="TokenizedText"/> objects containing information on the presentation data and text for each token.</returns>
        private static List<TokenizedText> TokenizeChars(in string text, in FontData defaultFontData, in Color defaultColor, in Color defaultBGColor, in float defaultLineSpacing,
                TokenStyle currentStyle, ref float currentLineSpacing, ref SizeUnit sizeUnit)
        {
            List<TokenizedText> tokens = new List<TokenizedText>();
            StringBuilder currentToken = new StringBuilder();
            for (int i = 0; i < text.Length; ++i)//changed from foreach so we can look ahead from the current position 
            {
                char c = text[i];

                char? NullableC1_8bitCode = typeof(C1_8BitCodes).HasValue(c)
                                ? (char?)c : null;

                //check for special characters to change currentColor and whatever other presentation options we want to include; see "Select Graphic Rendition"
                if (c == '\x1B' || NullableC1_8bitCode.HasValue)//ANSI escape codes
                {
                    //flush current token
                    if (currentToken.Length > 0)
                    {
                        tokens.Add(new TokenizedText(currentToken.ToString(), currentStyle));
                        currentToken.Clear();
                    }

                    /*
                     * Note: 
                     * This code only handles Presentation control functions that I (Jenna Sloan) deemed possible to reasonably implement in the XNA framework.
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
                     *     Miscellaneous control functions,
                     *     and codes that use absolute sizes (e.g., mm ) (mainly because that would be confusing with DrawString's `scale` parameter)
                     * with the exception of PRIVATE USE ONE and PRIVATE USE TWO, which may be used for something at a later date.
                     * 
                     * Anyways, I manually went through all the escape codes and picked out ones that I though would fit with XNA's DrawString.
                     * I thought about copying over the specifications from ECMA-48, but they're so verbose and I don't want it to clutter the code.
                     * ECMA-48 does a great job describing what each code does and how they interact with each other,
                     *   so just reference it to get an idea of how the code below should function.
                     */

                    if (NullableC1_8bitCode.HasValue || i + 1 < text.Length)
                    {
                        ///See: <see cref="C1_EscapeSequences"/>
                        char? nextChar = text.Length >= (i + 1) ? (char?)text[i + 1] : null;
                        char? escapeSequence = nextChar.HasValue && typeof(C1_EscapeSequences).HasValue(nextChar.Value)
                                ? (char?)nextChar : null;
                        if (NullableC1_8bitCode == C1_8BitCodes.ControlSequenceIntroducer
                                || escapeSequence == C1_EscapeSequences.ControlSequenceIntroducer)
                        {
                            // CSI (Control Sequence Identifier) codes

                            // Start collecting the escape sequence
                            int start;
                            if (NullableC1_8bitCode.HasValue)
                            {
                                start = i + 1;
                            }
                            else if (escapeSequence.HasValue)
                            {
                                start = i + 2;
                            }
                            else
                            {
                                throw new ArgumentException("Somehow got to the C1 control code processing code without a value for C1");
                            }
                            int i_temp = i;
                            i_temp += 2; // Skip over the escape and the '['

                            // Collect until we find a character in the range ['\x40' and '\x7F'] or the end of the string
                            while (i_temp < text.Length && !((c = text[i_temp]) >= '\x40' && c <= '\x7F'))
                            {
                                i_temp++;
                            }

                            if (i_temp < text.Length)
                            {
                                // Note: some of the escape codes have a space as the penultimate character
                                if (text[i_temp - 1] == '\x20')
                                {
                                    // Capture the full parameter, excluding the ESC and '[' and ending characters
                                    string parameters = text.Substring(start, i_temp - start - 1);


                                    // Excerp from ECMA-48 section 5.4.2 "Parameter string format":
                                    // b) Each parameter sub-string consists of one or more bit combinations from 03/00 to 03/10;
                                    //   the bit combinations from 03/00 to 03/09 represent the digits ZERO to NINE;
                                    //   bit combination 03/10 may be used as a separator in a parameter sub-string,
                                    //    for example, to separate the fractional part of a decimal number from the integer part of that number.
                                    parameters = parameters.Replace('\x3A', '.');

                                    // Note: our code intentially allows for positive and negative signs in parameter strings, 
                                    // but ECMA-48 only allows for the bit combinations from \x30 to \x3F
                                    // Note ECMA-48 says the range \x3C and \x3F is intended for private use,
                                    // so we could use the characters in the range \x3C and \x3F for positive/negative signs

                                    ///See: <see cref="CSICommands20"/>
                                    switch (text[i_temp])
                                    {
                                    /*
                                     * See Table 4 in ECMA-48 ( https://www.ecma-international.org/publications-and-standards/standards/ecma-48/ )
                                     * Note in ECMA-48 the "Representation" text is in a format where 02/00 is character \x20, 04/11 is \x4B, 04/15 is \x4F, etc.
                                    **/
                                    //TODO support all these empty switch cases?
                                    //TODO change these hex codes to their corresponding ASCII character?
                                    case '\x42'://GSM - GRAPHIC SIZE MODIFICATION
                                        break;
                                    case '\x43'://GSS - GRAPHIC SIZE SELECTION
                                        break;
                                    case '\x44'://FNT - FONT SELECTION
                                        break;
                                    case '\x45'://TSS - THIN SPACE SPECIFICATION
                                        break;
                                    case '\x47'://SPI - SPACING INCREMENT
                                        break;

                                    case '\x49'://SSU - SELECT SIZE UNIT
                                        if (int.TryParse(parameters, out int newSizeUnit))
                                        {
                                            switch (newSizeUnit)
                                            {
                                            case 7:
                                                sizeUnit = SizeUnit.Pixel;
                                                break;
                                            case 0:
                                            default:
                                                sizeUnit = SizeUnit.Character;
                                                break;
                                            }
                                        }
                                        else
                                        {
                                            sizeUnit = SizeUnit.Character;
                                        }
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
                                    case '\x5F'://GCC - GRAPHIC CHARACTER COMBINATION
                                        break;

                                    case '\x65'://SCO - SELECT CHARACTER ORIENTATION
                                        break;
                                    case '\x66'://SRCS - SET REDUCED CHARACTER SEPARATION
                                        break;
                                    case '\x67'://SCS - SET CHARACTER SPACING
                                        break;
                                    case '\x68'://SLS - SET LINE SPACING
                                        //Set to a percentage of the normal line spacing
                                        if (float.TryParse(parameters, out float newLineSpacing))
                                        {
                                            switch (sizeUnit)
                                            {
                                            case SizeUnit.Character:
                                                currentLineSpacing = defaultLineSpacing * (newLineSpacing / 100);
                                                break;
                                            case SizeUnit.Pixel:
                                                currentLineSpacing = newLineSpacing;
                                                break;
                                            default:
                                                break;
                                            }
                                        }
                                        else
                                        {
                                            //currentLineSpacing = defaultLineSpacing;
                                        }
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
                                    // Capture the full parameter, excluding the ESC and '[' and ending character
                                    string parameters = text.Substring(start, i_temp - start);
                                    ///See: <see cref="CSICommands"/>
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
                                        // Parse the escape sequence to change style data
                                        ParseSGREscape(parameters, defaultFontData, defaultColor, defaultBGColor, currentStyle);
                                        break;

                                    default:
                                        //Not supported escape code
                                        break;
                                    }
                                }
                                // skip to the next character
                                i = i_temp;
                                continue;//this continue jumps all the way back up to that for loop that iterates over the characters
                            }
                        }
                        //ESC codes
                        else if (NullableC1_8bitCode == C1_8BitCodes.PrivateUseOne
                                || escapeSequence == C1_EscapeSequences.PrivateUseOne)//PRIVATE USE ONE
                        {
                            //tbd?
                        }
                        else if (NullableC1_8bitCode == C1_8BitCodes.PrivateUseTwo
                            || escapeSequence == C1_EscapeSequences.PrivateUseTwo)//PRIVATE USE TWO
                        {
                            //tbd?
                        }
                        else
                        {
                            //Not supported escape code
                        }
                    }
                }
                FontData nextFont = GetFirstSupportedFont(defaultFontData, c);
                if (nextFont.Equals(currentStyle.FontData))
                {
                    currentToken.Append(c);
                }
                else
                {
                    if (currentToken.Length > 0)
                    {
                        tokens.Add(new TokenizedText(currentToken.ToString(), currentStyle));
                    }
                    currentToken.Clear();
                    currentToken.Append(c);
                    currentStyle.FontData = nextFont;
                }
                currentStyle.FontData = nextFont;
            }

            // Flush the remaining token if any
            if (currentToken.Length > 0)
            {
                tokens.Add(new TokenizedText(currentToken.ToString(), currentStyle));
            }

            return tokens;
        }
        private static readonly IList<Color> ColorTable = Array.AsReadOnly(new Color[]{
            Color.Black,   // Dark Black
            Color.Maroon,  // Dark Red
            Color.Green,   // Dark Green
            Color.Olive,   // Dark Yellow
            Color.Navy,    // Dark Blue
            Color.Purple,  // Dark Magenta
            Color.Teal,    // Dark Cyan
            Color.Silver,  // Dark White

            Color.Gray,    // Bright Black (Gray)
            Color.Red,     // Bright Red
            Color.Lime,    // Bright Green
            Color.Yellow,  // Bright Yellow
            Color.Blue,    // Bright Blue
            Color.Magenta, // Bright Magenta
            Color.Cyan,    // Bright Cyan
            Color.White,   // Bright White
        });
        private static void ParseSGREscape(in string parameters, in FontData defaultFontData, in Color defaultColor, in Color defaultBGColor,
                TokenStyle currentStyle)
        {
            var codes = parameters.Split(';');

            for (int i = 0; i < codes.Length; i++)
            {
                Color GetTrueColor(Color color)
                {
                    if (i + 1 < codes.Length && int.TryParse(codes[i + 1], out int colorType))
                    {
                        if (colorType == 5) // 8-bit color index
                        {
                            if (i + 2 < codes.Length && int.TryParse(codes[i + 2], out int colorIndex))
                            {
                                i += 2; // Skip the next two parameters
                                if (TryGetColorFrom8BitIndex(colorIndex, out Color newColor))
                                {
                                    return newColor;
                                };
                            }
                        }
                        else if (colorType == 2) // 24-bit true color
                        {
                            if (i + 4 < codes.Length &&
                                int.TryParse(codes[i + 2], out int r) &&
                                int.TryParse(codes[i + 3], out int g) &&
                                int.TryParse(codes[i + 4], out int b))
                            {
                                i += 4; // Skip the next four parameters
                                return new Color(MathHelper.Clamp(r, 0, 255) / 255f, MathHelper.Clamp(g, 0, 255) / 255f, MathHelper.Clamp(b, 0, 255) / 255f);
                            }
                        }
                    }
                    return color;
                }
                if (int.TryParse(codes[i], out int codeValue))
                {
                    ///See: <see cref="SGRParameters"/>
                    switch (codeValue)
                    {
                    case 0: // Reset
                        currentStyle.FontData = defaultFontData; // Reset to default font
                        currentStyle.Color = defaultColor; // Reset to default color
                        currentStyle.BackgroundColor = defaultBGColor; // Reset to default color
                        currentStyle.DecorationColor = null; // Reset to default color
                        currentStyle.Decoration = TextDecoration.None;
                        currentStyle.FontWeight = DefaultFontWeight;
                        currentStyle.ObliqueAngle = 0f;
                        break;
                    case 1: // Bold
                        //TODO set currentStyle.FontWeight to a value greater than DefaultFontWeight
                        break;
                    case 2: // Faint
                        //TODO set currentStyle.FontWeight to a value less than DefaultFontWeight
                        break;
                    case 3: // Italic
                        currentStyle.ObliqueAngle = 15f;
                        break;
                    case 4: // Underline
                        currentStyle.Decoration |= TextDecoration.Underline;
                        break;
                    case 5: // Slow blink
                        currentStyle.BlinkDuration = TextBlinkDurationSlow;
                        break;
                    case 6: // Rapid blink
                        currentStyle.BlinkDuration = TextBlinkDurationFast;
                        break;
                    case 7: // Negative image
                        //TODO decide if/how to implement this
                        break;
                    case 8: // Obfuscate / concealed characters
                        currentStyle.Decoration |= TextDecoration.Concealed;
                        break;
                    case 9: // Strikethrough
                        currentStyle.Decoration |= TextDecoration.Strikethrough;
                        break;

                    //cases 10 to 20 are fonts

                    case 21: // Double-underline
                        currentStyle.Decoration |= TextDecoration.DoubleUnderline;
                        break;
                    case 22: // Neither bold nor faint
                        currentStyle.FontWeight = DefaultFontWeight;
                        break;
                    case 23: // Not italic
                        currentStyle.ObliqueAngle = 0f;
                        break;
                    case 24: // Not underlined
                        currentStyle.Decoration &= ~(TextDecoration.Underline | TextDecoration.DoubleUnderline);
                        break;
                    case 25: // Not blinking
                        currentStyle.BlinkDuration = 0;
                        break;
                    case 26: // Proportional spacing
                        //Not supporting this
                        break;
                    case 27: // Not negative image
                        //TODO decide if/how to implement this
                        break;
                    case 28: // Revealed characters / Not obfuscated
                        currentStyle.Decoration &= ~TextDecoration.Concealed;
                        break;
                    case 29: // Not strikethrough
                        currentStyle.Decoration &= ~TextDecoration.Strikethrough;
                        break;

                    // Using Windows XP Console colors
                    // Note these are the same as the first 16 colors from TryGetColorFrom8BitIndex
                    // Standard colors
                    case 30: currentStyle.Color = ColorTable[0]; break; // Dark Black
                    case 31: currentStyle.Color = ColorTable[1]; break; // Dark Red
                    case 32: currentStyle.Color = ColorTable[2]; break; // Dark Green
                    case 33: currentStyle.Color = ColorTable[3]; break; // Dark Yellow
                    case 34: currentStyle.Color = ColorTable[4]; break; // Dark Blue
                    case 35: currentStyle.Color = ColorTable[5]; break; // Dark Magenta
                    case 36: currentStyle.Color = ColorTable[6]; break; // Dark Cyan
                    case 37: currentStyle.Color = ColorTable[7]; break; // Dark White

                    case 38: // Start of 8-bit or true color
                        currentStyle.Color = GetTrueColor(currentStyle.Color);
                        break;
                    case 39: currentStyle.Color = defaultColor; break; // Reset color

                    // cases 40 to 49 are for backgrounds, in the same order as 30 to 39
                    case 40: currentStyle.BackgroundColor = ColorTable[0]; break; // Dark Black
                    case 41: currentStyle.BackgroundColor = ColorTable[1]; break; // Dark Red
                    case 42: currentStyle.BackgroundColor = ColorTable[2]; break; // Dark Green
                    case 43: currentStyle.BackgroundColor = ColorTable[3]; break; // Dark Yellow
                    case 44: currentStyle.BackgroundColor = ColorTable[4]; break; // Dark Blue
                    case 45: currentStyle.BackgroundColor = ColorTable[5]; break; // Dark Magenta
                    case 46: currentStyle.BackgroundColor = ColorTable[6]; break; // Dark Cyan
                    case 47: currentStyle.BackgroundColor = ColorTable[7]; break; // Dark White

                    case 48: // Start of 8-bit or true color
                        currentStyle.BackgroundColor = GetTrueColor(currentStyle.BackgroundColor);
                        break;
                    case 49: currentStyle.BackgroundColor = defaultBGColor; break; // Reset color

                    case 50: // Cancel proportional spacing
                        //Not supporting this
                        break;
                    case 51: // Framed
                        currentStyle.Decoration |= TextDecoration.Framed;
                        break;
                    case 52: // Encircled
                        currentStyle.Decoration |= TextDecoration.Encircled;
                        break;
                    case 53: // Overlined
                        currentStyle.Decoration |= TextDecoration.Overline;
                        break;
                    case 54: // Not framed, not encircled
                        currentStyle.Decoration &= ~(TextDecoration.Framed | TextDecoration.Encircled);
                        break;
                    case 55: // Not overlined
                        currentStyle.Decoration &= ~TextDecoration.Overline;
                        break;

                    // cases 56 to 59 are not defined

                    case 60: // ideogram underline or right side line
                        currentStyle.Decoration |= TextDecoration.Underline;
                        break;
                    case 61: // ideogram double underline or double line on the right side
                        currentStyle.Decoration |= TextDecoration.DoubleUnderline;
                        break;
                    case 62: // ideogram overline or left side line
                        currentStyle.Decoration |= TextDecoration.Overline;
                        break;
                    case 63: // ideogram double overline or double line on the left side
                        currentStyle.Decoration |= TextDecoration.DoubleOverline;
                        break;
                    case 64: // ideogram stress marking
                        currentStyle.Decoration |= TextDecoration.StressMarking;
                        break;
                    case 65: // cancels the effect of the rendition aspects established by parameter values 60 to 64
                        currentStyle.Decoration &= ~(TextDecoration.Underline
                                                     | TextDecoration.DoubleUnderline
                                                     | TextDecoration.Overline
                                                     | TextDecoration.DoubleOverline
                                                     | TextDecoration.StressMarking);
                        break;

                    //nothing defined for 66 to 89

                    // Bright colors
                    case 90: currentStyle.Color = ColorTable[8]; break;  // Bright Black (Gray)
                    case 91: currentStyle.Color = ColorTable[9]; break;  // Bright Red
                    case 92: currentStyle.Color = ColorTable[10]; break; // Bright Green
                    case 93: currentStyle.Color = ColorTable[11]; break; // Bright Yellow
                    case 94: currentStyle.Color = ColorTable[12]; break; // Bright Blue
                    case 95: currentStyle.Color = ColorTable[13]; break; // Bright Magenta
                    case 96: currentStyle.Color = ColorTable[14]; break; // Bright Cyan
                    case 97: currentStyle.Color = ColorTable[15]; break; // Bright White

                    //nothing defined for 98 to 99

                    // cases 100 to 107 are for backgrounds, in the same order as 90 to 97
                    case 100: currentStyle.Color = ColorTable[8]; break;  // Bright Black (Gray)
                    case 101: currentStyle.Color = ColorTable[9]; break;  // Bright Red
                    case 102: currentStyle.Color = ColorTable[10]; break; // Bright Green
                    case 103: currentStyle.Color = ColorTable[11]; break; // Bright Yellow
                    case 104: currentStyle.Color = ColorTable[12]; break; // Bright Blue
                    case 105: currentStyle.Color = ColorTable[13]; break; // Bright Magenta
                    case 106: currentStyle.Color = ColorTable[14]; break; // Bright Cyan
                    case 107: currentStyle.Color = ColorTable[15]; break; // Bright White

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
        private static bool TryGetColorFrom8BitIndex(in int index, out Color color)
        {
            // Check for valid index range
            if (index < 0 || index > 255)
            {
                color = Color.Transparent; // or any default color
                return false; // Indicate failure
            }
            if (index >= 0 && index < 16)
            {
                //The first 16 colors are the same as the "Standard colors" and "Bright colors" 
                color = ColorTable[index];
                return true;
            }
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

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
                //TODO check for special characters to change currentColor and whatever other presentation options we want to include; see "Select Graphic Rendition"
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
    }
}

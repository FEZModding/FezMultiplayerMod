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
        private static IList<FontData> Fonts = new List<FontData>();//TODO populate list

        private static List<TokenizedText> TokenizeChars(string text, FontData defaultFontData, Color defaultColor)
        {
            List<TokenizedText> tokens = new List<TokenizedText>();
            FontData lastFont = defaultFontData;
            FontData currentFont = defaultFontData;
            Color currentColor = defaultColor;
            string currentToken = "";
            foreach (char c in text)
            {
                currentFont = GetFirstSupportedFont(defaultFontData, c);
                if(currentFont.Equals(lastFont))
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
        public Vector2 MeasureString(SpriteFont defaultFont, float defaultFontScale, string text)
        {
            FontData defaultFontData = new FontData(defaultFont, defaultFontScale);
            Vector2 size = Vector2.Zero;
            string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            string line;
            for (int i=0; i<lines.Length; ++i)
            {
                line = lines[i];
                Vector2 linesize = Vector2.Zero;
                void lambda(TokenizedText token)
                {
                    FontData fontData = token.FontData;
                    var tokensize = fontData.Font.MeasureString(token.Text.ToString());
                    tokensize *= fontData.Scale;
                    linesize.X += tokensize.X + fontData.Font.Spacing;
                    linesize.Y = Math.Max(linesize.Y, tokensize.Y);
                }
                TokenizeChars(line, defaultFontData, Color.White).ForEach(lambda);
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
        public void DrawString(SpriteBatch batch, SpriteFont defaultFont, float defaultFontScale, string text, Vector2 position, Color defaultColor, float scale)
        {
            DrawString(batch, defaultFont, defaultFontScale, text, position, defaultColor, scale, 0);
        }
        public void DrawString(SpriteBatch batch, SpriteFont defaultFont, float defaultFontScale, string text, Vector2 position, Color defaultColor, float scale, float layerDepth)
        {
            Vector2 currentPositionOffset = Vector2.Zero;
            FontData defaultFontData = new FontData(defaultFont, defaultFontScale);
            string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            string line;
            for (int i = 0; i < lines.Length; ++i)
            {
                line = lines[i];
                Vector2 linesize = Vector2.Zero;
                void lambda(TokenizedText token)
                {
                    FontData fontData = token.FontData;
                    var tokensize = fontData.Font.MeasureString(token.Text.ToString()) *  fontData.Scale;
                    linesize.X += tokensize.X + fontData.Font.Spacing;
                    linesize.Y = Math.Max(linesize.Y, tokensize.Y);
                    currentPositionOffset.X += linesize.X;
                    batch.DrawString(fontData.Font, token.Text, position + currentPositionOffset, token.Color, 0f, Vector2.Zero, fontData.Scale * scale, SpriteEffects.None, layerDepth);
                }
                TokenizeChars(line, defaultFontData, Color.White).ForEach(lambda);
                currentPositionOffset.Y += linesize.Y;
                //check if there's more lines
                if (i + 1 < lines.Length)
                {
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

using FezEngine;
using FezEngine.Components;
using FezEngine.Effects;
using FezEngine.Structure;
using FezEngine.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace FezGame.MultiplayerMod
{
    public class TextDrawer3D
    {
        private readonly Dictionary<string, RenderTarget2D> textures;
        private readonly Mesh mesh = new Mesh()
        {
            AlwaysOnTop = true,
            SamplerState = SamplerState.PointClamp,
            Blending = BlendingMode.Alphablending
        };
        private readonly GlyphTextRenderer GTR;
        private readonly SpriteFont Font;
        private SpriteBatch spriteBatch;
        private Color TextColor = Color.White;
        public TextDrawer3D(Game Game, SpriteFont Font)
        {
            textures = new Dictionary<string, RenderTarget2D>();
            mesh.AddFace(new Vector3(1f), Vector3.Zero, FaceOrientation.Front, centeredOnOrigin: true, doublesided: true);
            GTR = new GlyphTextRenderer(Game);
            this.Font = Font;
        }
        //draws the name to a Texture2D, assign the texture to a Mesh, and draw the Mesh; See SpeechBubble for inspiration 
        internal void DrawPlayerName(GraphicsDevice GraphicsDevice, string playerName, Vector3 position, Quaternion rotation, bool DepthDraw)
        {
            RenderTarget2D textTexture;
            if(!textures.TryGetValue(playerName, out textTexture)){
                mesh.Effect = new DefaultEffect.Textured();
                const float scale = 1f;
                Vector2 textSize = GTR.MeasureWithGlyphs(Font, playerName, scale, out bool multilineGlyphs);

                Vector2 scalableMiddleSize = textSize + Vector2.One * 8f + Vector2.UnitX * 8f;

                textTexture = new RenderTarget2D(GraphicsDevice, 2*(int)textSize.X, 2*(int)textSize.Y, mipMap: false, GraphicsDevice.PresentationParameters.BackBufferFormat, GraphicsDevice.PresentationParameters.DepthStencilFormat, 0, RenderTargetUsage.PreserveContents);

                if (this.spriteBatch == null) {
                    this.spriteBatch = new SpriteBatch(GraphicsDevice);
                }
                GraphicsDevice.SetRenderTarget(textTexture);
                GraphicsDevice.PrepareDraw();
                GraphicsDevice.Clear(ClearOptions.Target, ColorEx.TransparentWhite, 1f, 0);
                if (Culture.IsCJK)
                {
                    spriteBatch.BeginLinear();
                }
                else
                {
                    spriteBatch.BeginPoint();
                }
                Vector2 vector3 = (Culture.IsCJK ? new Vector2(8f) : Vector2.Zero);
                GTR.DrawString(spriteBatch, Font, playerName, (scalableMiddleSize / 2f - textSize / 2f + vector3).Round(), TextColor, scale);
                spriteBatch.End();
                GraphicsDevice.SetRenderTarget(null);
                mesh.Effect = new DefaultEffect.Textured();
                mesh.SamplerState = Culture.IsCJK ? SamplerState.AnisotropicClamp : SamplerState.PointClamp;
                mesh.Material.Opacity = 1;
                textures.Add(playerName, textTexture);
            }

            mesh.Texture = textTexture;
            //mesh.FirstGroup.TextureMatrix.Set(new Matrix(1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 1f, 1f, 1f, 0f, 0f, 0f, 0f, 0f));
            mesh.Scale = new Vector3(1f);
            mesh.Rotation = rotation;
            mesh.Position = position;
            mesh.DepthWrites = DepthDraw;
            mesh.AlwaysOnTop = true;
            //TODO draw text background
            mesh.Draw();
        }
    }
}
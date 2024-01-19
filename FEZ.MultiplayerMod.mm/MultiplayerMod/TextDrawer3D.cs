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
        private readonly Dictionary<string, Mesh> meshes;
        private readonly GlyphTextRenderer GTR;
        private readonly SpriteFont Font;
        private SpriteBatch spriteBatch;
        private Color TextColor = Color.White;
        private Color BackgroundColor = new Color(0f,0f,0f,0.9f);
        public TextDrawer3D(Game Game, SpriteFont Font)
        {
            meshes = new Dictionary<string, Mesh>();
            GTR = new GlyphTextRenderer(Game);
            this.Font = Font;
        }
        //draws the name to a Texture2D, assign the texture to a Mesh, and draw the Mesh; See SpeechBubble for inspiration 
        internal void DrawPlayerName(GraphicsDevice GraphicsDevice, string playerName, Vector3 position, Quaternion rotation, bool DepthDraw, float fontScale, float renderScale)
        {
            Mesh mesh;
            if(!meshes.TryGetValue(playerName, out mesh)){
                mesh = new Mesh()
                {
                    AlwaysOnTop = true,
                    SamplerState = SamplerState.PointClamp,
                    Blending = BlendingMode.Alphablending
                };
                mesh.AddFace(new Vector3(1f, 1f, 0f), Vector3.Zero, FaceOrientation.Front, centeredOnOrigin: true, doublesided: true);

                mesh.Effect = new DefaultEffect.Textured();
                Vector2 textSize = GTR.MeasureWithGlyphs(Font, playerName, fontScale, out bool multilineGlyphs);

                Vector2 scalableMiddleSize = textSize + Vector2.One * 8f + Vector2.UnitX * 8f;

                RenderTarget2D textTexture = new RenderTarget2D(GraphicsDevice, 2*(int)textSize.X, 2*(int)textSize.Y, mipMap: false, GraphicsDevice.PresentationParameters.BackBufferFormat, GraphicsDevice.PresentationParameters.DepthStencilFormat, 0, RenderTargetUsage.PreserveContents);

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
                GTR.DrawString(spriteBatch, Font, playerName, (scalableMiddleSize / 2f - textSize / 2f + vector3).Round(), TextColor, fontScale);
                spriteBatch.End();
                GraphicsDevice.SetRenderTarget(null);
                mesh.Effect = new DefaultEffect.Textured();
                mesh.SamplerState = Culture.IsCJK ? SamplerState.AnisotropicClamp : SamplerState.PointClamp;
                mesh.Material.Opacity = 1;
                mesh.Texture = textTexture;
                //mesh.FirstGroup.TextureMatrix.Set(new Matrix(1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 1f, 1f, 1f, 0f, 0f, 0f, 0f, 0f));
                mesh.AlwaysOnTop = true;
                meshes.Add(playerName, mesh);
            }
            mesh.Rotation = rotation;
            mesh.Position = position;
            mesh.DepthWrites = DepthDraw;
            mesh.Scale = new Vector3(renderScale, renderScale, 1f);
            //TODO draw text background
            mesh.Draw();
        }
    }
}
using FezEngine;
using FezEngine.Components;
using FezEngine.Effects;
using FezEngine.Structure;
using FezEngine.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

using MeshData = System.Tuple<FezEngine.Structure.Mesh, Microsoft.Xna.Framework.Vector2>;
namespace FezGame.MultiplayerMod
{
    public sealed class TextDrawer3D
    {
        private readonly Dictionary<string, MeshData> meshes;
        private readonly IFontManager FontManager;
        private SpriteBatch spriteBatch;
        private Color TextColor = Color.White;
        private Color BackgroundColor = new Color(0f, 0f, 0f, 0.8f);
        const int padding_top = 0;
        const int padding_bottom = 0;
        const int padding_sides = 16;
        public TextDrawer3D(Game Game, IFontManager FontManager)
        {
            meshes = new Dictionary<string, MeshData>();
            this.FontManager = FontManager;
            //TODO clear meshes when one of the things used to make the mesh textures changes
        }
        /// <summary>
        /// Draws the player name to the screen
        /// </summary>
        /// <param name="GraphicsDevice">The graphics device to use.</param>
        /// <param name="playerName">The text to draw.</param>
        /// <param name="position">Where to draw the text mesh.</param>
        /// <param name="rotation">The rotation of the mesh.</param>
        /// <param name="DepthDraw">If true, the depth of the mesh will be ignored when drawing. This should be false if you want to render something in 3D.</param>
        /// <param name="textScale">The scale at which to generate the mesh texture.</param>
        /// <param name="renderScale">The scale at which to render the name in-game.</param>
        /// <param name="renderScaleY">The scale at which the renderScale will be scaled for the vertical height of the name in-game.</param>
        // draws the name to a Texture2D, assign the texture to a Mesh, and draw the Mesh; See SpeechBubble for inspiration
        internal void DrawPlayerName(GraphicsDevice GraphicsDevice, string playerName, Vector3 position, Quaternion rotation, bool DepthDraw, float textScale, float renderScale, float renderScaleY = 1)
        {
            Mesh mesh;
            Vector2 scalableMiddleSize;
            if (!meshes.TryGetValue(playerName, out MeshData meshData))
            {
                mesh = new Mesh()
                {
                    AlwaysOnTop = true,
                    SamplerState = SamplerState.PointClamp,
                    Blending = BlendingMode.Alphablending
                };
                mesh.AddFace(new Vector3(1f, 1f, 0f), Vector3.Zero, FaceOrientation.Front, centeredOnOrigin: true, doublesided: true);

                mesh.Effect = new DefaultEffect.Textured();
                Vector2 textSize = RichTextRenderer.MeasureString(FontManager, playerName) * textScale;

                scalableMiddleSize = textSize;

                RenderTarget2D textTexture = new RenderTarget2D(GraphicsDevice, (int)textSize.X + padding_sides * 2, (int)textSize.Y + padding_top + padding_bottom, mipMap: false, GraphicsDevice.PresentationParameters.BackBufferFormat, GraphicsDevice.PresentationParameters.DepthStencilFormat, 0, RenderTargetUsage.PreserveContents);

                if (this.spriteBatch == null)
                {
                    this.spriteBatch = new SpriteBatch(GraphicsDevice);
                }
                GraphicsDevice.SetRenderTarget(textTexture);
                GraphicsDevice.PrepareDraw();
                GraphicsDevice.Clear(ClearOptions.Target, BackgroundColor, 1f, 0);
                if (Culture.IsCJK)
                {
                    spriteBatch.BeginLinear();
                }
                else
                {
                    spriteBatch.BeginPoint();
                }
                RichTextRenderer.DrawString(spriteBatch, FontManager, playerName, new Vector2(padding_sides, padding_top), TextColor, Color.Transparent, textScale, 0);
                spriteBatch.End();
                GraphicsDevice.SetRenderTarget(null);
                mesh.Effect = new DefaultEffect.Textured();
                mesh.SamplerState = Culture.IsCJK ? SamplerState.AnisotropicClamp : SamplerState.PointClamp;
                mesh.Material.Opacity = 1;
                mesh.Texture = textTexture;
                mesh.AlwaysOnTop = true;
                scalableMiddleSize /= 16;
                scalableMiddleSize -= Vector2.One;
                meshes.Add(playerName, new MeshData(mesh, scalableMiddleSize));
            }
            else
            {
                mesh = meshData.Item1;
                scalableMiddleSize = meshData.Item2;
            }
            mesh.Rotation = rotation;
            mesh.Position = position;
            mesh.DepthWrites = DepthDraw;
            mesh.Scale = new Vector3(scalableMiddleSize.X * renderScale + 1f, scalableMiddleSize.Y * renderScale * renderScaleY + 1f, 1f);
            mesh.Draw();
        }
        public void ClearMeshes()
        {
            meshes.Clear();
        }
        public bool RemoveMesh(string key)
        {
            return meshes.Remove(key);
        }
    }
}
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
    public class TextDrawer3D
    {
        private readonly Dictionary<string, MeshData> meshes;
        private readonly SpriteFont Font;
        private SpriteBatch spriteBatch;
        private Color TextColor = Color.White;
        private Color BackgroundColor = new Color(0f,0f,0f,0.8f);
        const int padding_top = 0;
        const int padding_bottom = 0;
        const int padding_sides = 16;
        public TextDrawer3D(Game Game, SpriteFont Font)
        {
            meshes = new Dictionary<string, Tuple<Mesh, Vector2>>();
            this.Font = Font;
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
        /// <param name="fontScale">The scale at which to generate the mesh texture.</param>
        /// <param name="renderScale">The scale at which to render the name in-game.</param>
        // draws the name to a Texture2D, assign the texture to a Mesh, and draw the Mesh; See SpeechBubble for inspiration
        internal void DrawPlayerName(GraphicsDevice GraphicsDevice, string playerName, Vector3 position, Quaternion rotation, bool DepthDraw, float fontScale, float renderScale)
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
                Vector2 textSize = Font.MeasureString(playerName) * fontScale;

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
                spriteBatch.DrawString(Font, playerName, (scalableMiddleSize / 2f - textSize / 2f).Round() + new Vector2(padding_sides, padding_top), TextColor, 0f, Vector2.Zero, fontScale, SpriteEffects.None, 0);
                spriteBatch.End();
                GraphicsDevice.SetRenderTarget(null);
                mesh.Effect = new DefaultEffect.Textured();
                mesh.SamplerState = Culture.IsCJK ? SamplerState.AnisotropicClamp : SamplerState.PointClamp;
                mesh.Material.Opacity = 1;
                mesh.Texture = textTexture;
                mesh.AlwaysOnTop = true;
                scalableMiddleSize /= 16;
                scalableMiddleSize -= Vector2.One;
                mesh.Scale = new Vector3(scalableMiddleSize.X + 1f, scalableMiddleSize.Y + 1f, 1f);
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
            mesh.Scale = new Vector3(scalableMiddleSize.X * renderScale + 1f, scalableMiddleSize.Y * renderScale + 1f, 1f);
            mesh.Draw();
        }
        public void ClearMeshes(){
            meshes.Clear();
        }
        public bool RemoveMesh(string key){
            return meshes.Remove(key);
        }
    }
}
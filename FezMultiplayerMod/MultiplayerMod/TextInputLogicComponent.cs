using FezEngine.Components;
using FezEngine.Structure;
using FezEngine.Tools;
using FezGame.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace FezGame.MultiplayerMod
{
    internal class TextInputLogicComponent : GameComponent
    {
        public string Value = "";
        public int MaxLength = 10000;
        /// <summary>
        /// When the value of this textbox has been changed
        /// </summary>
        public event Action OnInput = () => { };
        /// <summary>
        /// If this textbox has focus
        /// </summary>
        public bool HasFocus = false;

        public TextInputLogicComponent(Game game) : base(game)
        {
            //TODO
        }
        /// <summary>
        /// Focuses on this textbox; enabling editing of the value.
        /// </summary>
        public void Focus()
        {
            HasFocus = true;
            //TODO
        }
        public override void Update(GameTime gameTime)
        {
            //TODO
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                //TODO
            }
        }
    }
}
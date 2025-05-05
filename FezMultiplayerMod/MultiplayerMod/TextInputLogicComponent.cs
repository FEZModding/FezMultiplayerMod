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
        public static int TextboxPadRight = 30;
        public string Value = "";
        private bool showCaret = false;
        private const string caret = "|";
        private static readonly string noCaret = $"{RichTextRenderer.C1_8BitCodes.CSI}{RichTextRenderer.SGRParameters.Obfuscate}{RichTextRenderer.CSICommands.SGR}"
                + caret + $"{RichTextRenderer.C1_8BitCodes.CSI}{RichTextRenderer.SGRParameters.NotObfuscated}{RichTextRenderer.CSICommands.SGR}";
        private int caretPosition = 0;
        private double caretBlinksPerSecond = 0.8d;
        public string DisplayValue => HasFocus ? Value.PadRight(TextboxPadRight - caret.Length).Insert(caretPosition, showCaret ? caret : noCaret) : Value.PadRight(TextboxPadRight);

        public int MaxLength = 10000;
        public event Action OnUpdate = () => { };
        /// <summary>
        /// If this textbox has focus
        /// </summary>
        private bool hasFocus = false;
        /// <summary>
        /// If this textbox has focus
        /// </summary>
        public bool HasFocus
        {
            get => hasFocus;
            set
            {
                hasFocus = value;
                showCaret = true;
                OnUpdate();
            }
        }


        public TextInputLogicComponent(Game game) : base(game)
        {
            //TODO
        }
        public override void Update(GameTime gameTime)
        {
            //TODO
            bool newShowCaretValue = ((gameTime.TotalGameTime.TotalSeconds * caretBlinksPerSecond) % 1.0d) > 0.5d;
            if (newShowCaretValue != showCaret)
            {
                showCaret = newShowCaretValue;
                OnUpdate();
            }
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
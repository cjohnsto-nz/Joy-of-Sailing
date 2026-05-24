using System;
using System.Globalization;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace joyofsailing
{
    public class GuiDialogSailboatDebug : GuiDialog
    {
        const string ComposerKey = "joyofsailing-ratline-debug";

        public override string ToggleKeyCombinationCode => "joyofsailingratlinedebug";

        public GuiDialogSailboatDebug(ICoreClientAPI capi) : base(capi)
        {
            Compose();
        }

        private void Compose()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fixed(0, 0, 420, 420);
            ElementBounds insetBounds = bgBounds.ForkBoundingParent(GuiStyle.ElementToDialogPadding);

            SingleComposer = capi.Gui.CreateCompo(ComposerKey, dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar("Sailboat Ratline Debug", () => TryClose())
                .BeginChildElements(insetBounds)
                    .AddStaticText("Draw path", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 42, 160, 24))
                    .AddSwitch(on => RatlineClimbDebugSettings.DrawPath = on, ElementBounds.Fixed(210, 38, 30, 30), "drawPath")
                    .AddStaticText("Speed", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 80, 160, 24))
                    .AddNumberInput(ElementBounds.Fixed(210, 74, 90, 28), value => SetFloat(value, v => RatlineClimbDebugSettings.Speed = GameMath.Max(0f, v)), CairoFont.TextInput(), "speed")
                    .AddStaticText("Path X", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 118, 160, 24))
                    .AddNumberInput(ElementBounds.Fixed(210, 112, 90, 28), value => SetFloat(value, v => RatlineClimbDebugSettings.PathX = v), CairoFont.TextInput(), "pathX")
                    .AddStaticText("Left Z", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 156, 160, 24))
                    .AddNumberInput(ElementBounds.Fixed(210, 150, 90, 28), value => SetFloat(value, v => RatlineClimbDebugSettings.LeftPathZ = v), CairoFont.TextInput(), "leftZ")
                    .AddStaticText("Right Z", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 194, 160, 24))
                    .AddNumberInput(ElementBounds.Fixed(210, 188, 90, 28), value => SetFloat(value, v => RatlineClimbDebugSettings.RightPathZ = v), CairoFont.TextInput(), "rightZ")
                    .AddStaticText("Start Y", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 232, 160, 24))
                    .AddNumberInput(ElementBounds.Fixed(210, 226, 90, 28), value => SetFloat(value, v => RatlineClimbDebugSettings.StartY = v), CairoFont.TextInput(), "startY")
                    .AddStaticText("End Y", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 270, 160, 24))
                    .AddNumberInput(ElementBounds.Fixed(210, 264, 90, 28), value => SetFloat(value, v => RatlineClimbDebugSettings.EndY = v), CairoFont.TextInput(), "endY")
                    .AddStaticText("Tilt", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 308, 160, 24))
                    .AddNumberInput(ElementBounds.Fixed(210, 302, 90, 28), value => SetFloat(value, v => RatlineClimbDebugSettings.TiltDegrees = v), CairoFont.TextInput(), "tilt")
                    .AddStaticText("Lean", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 346, 160, 24))
                    .AddNumberInput(ElementBounds.Fixed(210, 340, 90, 28), value => SetFloat(value, v => RatlineClimbDebugSettings.LeanDegrees = v), CairoFont.TextInput(), "lean")
                    .AddButton("Reset", OnReset, ElementBounds.Fixed(0, 380, 120, 32))
                .EndChildElements()
                .Compose();

            SyncInputs();
        }

        private bool OnReset()
        {
            RatlineClimbDebugSettings.ResetRuntimeToDefaults();
            SyncInputs();
            return true;
        }

        private void SyncInputs()
        {
            SingleComposer.GetSwitch("drawPath").SetValue(RatlineClimbDebugSettings.DrawPath);
            SetInput("speed", RatlineClimbDebugSettings.Speed);
            SetInput("pathX", RatlineClimbDebugSettings.PathX);
            SetInput("leftZ", RatlineClimbDebugSettings.LeftPathZ);
            SetInput("rightZ", RatlineClimbDebugSettings.RightPathZ);
            SetInput("startY", RatlineClimbDebugSettings.StartY);
            SetInput("endY", RatlineClimbDebugSettings.EndY);
            SetInput("tilt", RatlineClimbDebugSettings.TiltDegrees);
            SetInput("lean", RatlineClimbDebugSettings.LeanDegrees);
        }

        private void SetInput(string key, float value)
        {
            SingleComposer.GetTextInput(key).SetValue(value.ToString("0.###", CultureInfo.InvariantCulture));
        }

        private void SetFloat(string value, Action<float> setter)
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                setter(parsed);
            }
        }
    }
}

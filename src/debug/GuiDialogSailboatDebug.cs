using System;
using System.Globalization;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace joyofsailing
{
    public class GuiDialogSailboatDebug : GuiDialog
    {
        const string ComposerKey = "joyofsailing-ratline-debug";
        const double RowHeight = 32.0;
        const double LabelX = 0.0;
        const double InputX = 154.0;
        const double LabelWidth = 135.0;
        const double InputWidth = 96.0;

        public override string ToggleKeyCombinationCode => "joyofsailingratlinedebug";

        public GuiDialogSailboatDebug(ICoreClientAPI capi) : base(capi)
        {
            Compose();
        }

        private void Compose()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            SingleComposer = capi.Gui.CreateCompo(ComposerKey, dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar("Sailboat Ratline Debug", () => TryClose())
                .BeginChildElements(bgBounds)
                    .AddStaticText("Draw path", CairoFont.WhiteSmallText(), LabelBounds(0))
                    .AddSwitch(on => RatlineClimbDebugSettings.DrawPath = on, ElementBounds.Fixed(InputX, RowY(0) - 4, 30, 30), "drawPath")
                    .AddStaticText("Speed", CairoFont.WhiteSmallText(), LabelBounds(1))
                    .AddNumberInput(InputBounds(1), value => SetFloat(value, v => RatlineClimbDebugSettings.Speed = GameMath.Max(0f, v)), CairoFont.TextInput(), "speed")
                    .AddStaticText("Path X", CairoFont.WhiteSmallText(), LabelBounds(2))
                    .AddNumberInput(InputBounds(2), value => SetFloat(value, v => RatlineClimbDebugSettings.PathX = v), CairoFont.TextInput(), "pathX")
                    .AddStaticText("Left Z", CairoFont.WhiteSmallText(), LabelBounds(3))
                    .AddNumberInput(InputBounds(3), value => SetFloat(value, v => RatlineClimbDebugSettings.LeftPathZ = v), CairoFont.TextInput(), "leftZ")
                    .AddStaticText("Right Z", CairoFont.WhiteSmallText(), LabelBounds(4))
                    .AddNumberInput(InputBounds(4), value => SetFloat(value, v => RatlineClimbDebugSettings.RightPathZ = v), CairoFont.TextInput(), "rightZ")
                    .AddStaticText("Start Y", CairoFont.WhiteSmallText(), LabelBounds(5))
                    .AddNumberInput(InputBounds(5), value => SetFloat(value, v => RatlineClimbDebugSettings.StartY = v), CairoFont.TextInput(), "startY")
                    .AddStaticText("End Y", CairoFont.WhiteSmallText(), LabelBounds(6))
                    .AddNumberInput(InputBounds(6), value => SetFloat(value, v => RatlineClimbDebugSettings.EndY = v), CairoFont.TextInput(), "endY")
                    .AddStaticText("Path Tilt", CairoFont.WhiteSmallText(), LabelBounds(7))
                    .AddNumberInput(InputBounds(7), value => SetFloat(value, v => RatlineClimbDebugSettings.TiltDegrees = v), CairoFont.TextInput(), "tilt")
                    .AddStaticText("Path Lean", CairoFont.WhiteSmallText(), LabelBounds(8))
                    .AddNumberInput(InputBounds(8), value => SetFloat(value, v => RatlineClimbDebugSettings.LeanDegrees = v), CairoFont.TextInput(), "lean")
                    .AddStaticText("Player Rot", CairoFont.WhiteSmallText(), LabelBounds(9))
                    .AddNumberInput(InputBounds(9), value => SetFloat(value, v => RatlineClimbDebugSettings.PlayerRotationDegrees = v), CairoFont.TextInput(), "playerRotation")
                    .AddStaticText("Left Tilt", CairoFont.WhiteSmallText(), LabelBounds(10))
                    .AddNumberInput(InputBounds(10), value => SetFloat(value, v => RatlineClimbDebugSettings.LeftPlayerTiltDegrees = v), CairoFont.TextInput(), "leftPlayerTilt")
                    .AddStaticText("Right Tilt", CairoFont.WhiteSmallText(), LabelBounds(11))
                    .AddNumberInput(InputBounds(11), value => SetFloat(value, v => RatlineClimbDebugSettings.RightPlayerTiltDegrees = v), CairoFont.TextInput(), "rightPlayerTilt")
                    .AddStaticText("Left Lean", CairoFont.WhiteSmallText(), LabelBounds(12))
                    .AddNumberInput(InputBounds(12), value => SetFloat(value, v => RatlineClimbDebugSettings.LeftPlayerLeanDegrees = v), CairoFont.TextInput(), "leftPlayerLean")
                    .AddStaticText("Right Lean", CairoFont.WhiteSmallText(), LabelBounds(13))
                    .AddNumberInput(InputBounds(13), value => SetFloat(value, v => RatlineClimbDebugSettings.RightPlayerLeanDegrees = v), CairoFont.TextInput(), "rightPlayerLean")
                    .AddButton("Reset", OnReset, ElementBounds.Fixed(LabelX, RowY(14) + 6, 112, 32))
                .EndChildElements()
                .Compose();

            SyncInputs();
        }

        private static double RowY(int row)
        {
            return 42.0 + row * RowHeight;
        }

        private static ElementBounds LabelBounds(int row)
        {
            return ElementBounds.Fixed(LabelX, RowY(row), LabelWidth, 24);
        }

        private static ElementBounds InputBounds(int row)
        {
            return ElementBounds.Fixed(InputX, RowY(row) - 6, InputWidth, 28);
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
            SetInput("playerRotation", RatlineClimbDebugSettings.PlayerRotationDegrees);
            SetInput("leftPlayerTilt", RatlineClimbDebugSettings.LeftPlayerTiltDegrees);
            SetInput("rightPlayerTilt", RatlineClimbDebugSettings.RightPlayerTiltDegrees);
            SetInput("leftPlayerLean", RatlineClimbDebugSettings.LeftPlayerLeanDegrees);
            SetInput("rightPlayerLean", RatlineClimbDebugSettings.RightPlayerLeanDegrees);
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

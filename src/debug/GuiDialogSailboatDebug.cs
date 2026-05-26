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
        const double ToggleLabelX = 282.0;
        const double ToggleInputX = 456.0;
        const double ToggleLabelWidth = 158.0;
        const double BoatLabelX = 520.0;
        const double BoatInputX = 676.0;
        const double BoatLabelWidth = 140.0;
        const double BoatInputWidth = 86.0;
        private readonly GuiDialogSailboatTransformDebug transformDebugDialog;

        public override string ToggleKeyCombinationCode => "joyofsailingratlinedebug";

        public GuiDialogSailboatDebug(ICoreClientAPI capi, GuiDialogSailboatTransformDebug transformDebugDialog) : base(capi)
        {
            this.transformDebugDialog = transformDebugDialog;
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
                    .AddStaticText("Center Z", CairoFont.WhiteSmallText(), LabelBounds(3))
                    .AddNumberInput(InputBounds(3), value => SetFloat(value, v => RatlineClimbDebugSettings.PathCenterZ = v), CairoFont.TextInput(), "centerZ")
                    .AddStaticText("Z Offset", CairoFont.WhiteSmallText(), LabelBounds(4))
                    .AddNumberInput(InputBounds(4), value => SetFloat(value, v => RatlineClimbDebugSettings.PathZOffset = GameMath.Max(0f, v)), CairoFont.TextInput(), "zOffset")
                    .AddStaticText("Start Y", CairoFont.WhiteSmallText(), LabelBounds(5))
                    .AddNumberInput(InputBounds(5), value => SetFloat(value, v => RatlineClimbDebugSettings.StartY = v), CairoFont.TextInput(), "startY")
                    .AddStaticText("End Y", CairoFont.WhiteSmallText(), LabelBounds(6))
                    .AddNumberInput(InputBounds(6), value => SetFloat(value, v => RatlineClimbDebugSettings.EndY = v), CairoFont.TextInput(), "endY")
                    .AddStaticText("Tri Height", CairoFont.WhiteSmallText(), LabelBounds(7))
                    .AddNumberInput(InputBounds(7), value => SetFloat(value, v => RatlineClimbDebugSettings.TriangleHeight = GameMath.Max(0f, v)), CairoFont.TextInput(), "triangleHeight")
                    .AddStaticText("Path Tilt", CairoFont.WhiteSmallText(), LabelBounds(8))
                    .AddNumberInput(InputBounds(8), value => SetFloat(value, v => RatlineClimbDebugSettings.TiltDegrees = v), CairoFont.TextInput(), "tilt")
                    .AddStaticText("Path Lean", CairoFont.WhiteSmallText(), LabelBounds(9))
                    .AddNumberInput(InputBounds(9), value => SetFloat(value, v => RatlineClimbDebugSettings.LeanDegrees = v), CairoFont.TextInput(), "lean")
                    .AddStaticText("Player Rot", CairoFont.WhiteSmallText(), LabelBounds(10))
                    .AddNumberInput(InputBounds(10), value => SetFloat(value, v => RatlineClimbDebugSettings.PlayerRotationDegrees = v), CairoFont.TextInput(), "playerRotation")
                    .AddStaticText("Right Rot Off", CairoFont.WhiteSmallText(), LabelBounds(11))
                    .AddNumberInput(InputBounds(11), value => SetFloat(value, v => RatlineClimbDebugSettings.RightPlayerRotationOffsetDegrees = v), CairoFont.TextInput(), "rightPlayerRotationOffset")
                    .AddStaticText("Left Tilt", CairoFont.WhiteSmallText(), LabelBounds(12))
                    .AddNumberInput(InputBounds(12), value => SetFloat(value, v => RatlineClimbDebugSettings.LeftPlayerTiltDegrees = v), CairoFont.TextInput(), "leftPlayerTilt")
                    .AddStaticText("Right Tilt", CairoFont.WhiteSmallText(), LabelBounds(13))
                    .AddNumberInput(InputBounds(13), value => SetFloat(value, v => RatlineClimbDebugSettings.RightPlayerTiltDegrees = v), CairoFont.TextInput(), "rightPlayerTilt")
                    .AddStaticText("Left Lean", CairoFont.WhiteSmallText(), LabelBounds(14))
                    .AddNumberInput(InputBounds(14), value => SetFloat(value, v => RatlineClimbDebugSettings.LeftPlayerLeanDegrees = v), CairoFont.TextInput(), "leftPlayerLean")
                    .AddStaticText("Right Lean", CairoFont.WhiteSmallText(), LabelBounds(15))
                    .AddNumberInput(InputBounds(15), value => SetFloat(value, v => RatlineClimbDebugSettings.RightPlayerLeanDegrees = v), CairoFont.TextInput(), "rightPlayerLean")
                    .AddStaticText("Player X", CairoFont.WhiteSmallText(), LabelBounds(16))
                    .AddNumberInput(InputBounds(16), value => SetFloat(value, v => RatlineClimbDebugSettings.PlayerOffsetX = v), CairoFont.TextInput(), "playerOffsetX")
                    .AddStaticText("Player Y", CairoFont.WhiteSmallText(), LabelBounds(17))
                    .AddNumberInput(InputBounds(17), value => SetFloat(value, v => RatlineClimbDebugSettings.PlayerOffsetY = v), CairoFont.TextInput(), "playerOffsetY")
                    .AddStaticText("Player Z", CairoFont.WhiteSmallText(), LabelBounds(18))
                    .AddNumberInput(InputBounds(18), value => SetFloat(value, v => RatlineClimbDebugSettings.PlayerOffsetZ = v), CairoFont.TextInput(), "playerOffsetZ")
                    .AddStaticText("Mast X", CairoFont.WhiteSmallText(), LabelBounds(19))
                    .AddNumberInput(InputBounds(19), value => SetFloat(value, v => RatlineClimbDebugSettings.MastDebugXOffset = v), CairoFont.TextInput(), "mastX")
                    .AddStaticText("Invert X", CairoFont.WhiteSmallText(), LabelBounds(20))
                    .AddSwitch(on => RatlineClimbDebugSettings.InvertSwayX = on, ElementBounds.Fixed(InputX, RowY(20) - 4, 30, 30), "invertSwayX")
                    .AddStaticText("Invert Y", CairoFont.WhiteSmallText(), LabelBounds(21))
                    .AddSwitch(on => RatlineClimbDebugSettings.InvertSwayY = on, ElementBounds.Fixed(InputX, RowY(21) - 4, 30, 30), "invertSwayY")
                    .AddStaticText("Invert Z", CairoFont.WhiteSmallText(), LabelBounds(22))
                    .AddSwitch(on => RatlineClimbDebugSettings.InvertSwayZ = on, ElementBounds.Fixed(InputX, RowY(22) - 4, 30, 30), "invertSwayZ")
                    .AddStaticText("Invert Swivel", CairoFont.WhiteSmallText(), LabelBounds(23))
                    .AddSwitch(on => RatlineClimbDebugSettings.InvertSwaySwivel = on, ElementBounds.Fixed(InputX, RowY(23) - 4, 30, 30), "invertSwaySwivel")
                    .AddButton("Reset", OnReset, ElementBounds.Fixed(LabelX, RowY(25) + 6, 112, 32))
                    .AddButton("Values", OnValues, ElementBounds.Fixed(LabelX + 126, RowY(25) + 6, 112, 32))
                    .AddStaticText("Use Player Offset", CairoFont.WhiteSmallText(), ToggleLabelBounds(0))
                    .AddSwitch(on => RatlineClimbDebugSettings.EnablePlayerOffset = on, ToggleSwitchBounds(0), "enablePlayerOffset")
                    .AddStaticText("Use Path Tilt", CairoFont.WhiteSmallText(), ToggleLabelBounds(1))
                    .AddSwitch(on => RatlineClimbDebugSettings.EnablePathTilt = on, ToggleSwitchBounds(1), "enablePathTilt")
                    .AddStaticText("Use Path Lean", CairoFont.WhiteSmallText(), ToggleLabelBounds(2))
                    .AddSwitch(on => RatlineClimbDebugSettings.EnablePathLean = on, ToggleSwitchBounds(2), "enablePathLean")
                    .AddStaticText("Use Model Tilt", CairoFont.WhiteSmallText(), ToggleLabelBounds(3))
                    .AddSwitch(on => RatlineClimbDebugSettings.EnablePlayerTilt = on, ToggleSwitchBounds(3), "enablePlayerTilt")
                    .AddStaticText("Use Model Yaw", CairoFont.WhiteSmallText(), ToggleLabelBounds(4))
                    .AddSwitch(on => RatlineClimbDebugSettings.EnablePlayerYaw = on, ToggleSwitchBounds(4), "enablePlayerYaw")
                    .AddStaticText("Use Model Lean", CairoFont.WhiteSmallText(), ToggleLabelBounds(5))
                    .AddSwitch(on => RatlineClimbDebugSettings.EnablePlayerLean = on, ToggleSwitchBounds(5), "enablePlayerLean")
                    .AddStaticText("Ratline Steer", CairoFont.WhiteSmallText(), BoatLabelBounds(0))
                    .AddSwitch(on => RatlineClimbDebugSettings.EnableRatlineSteering = on, BoatSwitchBounds(0), "enableRatlineSteering")
                    .AddStaticText("Steer Mul", CairoFont.WhiteSmallText(), BoatLabelBounds(1))
                    .AddNumberInput(BoatInputBounds(1), value => SetFloat(value, v => RatlineClimbDebugSettings.RatlineSteeringMultiplier = GameMath.Max(0f, v)), CairoFont.TextInput(), "ratlineSteeringMultiplier")
                    .AddStaticText("Override Sway", CairoFont.WhiteSmallText(), BoatLabelBounds(2))
                    .AddSwitch(on => RatlineClimbDebugSettings.OverrideBoatSway = on, BoatSwitchBounds(2), "overrideBoatSway")
                    .AddStaticText("Sway X", CairoFont.WhiteSmallText(), BoatLabelBounds(3))
                    .AddNumberInput(BoatInputBounds(3), value => SetFloat(value, v => RatlineClimbDebugSettings.BoatSwayXDegrees = v), CairoFont.TextInput(), "boatSwayX")
                    .AddStaticText("Sway Y", CairoFont.WhiteSmallText(), BoatLabelBounds(4))
                    .AddNumberInput(BoatInputBounds(4), value => SetFloat(value, v => RatlineClimbDebugSettings.BoatSwayYDegrees = v), CairoFont.TextInput(), "boatSwayY")
                    .AddStaticText("Sway Z", CairoFont.WhiteSmallText(), BoatLabelBounds(5))
                    .AddNumberInput(BoatInputBounds(5), value => SetFloat(value, v => RatlineClimbDebugSettings.BoatSwayZDegrees = v), CairoFont.TextInput(), "boatSwayZ")
                    .AddStaticText("Swivel", CairoFont.WhiteSmallText(), BoatLabelBounds(6))
                    .AddNumberInput(BoatInputBounds(6), value => SetFloat(value, v => RatlineClimbDebugSettings.BoatSwivelDegrees = v), CairoFont.TextInput(), "boatSwivel")
                    .AddStaticText("Override Yaw", CairoFont.WhiteSmallText(), BoatLabelBounds(7))
                    .AddSwitch(on => RatlineClimbDebugSettings.OverrideBoatYaw = on, BoatSwitchBounds(7), "overrideBoatYaw")
                    .AddStaticText("Yaw Deg", CairoFont.WhiteSmallText(), BoatLabelBounds(8))
                    .AddNumberInput(BoatInputBounds(8), value => SetFloat(value, v => RatlineClimbDebugSettings.BoatYawDegrees = v), CairoFont.TextInput(), "boatYaw")
                    .AddStaticText("Model X", CairoFont.WhiteSmallText(), BoatLabelBounds(9))
                    .AddNumberInput(BoatInputBounds(9), value => SetFloat(value, v => RatlineClimbDebugSettings.DebugModelOffsetX = v), CairoFont.TextInput(), "debugModelOffsetX")
                    .AddStaticText("Model Y", CairoFont.WhiteSmallText(), BoatLabelBounds(10))
                    .AddNumberInput(BoatInputBounds(10), value => SetFloat(value, v => RatlineClimbDebugSettings.DebugModelOffsetY = v), CairoFont.TextInput(), "debugModelOffsetY")
                    .AddStaticText("Model Z", CairoFont.WhiteSmallText(), BoatLabelBounds(11))
                    .AddNumberInput(BoatInputBounds(11), value => SetFloat(value, v => RatlineClimbDebugSettings.DebugModelOffsetZ = v), CairoFont.TextInput(), "debugModelOffsetZ")
                    .AddStaticText("Model Roll", CairoFont.WhiteSmallText(), BoatLabelBounds(12))
                    .AddNumberInput(BoatInputBounds(12), value => SetFloat(value, v => RatlineClimbDebugSettings.DebugModelRollDegrees = v), CairoFont.TextInput(), "debugModelRoll")
                    .AddStaticText("Model Yaw", CairoFont.WhiteSmallText(), BoatLabelBounds(13))
                    .AddNumberInput(BoatInputBounds(13), value => SetFloat(value, v => RatlineClimbDebugSettings.DebugModelYawDegrees = v), CairoFont.TextInput(), "debugModelYaw")
                    .AddStaticText("Model Pitch", CairoFont.WhiteSmallText(), BoatLabelBounds(14))
                    .AddNumberInput(BoatInputBounds(14), value => SetFloat(value, v => RatlineClimbDebugSettings.DebugModelPitchDegrees = v), CairoFont.TextInput(), "debugModelPitch")
                    .AddStaticText("Body Yaw", CairoFont.WhiteSmallText(), BoatLabelBounds(15))
                    .AddNumberInput(BoatInputBounds(15), value => SetFloat(value, v => RatlineClimbDebugSettings.RatlineBodyYawLimitDegrees = GameMath.Max(0f, v)), CairoFont.TextInput(), "ratlineBodyYawLimit")
                    .AddStaticText("Camera Yaw", CairoFont.WhiteSmallText(), BoatLabelBounds(16))
                    .AddNumberInput(BoatInputBounds(16), value => SetFloat(value, v => RatlineClimbDebugSettings.RatlineCameraYawLimitDegrees = GameMath.Max(0f, v)), CairoFont.TextInput(), "ratlineCameraYawLimit")
                    .AddStaticText("Right Cam Off", CairoFont.WhiteSmallText(), BoatLabelBounds(17))
                    .AddNumberInput(BoatInputBounds(17), value => SetFloat(value, v => RatlineClimbDebugSettings.RightRatlineCameraYawOffsetDegrees = v), CairoFont.TextInput(), "rightRatlineCameraYawOffset")
                    .AddStaticText("Left Cam Off", CairoFont.WhiteSmallText(), BoatLabelBounds(18))
                    .AddNumberInput(BoatInputBounds(18), value => SetFloat(value, v => RatlineClimbDebugSettings.LeftRatlineCameraYawOffsetDegrees = v), CairoFont.TextInput(), "leftRatlineCameraYawOffset")
                    .AddStaticText("Eye X", CairoFont.WhiteSmallText(), BoatLabelBounds(19))
                    .AddNumberInput(BoatInputBounds(19), value => SetFloat(value, v => RatlineClimbDebugSettings.DebugEyeOffsetX = v), CairoFont.TextInput(), "debugEyeOffsetX")
                    .AddStaticText("Eye Y", CairoFont.WhiteSmallText(), BoatLabelBounds(20))
                    .AddNumberInput(BoatInputBounds(20), value => SetFloat(value, v => RatlineClimbDebugSettings.DebugEyeOffsetY = v), CairoFont.TextInput(), "debugEyeOffsetY")
                    .AddStaticText("Eye Z", CairoFont.WhiteSmallText(), BoatLabelBounds(21))
                    .AddNumberInput(BoatInputBounds(21), value => SetFloat(value, v => RatlineClimbDebugSettings.DebugEyeOffsetZ = v), CairoFont.TextInput(), "debugEyeOffsetZ")
                .EndChildElements()
                .Compose();

            ConfigureInputIntervals();
            SyncInputs();
        }

        private void ConfigureInputIntervals()
        {
            SetInputInterval("playerOffsetX", 0.1f);
            SetInputInterval("playerOffsetY", 0.1f);
            SetInputInterval("playerOffsetZ", 0.1f);
            SetInputInterval("ratlineSteeringMultiplier", 0.1f);
            SetInputInterval("boatSwayX", 1f);
            SetInputInterval("boatSwayY", 1f);
            SetInputInterval("boatSwayZ", 1f);
            SetInputInterval("boatSwivel", 1f);
            SetInputInterval("boatYaw", 5f);
            SetInputInterval("debugModelOffsetX", 0.1f);
            SetInputInterval("debugModelOffsetY", 0.1f);
            SetInputInterval("debugModelOffsetZ", 0.1f);
            SetInputInterval("debugModelRoll", 1f);
            SetInputInterval("debugModelYaw", 1f);
            SetInputInterval("debugModelPitch", 1f);
            SetInputInterval("ratlineBodyYawLimit", 5f);
            SetInputInterval("ratlineCameraYawLimit", 5f);
            SetInputInterval("rightRatlineCameraYawOffset", 5f);
            SetInputInterval("leftRatlineCameraYawOffset", 5f);
            SetInputInterval("debugEyeOffsetX", 0.1f);
            SetInputInterval("debugEyeOffsetY", 0.1f);
            SetInputInterval("debugEyeOffsetZ", 0.1f);
        }

        private void SetInputInterval(string key, float interval)
        {
            SingleComposer.GetNumberInput(key).Interval = interval;
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

        private static ElementBounds ToggleLabelBounds(int row)
        {
            return ElementBounds.Fixed(ToggleLabelX, RowY(row), ToggleLabelWidth, 24);
        }

        private static ElementBounds ToggleSwitchBounds(int row)
        {
            return ElementBounds.Fixed(ToggleInputX, RowY(row) - 4, 30, 30);
        }

        private static ElementBounds BoatLabelBounds(int row)
        {
            return ElementBounds.Fixed(BoatLabelX, RowY(row), BoatLabelWidth, 24);
        }

        private static ElementBounds BoatInputBounds(int row)
        {
            return ElementBounds.Fixed(BoatInputX, RowY(row) - 6, BoatInputWidth, 28);
        }

        private static ElementBounds BoatSwitchBounds(int row)
        {
            return ElementBounds.Fixed(BoatInputX, RowY(row) - 4, 30, 30);
        }

        private bool OnReset()
        {
            RatlineClimbDebugSettings.ResetRuntimeToDefaults();
            SyncInputs();
            return true;
        }

        private bool OnValues()
        {
            if (transformDebugDialog == null)
            {
                return true;
            }

            if (transformDebugDialog.IsOpened())
            {
                transformDebugDialog.TryClose();
            }
            else
            {
                transformDebugDialog.TryOpen();
            }

            return true;
        }

        private void SyncInputs()
        {
            SingleComposer.GetSwitch("drawPath").SetValue(RatlineClimbDebugSettings.DrawPath);
            SetInput("speed", RatlineClimbDebugSettings.Speed);
            SetInput("pathX", RatlineClimbDebugSettings.PathX);
            SetInput("centerZ", RatlineClimbDebugSettings.PathCenterZ);
            SetInput("zOffset", RatlineClimbDebugSettings.PathZOffset);
            SetInput("startY", RatlineClimbDebugSettings.StartY);
            SetInput("endY", RatlineClimbDebugSettings.EndY);
            SetInput("triangleHeight", RatlineClimbDebugSettings.TriangleHeight);
            SetInput("tilt", RatlineClimbDebugSettings.TiltDegrees);
            SetInput("lean", RatlineClimbDebugSettings.LeanDegrees);
            SetInput("playerRotation", RatlineClimbDebugSettings.PlayerRotationDegrees);
            SetInput("rightPlayerRotationOffset", RatlineClimbDebugSettings.RightPlayerRotationOffsetDegrees);
            SetInput("leftPlayerTilt", RatlineClimbDebugSettings.LeftPlayerTiltDegrees);
            SetInput("rightPlayerTilt", RatlineClimbDebugSettings.RightPlayerTiltDegrees);
            SetInput("leftPlayerLean", RatlineClimbDebugSettings.LeftPlayerLeanDegrees);
            SetInput("rightPlayerLean", RatlineClimbDebugSettings.RightPlayerLeanDegrees);
            SetInput("playerOffsetX", RatlineClimbDebugSettings.PlayerOffsetX);
            SetInput("playerOffsetY", RatlineClimbDebugSettings.PlayerOffsetY);
            SetInput("playerOffsetZ", RatlineClimbDebugSettings.PlayerOffsetZ);
            SetInput("mastX", RatlineClimbDebugSettings.MastDebugXOffset);
            SingleComposer.GetSwitch("invertSwayX").SetValue(RatlineClimbDebugSettings.InvertSwayX);
            SingleComposer.GetSwitch("invertSwayY").SetValue(RatlineClimbDebugSettings.InvertSwayY);
            SingleComposer.GetSwitch("invertSwayZ").SetValue(RatlineClimbDebugSettings.InvertSwayZ);
            SingleComposer.GetSwitch("invertSwaySwivel").SetValue(RatlineClimbDebugSettings.InvertSwaySwivel);
            SingleComposer.GetSwitch("enablePlayerOffset").SetValue(RatlineClimbDebugSettings.EnablePlayerOffset);
            SingleComposer.GetSwitch("enablePathTilt").SetValue(RatlineClimbDebugSettings.EnablePathTilt);
            SingleComposer.GetSwitch("enablePathLean").SetValue(RatlineClimbDebugSettings.EnablePathLean);
            SingleComposer.GetSwitch("enablePlayerTilt").SetValue(RatlineClimbDebugSettings.EnablePlayerTilt);
            SingleComposer.GetSwitch("enablePlayerYaw").SetValue(RatlineClimbDebugSettings.EnablePlayerYaw);
            SingleComposer.GetSwitch("enablePlayerLean").SetValue(RatlineClimbDebugSettings.EnablePlayerLean);
            SingleComposer.GetSwitch("enableRatlineSteering").SetValue(RatlineClimbDebugSettings.EnableRatlineSteering);
            SetInput("ratlineSteeringMultiplier", RatlineClimbDebugSettings.RatlineSteeringMultiplier);
            SingleComposer.GetSwitch("overrideBoatSway").SetValue(RatlineClimbDebugSettings.OverrideBoatSway);
            SetInput("boatSwayX", RatlineClimbDebugSettings.BoatSwayXDegrees);
            SetInput("boatSwayY", RatlineClimbDebugSettings.BoatSwayYDegrees);
            SetInput("boatSwayZ", RatlineClimbDebugSettings.BoatSwayZDegrees);
            SetInput("boatSwivel", RatlineClimbDebugSettings.BoatSwivelDegrees);
            SingleComposer.GetSwitch("overrideBoatYaw").SetValue(RatlineClimbDebugSettings.OverrideBoatYaw);
            SetInput("boatYaw", RatlineClimbDebugSettings.BoatYawDegrees);
            SetInput("debugModelOffsetX", RatlineClimbDebugSettings.DebugModelOffsetX);
            SetInput("debugModelOffsetY", RatlineClimbDebugSettings.DebugModelOffsetY);
            SetInput("debugModelOffsetZ", RatlineClimbDebugSettings.DebugModelOffsetZ);
            SetInput("debugModelRoll", RatlineClimbDebugSettings.DebugModelRollDegrees);
            SetInput("debugModelYaw", RatlineClimbDebugSettings.DebugModelYawDegrees);
            SetInput("debugModelPitch", RatlineClimbDebugSettings.DebugModelPitchDegrees);
            SetInput("ratlineBodyYawLimit", RatlineClimbDebugSettings.RatlineBodyYawLimitDegrees);
            SetInput("ratlineCameraYawLimit", RatlineClimbDebugSettings.RatlineCameraYawLimitDegrees);
            SetInput("rightRatlineCameraYawOffset", RatlineClimbDebugSettings.RightRatlineCameraYawOffsetDegrees);
            SetInput("leftRatlineCameraYawOffset", RatlineClimbDebugSettings.LeftRatlineCameraYawOffsetDegrees);
            SetInput("debugEyeOffsetX", RatlineClimbDebugSettings.DebugEyeOffsetX);
            SetInput("debugEyeOffsetY", RatlineClimbDebugSettings.DebugEyeOffsetY);
            SetInput("debugEyeOffsetZ", RatlineClimbDebugSettings.DebugEyeOffsetZ);
        }

        private void SetInput(string key, float value)
        {
            SingleComposer.GetTextInput(key).SetValue(value.ToString("0.###", CultureInfo.InvariantCulture));
        }

        private void SetInput(string key, int value)
        {
            SingleComposer.GetTextInput(key).SetValue(value.ToString(CultureInfo.InvariantCulture));
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

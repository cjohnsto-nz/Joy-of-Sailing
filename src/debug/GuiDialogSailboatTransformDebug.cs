using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace joyofsailing
{
    public class GuiDialogSailboatTransformDebug : GuiDialog
    {
        const string ComposerKey = "joyofsailing-player-transform-debug";
        private GuiElementDynamicText leftText;
        private GuiElementDynamicText rightText;
        private readonly long tickListenerId;

        public override string ToggleKeyCombinationCode => null;

        public GuiDialogSailboatTransformDebug(ICoreClientAPI capi) : base(capi)
        {
            Compose();
            tickListenerId = capi.Event.RegisterGameTickListener(OnGameTick, 100);
        }

        private void Compose()
        {
            ElementBounds leftTextBounds = ElementBounds.Fixed(0, 34, 390, 700);
            ElementBounds rightTextBounds = ElementBounds.Fixed(410, 34, 430, 700);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(leftTextBounds, rightTextBounds);

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.RightMiddle)
                .WithFixedAlignmentOffset(-20, 0);

            SingleComposer = capi.Gui.CreateCompo(ComposerKey, dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar("Sailboat Player Transform Debug", () => TryClose())
                .BeginChildElements(bgBounds)
                    .AddDynamicText("", CairoFont.WhiteDetailText(), leftTextBounds, "left")
                    .AddDynamicText("", CairoFont.WhiteDetailText(), rightTextBounds, "right")
                .EndChildElements()
                .Compose();

            leftText = SingleComposer.GetDynamicText("left");
            rightText = SingleComposer.GetDynamicText("right");
            UpdateDebugText();
        }

        private void OnGameTick(float dt)
        {
            if (!IsOpened())
            {
                return;
            }

            UpdateDebugText();
        }

        public override bool TryOpen()
        {
            bool opened = base.TryOpen();
            if (opened)
            {
                UpdateDebugText();
            }

            return opened;
        }

        private void UpdateDebugText()
        {
            if (leftText == null || rightText == null)
            {
                return;
            }

            StringBuilder left = new StringBuilder();
            StringBuilder right = new StringBuilder();

            IMountableSeat seat = capi.World?.Player?.Entity?.MountedOn;
            EntitySailboat sailboat = seat?.Entity as EntitySailboat;
            if (seat == null)
            {
                left.AppendLine("Player is not mounted.");
            }
            else if (sailboat == null)
            {
                left.AppendLine("Player is mounted, but not on a Joy of Sailing sailboat.");
                left.AppendLine("Mount entity: " + (seat.Entity?.Code?.ToString() ?? "unknown"));
            }
            else
            {
                sailboat.AppendRatlinePlayerTransformDebug(seat, left, right);
            }

            leftText.SetNewText(left.ToString());
            rightText.SetNewText(right.ToString());
        }

        public override void Dispose()
        {
            base.Dispose();
            capi.Event.UnregisterGameTickListener(tickListenerId);
        }
    }
}

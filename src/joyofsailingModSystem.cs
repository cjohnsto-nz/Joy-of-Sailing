using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace joyofsailing
{
    public class joyofsailingModSystem : ModSystem
    {
        private GuiDialogSailboatDebug debugDialog;
        private GuiDialogSailboatTransformDebug transformDebugDialog;

        // Called on server and client
        // Useful for registering block/entity classes on both sides
        public override void Start(ICoreAPI api)
        {
            RatlineClimbDebugSettings.ResetRuntimeToDefaults();
            api.RegisterEntity("EntitySailboat", typeof(EntitySailboat));
            Achievements.AchievementsManager.RegisterAchievement("joyofsailing", "joyofsailing.setsail", "joyofsailing:sailboat-oak");
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Logger.Notification("Hello from template mod server side: " + Lang.Get("joyofsailing2:hello"));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.Logger.Notification("Hello from template mod client side: " + Lang.Get("joyofsailing2:hello"));

            transformDebugDialog = new GuiDialogSailboatTransformDebug(api);
            api.Gui.RegisterDialog(transformDebugDialog);

            debugDialog = new GuiDialogSailboatDebug(api, transformDebugDialog);
            api.Gui.RegisterDialog(debugDialog);
            api.Input.RegisterHotKey("joyofsailingratlinedebug", "Joy of Sailing: Ratline Debug", GlKeys.F9, HotkeyType.GUIOrOtherControls);
            api.Input.SetHotKeyHandler("joyofsailingratlinedebug", OnToggleRatlineDebug);

        }

        private bool OnToggleRatlineDebug(KeyCombination keyCombination)
        {
            if (debugDialog == null)
            {
                return false;
            }

            if (debugDialog.IsOpened())
            {
                return debugDialog.TryClose();
            }

            return debugDialog.TryOpen();
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}

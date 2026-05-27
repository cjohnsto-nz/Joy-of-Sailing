using System.Linq;
using System.Reflection;
using HarmonyLib;
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
        private Harmony harmony;

        // Called on server and client
        // Useful for registering block/entity classes on both sides
        public override void Start(ICoreAPI api)
        {
            RatlineClimbDebugSettings.ResetRuntimeToDefaults();
            api.RegisterEntity("EntitySailboat", typeof(EntitySailboat));
            // Save Joy sailboats under the vanilla class name so worlds can be reopened
            // without this mod after they have been loaded and saved once with Joy present.
            api.RegisterEntity("EntityBoat", typeof(EntitySailboat));
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

            PatchRatlineThirdPersonHeadFollow(api);
        }

        private void PatchRatlineThirdPersonHeadFollow(ICoreClientAPI api)
        {
            harmony = new Harmony(Mod.Info.ModID + ".ratlineheadcamera");

            MethodInfo adjustHeadAngles = AccessTools.Method(
                typeof(PlayerHeadController),
                "AdjustHeadAngles",
                new[] { typeof(EnumCameraMode), typeof(float) }
            );

            if (adjustHeadAngles == null)
            {
                api.Logger.Warning("Joy of Sailing: could not patch PlayerHeadController.AdjustHeadAngles; ratline third-person head follow is disabled.");
                return;
            }

            harmony.Patch(
                adjustHeadAngles,
                prefix: new HarmonyMethod(typeof(RatlineThirdPersonHeadCameraPatch), nameof(RatlineThirdPersonHeadCameraPatch.Prefix))
            );
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
            harmony?.UnpatchAll(harmony.Id);
            harmony = null;
            base.Dispose();
        }
    }
}

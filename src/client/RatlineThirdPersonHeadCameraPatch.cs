using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace joyofsailing
{
    internal static class RatlineThirdPersonHeadCameraPatch
    {
        private static readonly FieldInfo EntityPlayerField = AccessTools.Field(typeof(PlayerHeadController), "entityPlayer");

        public static bool Prefix(PlayerHeadController __instance, EnumCameraMode cameraMode, float dt)
        {
            if (!RatlineClimbDebugSettings.EnableThirdPersonHeadCameraFollow || cameraMode != EnumCameraMode.ThirdPerson)
            {
                return true;
            }

            EntityPlayer entityPlayer = EntityPlayerField?.GetValue(__instance) as EntityPlayer;
            if (entityPlayer?.MountedOn == null || !EntitySailboat.IsRatlineMount(entityPlayer.MountedOn))
            {
                return true;
            }

            if (entityPlayer.Api is not ICoreClientAPI capi || capi.World?.Player?.Entity?.EntityId != entityPlayer.EntityId)
            {
                return true;
            }

            AngleConstraint headYawLimits = entityPlayer.HeadYawLimits;
            float cameraYaw = capi.Input.MouseYaw;
            float referenceYaw = entityPlayer.BodyYaw;
            if (headYawLimits != null)
            {
                float constrainedYawDelta = GameMath.AngleRadDistance(headYawLimits.CenterRad, cameraYaw);
                constrainedYawDelta = GameMath.Clamp(constrainedYawDelta, -headYawLimits.RangeRad, headYawLimits.RangeRad);
                cameraYaw = headYawLimits.CenterRad + constrainedYawDelta;
                referenceYaw = headYawLimits.CenterRad;
            }

            float yawLimit = GameMath.Max(0f, RatlineClimbDebugSettings.ThirdPersonHeadYawLimitDegrees) * GameMath.DEG2RAD;
            float targetHeadYaw = GameMath.AngleRadDistance(referenceYaw, cameraYaw);
            targetHeadYaw = GameMath.Clamp(targetHeadYaw, -yawLimit, yawLimit);

            float blend = GameMath.Clamp(dt * 10f, 0f, 1f);
            entityPlayer.Pos.HeadYaw += GameMath.AngleRadDistance(entityPlayer.Pos.HeadYaw, targetHeadYaw) * blend;
            entityPlayer.Pos.HeadPitch = GameMath.Clamp((entityPlayer.Pos.Pitch - GameMath.PI) * 0.75f, -69f * GameMath.DEG2RAD, 69f * GameMath.DEG2RAD);
            return false;
        }
    }
}

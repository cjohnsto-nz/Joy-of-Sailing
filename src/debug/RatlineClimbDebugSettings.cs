using Vintagestory.API.MathTools;

namespace joyofsailing
{
    public static class RatlineClimbDebugSettings
    {
        public const float ModelUnitsPerBlock = 16f;

        public const float AssetPathX = 20f;
        public const float AssetLeftPathZ = 20f;
        public const float AssetRightPathZ = -15f;
        public const float AssetStartY = 0f;

        public const float DefaultPathX = 33f;
        public const float DefaultLeftPathZ = 37f;
        public const float DefaultRightPathZ = -32f;
        public const float DefaultStartY = 0f;
        public const float DefaultEndY = 60f;
        public const float DefaultTiltDegrees = -15f;
        public const float DefaultLeanDegrees = 33f;
        public const float DefaultPlayerRotationDegrees = -90f;
        public const float DefaultLeftPlayerTiltDegrees = 10f;
        public const float DefaultRightPlayerTiltDegrees = 10f;
        public const float DefaultPlayerLeanDegrees = 10f;

        public static bool DrawPath = true;
        public static float Speed = 1.25f;
        public static float PathX = DefaultPathX;
        public static float LeftPathZ = DefaultLeftPathZ;
        public static float RightPathZ = DefaultRightPathZ;
        public static float StartY = DefaultStartY;
        public static float EndY = DefaultEndY;
        public static float TiltDegrees = DefaultTiltDegrees;
        public static float LeanDegrees = DefaultLeanDegrees;
        public static float PlayerRotationDegrees = DefaultPlayerRotationDegrees;
        public static float LeftPlayerTiltDegrees = DefaultLeftPlayerTiltDegrees;
        public static float RightPlayerTiltDegrees = DefaultRightPlayerTiltDegrees;
        public static float PlayerLeanDegrees = DefaultPlayerLeanDegrees;

        public static float MaxClimbHeight => GameMath.Max(0f, (EndY - StartY) / ModelUnitsPerBlock);

        public static void ResetRuntimeToDefaults()
        {
            DrawPath = true;
            Speed = 1.25f;
            PathX = DefaultPathX;
            LeftPathZ = DefaultLeftPathZ;
            RightPathZ = DefaultRightPathZ;
            StartY = DefaultStartY;
            EndY = DefaultEndY;
            TiltDegrees = DefaultTiltDegrees;
            LeanDegrees = DefaultLeanDegrees;
            PlayerRotationDegrees = DefaultPlayerRotationDegrees;
            LeftPlayerTiltDegrees = DefaultLeftPlayerTiltDegrees;
            RightPlayerTiltDegrees = DefaultRightPlayerTiltDegrees;
            PlayerLeanDegrees = DefaultPlayerLeanDegrees;
        }
    }
}

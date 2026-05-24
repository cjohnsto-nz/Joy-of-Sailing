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
        public const float DefaultLeftPathZ = 32f;
        public const float DefaultRightPathZ = -27f;
        public const float DefaultStartY = 0f;
        public const float DefaultEndY = 60f;
        public const float DefaultTiltDegrees = -15f;
        public const float DefaultLeanDegrees = 33f;

        public static bool DrawPath = true;
        public static float Speed = 1.25f;
        public static float PathX = DefaultPathX;
        public static float LeftPathZ = DefaultLeftPathZ;
        public static float RightPathZ = DefaultRightPathZ;
        public static float StartY = DefaultStartY;
        public static float EndY = DefaultEndY;
        public static float TiltDegrees = DefaultTiltDegrees;
        public static float LeanDegrees = DefaultLeanDegrees;

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
        }
    }
}

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

        public const float DefaultPathX = 47f;
        public const float DefaultPathCenterZ = 3f;
        public const float DefaultPathZOffset = 28f;
        public const float DefaultStartY = 0f;
        public const float DefaultEndY = 60f;
        public const float DefaultTriangleHeight = 110f;
        public const float DefaultTiltDegrees = -0f;
        public const float DefaultLeanDegrees = 30f;
        public const float DefaultPlayerRotationDegrees = -90f;
        public const float DefaultLeftPlayerTiltDegrees = 6f;
        public const float DefaultRightPlayerTiltDegrees = 6f;
        public const float DefaultLeftPlayerLeanDegrees = 6f;
        public const float DefaultRightPlayerLeanDegrees = -6f;
        public const float DefaultMastDebugXOffset = 0.25f;
        public const bool DefaultInvertSwayX = false;
        public const bool DefaultInvertSwayY = false;
        public const bool DefaultInvertSwayZ = false;
        public const bool DefaultInvertSwaySwivel = false;

        public static bool DrawPath = true;
        public static float Speed = 1.25f;
        public static float PathX = DefaultPathX;
        public static float PathCenterZ = DefaultPathCenterZ;
        public static float PathZOffset = DefaultPathZOffset;
        public static float StartY = DefaultStartY;
        public static float EndY = DefaultEndY;
        public static float TriangleHeight = DefaultTriangleHeight;
        public static float TiltDegrees = DefaultTiltDegrees;
        public static float LeanDegrees = DefaultLeanDegrees;
        public static float PlayerRotationDegrees = DefaultPlayerRotationDegrees;
        public static float LeftPlayerTiltDegrees = DefaultLeftPlayerTiltDegrees;
        public static float RightPlayerTiltDegrees = DefaultRightPlayerTiltDegrees;
        public static float LeftPlayerLeanDegrees = DefaultLeftPlayerLeanDegrees;
        public static float RightPlayerLeanDegrees = DefaultRightPlayerLeanDegrees;
        public static float MastDebugXOffset = DefaultMastDebugXOffset;
        public static bool InvertSwayX = DefaultInvertSwayX;
        public static bool InvertSwayY = DefaultInvertSwayY;
        public static bool InvertSwayZ = DefaultInvertSwayZ;
        public static bool InvertSwaySwivel = DefaultInvertSwaySwivel;

        public static float MaxClimbHeight => GameMath.Max(0f, (EndY - StartY) / ModelUnitsPerBlock);

        public static void ResetRuntimeToDefaults()
        {
            DrawPath = true;
            Speed = 1.25f;
            PathX = DefaultPathX;
            PathCenterZ = DefaultPathCenterZ;
            PathZOffset = DefaultPathZOffset;
            StartY = DefaultStartY;
            EndY = DefaultEndY;
            TriangleHeight = DefaultTriangleHeight;
            TiltDegrees = DefaultTiltDegrees;
            LeanDegrees = DefaultLeanDegrees;
            PlayerRotationDegrees = DefaultPlayerRotationDegrees;
            LeftPlayerTiltDegrees = DefaultLeftPlayerTiltDegrees;
            RightPlayerTiltDegrees = DefaultRightPlayerTiltDegrees;
            LeftPlayerLeanDegrees = DefaultLeftPlayerLeanDegrees;
            RightPlayerLeanDegrees = DefaultRightPlayerLeanDegrees;
            MastDebugXOffset = DefaultMastDebugXOffset;
            InvertSwayX = DefaultInvertSwayX;
            InvertSwayY = DefaultInvertSwayY;
            InvertSwayZ = DefaultInvertSwayZ;
            InvertSwaySwivel = DefaultInvertSwaySwivel;
        }
    }
}

using HarmonyLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Vintagestory.Common;
using Vintagestory.GameContent;
using static HarmonyLib.Code;
using static System.Formats.Asn1.AsnWriter;

namespace joyofsailing
{
    public class EntitySailboat : EntityBoat, ISeatInstSupplier
    {
        // WORLDCONFIG OPTIONS
        // joyofsailing.minwindspeed : adjusts the minimum wind speed
        // joyofsailing.sailspeedmul : multiplies the speed of all sailboats when sailing
        // joyofsailing.scullspeedmul : multiplies the sculling speed of all sailboats

        const string LeftRatlineAttachmentPoint = "RatlineLAP";
        const string RightRatlineAttachmentPoint = "RatlineRAP";
        const string UpperMastAttachmentPoint = "UpperMastAP";
        const string LowerMastAttachmentPoint = "LowerMastAP";
        const string RatlineClimbIdleAnimation = "climbidle";
        const string RatlineClimbMoveAnimation = "climbup";
        const float RatlineClimbMinHeight = 0f;
        const double MastDebugLineStartOffset = 0.5;
        const double MastDebugLineLength = 5.0;
        static readonly System.Reflection.FieldInfo RiderOffsetField = typeof(SeatConfig).GetField("RiderOffset");
        static readonly Vec3f LeftRatlineBaseRiderOffset = new Vec3f(0f, 0f, 0.375f);
        static readonly Vec3f RightRatlineBaseRiderOffset = new Vec3f(-0.0625f, 0.0625f, -0.375f);
        static readonly Vec3f RatlineNeutralMountRotation = new Vec3f();

        float sailLevel = 0f;
        float sailAccuracy = 0f;

        double windSpeed = 0f;
        double windAngle = 0f;

        float sailAngle = 0f;

        float rudderAngle = 0f;

        SailboatAttributes sailAttr;

        EntityBehaviorSelectionBoxes behaviorSelectionBoxes;
        Dictionary<string, int> selBoxId = new Dictionary<string, int>();
        readonly Dictionary<string, float> ratlineClimbBySeat = new Dictionary<string, float>();
        readonly Dictionary<string, Vec3d> ratlineWorldPositionBySeat = new Dictionary<string, Vec3d>();
        readonly Dictionary<string, Vec3f> ratlineWorldRotationBySeat = new Dictionary<string, Vec3f>();
        RatlineDebugRenderer ratlineDebugRenderer;

        float autoScullTimer = 0f;
        float autoScullEnableTimer = 0f;
        bool isAutoSculling = false;
        bool canDisableAutoSculling = false;

        private float curRotMountAngleZ;

        IMountableSeat ISeatInstSupplier.CreateSeat(IMountable mountable, string seatId, SeatConfig config)
        {
            return new EntitySailboatSeat(mountable, seatId, config);
        }

        /*public override void OnEntityLoaded()
        {
            behaviorSelectionBoxes = this.GetBehavior<EntityBehaviorSelectionBoxes>();
        }
        public override void OnEntitySpawn()
        {
            behaviorSelectionBoxes = this.GetBehavior<EntityBehaviorSelectionBoxes>();
        }*/

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            behaviorSelectionBoxes = this.GetBehavior<EntityBehaviorSelectionBoxes>();
            sailAttr = properties.Attributes["sailAttributes"].AsObject<SailboatAttributes>();

            if (api is ICoreClientAPI capi)
            {
                ratlineDebugRenderer = new RatlineDebugRenderer(capi, this);
                capi.Event.RegisterRenderer(ratlineDebugRenderer, EnumRenderStage.OIT, "josailing-ratline-debug");
            }
        }

        public override void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            ICoreClientAPI capi = Api as ICoreClientAPI;

            if (!capi.IsGamePaused)
            {
                updateBoatAngleAndMotion(dt);
                long inWorldEllapsedMilliseconds = capi.InWorldEllapsedMilliseconds;
                float num = 0f;
                if (Swimming)
                {
                    double num2 = capi.World.Calendar.SpeedOfTime / 60f;
                    float num3 = 0.15f + GlobalConstants.CurrentWindSpeedClient.X * 0.9f;
                    float num4 = MathF.PI / 360f * num3;
                    mountAngle.X = GameMath.Sin((float)((double)inWorldEllapsedMilliseconds / 1000.0 * 2.0 * num2)) * 8f * num4;
                    mountAngle.Y = GameMath.Cos((float)((double)inWorldEllapsedMilliseconds / 2000.0 * 2.0 * num2)) * 3f * num4;
                    mountAngle.Z = (0f - GameMath.Sin((float)((double)inWorldEllapsedMilliseconds / 3000.0 * 2.0 * num2))) * 8f * num4;
                    curRotMountAngleZ += ((float)AngularVelocity * 5f * (float)Math.Sign(ForwardSpeed) - curRotMountAngleZ) * dt * 5f;
                    num = (0f - (float)ForwardSpeed) * 1.3f * sailAttr.speedPitchMultiplier; // Configurable speed-pitching
                }

                EntityShapeRenderer entityShapeRenderer = base.Properties.Client.Renderer as EntityShapeRenderer;
                if (entityShapeRenderer != null)
                {
                    entityShapeRenderer.xangle = mountAngle.X + curRotMountAngleZ;
                    entityShapeRenderer.yangle = mountAngle.Y;

                    float maxAngle = sailAttr.speedPitchMaximum / 57.2958f; // degrees to radians
                    entityShapeRenderer.zangle = mountAngle.Z + Math.Clamp(num, -maxAngle, maxAngle); // Only change in that method: makes sure the boat doesn't roll back too far when it's going fast
                }

            }
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            if (Api is ICoreClientAPI capi && ratlineDebugRenderer != null)
            {
                capi.Event.UnregisterRenderer(ratlineDebugRenderer, EnumRenderStage.OIT);
                ratlineDebugRenderer.Dispose();
                ratlineDebugRenderer = null;
            }

            base.OnEntityDespawn(despawn);
        }

        public override void OnTesselation(ref Shape entityShape, string shapePathForLogging)
        {
            Shape shape = entityShape;

            if (shape == entityShape)
            {
                entityShape = entityShape.Clone();
                entityShape.Animations = shape.Animations;
            }

            foreach (SailLevel level in sailAttr.sailLevels)
            {
                if (sailLevel > level.threshold) continue;

                foreach (string element in level.disableElements)
                {
                    entityShape.RemoveElementByName(element);
                }

                foreach (KeyValuePair<string, float> element in level.sailSegmentsRotation)
                {
                    ShapeElement shapeEl = entityShape.GetElementByName(element.Key);
                    if (shapeEl != null)
                    shapeEl.RotationZ = element.Value * sailAccuracy;
                }
                break;
            }

            foreach (string rotatedElement in sailAttr.sailElements)
            {
                ShapeElement shapeEl = entityShape.GetElementByName(rotatedElement);
                if (shapeEl != null)
                    shapeEl.RotationY = sailAngle;
            }

            foreach (string flagElement in sailAttr.flagElements)
            {
                ShapeElement shapeEl = entityShape.GetElementByName(flagElement);
                if (shapeEl != null) shapeEl.RotationY = -(Pos.Yaw * 57.2958f + windAngle) + 180f;
            }

            foreach (KeyValuePair<string, float> element in sailAttr.rudderRotation)
            {
                ShapeElement shapeEl = entityShape.GetElementByName(element.Key);
                if (shapeEl != null)
                {
                    shapeEl.RotationY = rudderAngle * element.Value;
                }
            }

            /*if (Api is ICoreClientAPI capi)
            {
                Vec3d boxPosRight = behaviorSelectionBoxes.GetCenterPosOfBox(22);
                Vec3d boxPosLeft = behaviorSelectionBoxes.GetCenterPosOfBox(23);


                if (boxPosRight != null && boxPosLeft != null)
                {

                    //capi.Render.RenderRectangle((float)boxPosRight.X, (float)boxPosRight.Y, (float)boxPosRight.Z, 1f, 1f, Color.Red.ToArgb());

                    string msg = "";
                    foreach (var atpt in behaviorSelectionBoxes.selectionBoxes)
                    {
                        msg +=  atpt.AttachPoint.Code + " : ";
                    }

                    //capi.ShowChatMessage((boxPosLeft?.ToString() ?? "null") + " : " + (boxPosRight?.ToString() ?? "null"));
                }
            }*/

            base.OnTesselation(ref entityShape, shapePathForLogging);
        }

        protected override void updateBoatAngleAndMotion(float dt)
        {


            updateWind();

            dt = Math.Min(0.5f, dt);
            float physicsFrameTime = GlobalConstants.PhysicsFrameTime;
            this.MarkShapeModified();
            //this.shapeFresh = true;

            bool hasController = false;
            bool hasRatlinePassenger = false;
            Vec3d controlsVec = SeatsToMotionSail(physicsFrameTime, dt, ref hasController, ref hasRatlinePassenger);

            if (!Swimming)
            {
                return;
            }


            //ForwardSpeed += (vec2d.X * (double)SpeedMultiplier - ForwardSpeed) * (double)dt;
            AngularVelocity += (controlsVec.Y * (double)SpeedMultiplier - AngularVelocity) * (double)dt;

            sailLevel += (float)controlsVec.X * 8f * (float)dt;
            sailLevel = Math.Clamp(sailLevel, 0f, 1f);
            sailAngle += (float)controlsVec.Z * 1200f * (float)dt;
            sailAngle = Math.Clamp(sailAngle, -sailAttr.maximumSailAngle, sailAttr.maximumSailAngle);

            rudderAngle += (float)(((-controlsVec.Y / dt) - rudderAngle) * dt * 6f);
            rudderAngle = Math.Clamp(rudderAngle, -1f, 1f);

            if (!hasController && !hasRatlinePassenger)
            {
                sailLevel = 0f;
                WatchedAttributes.SetFloat("josailing.sailLevel", sailLevel);
            }

            float windYaw = ((Pos.Yaw * 57.2958f % 360f + (float)windAngle) + 360f + 180f) % 360f - 180f;

            float windSailDifference = (windYaw + sailAngle + 360f + 180f) % 360f - 180f;

            sailAccuracy = calculateSailPower(windSailDifference);
            //if (Api is ICoreClientAPI capi) capi.ShowChatMessage(sailAccuracy.ToString() + " : " + windYaw.ToString() + " : " + (((windYaw + sailAngle)).ToString()));

            float scullSpeed = (Math.Sign(controlsVec.Y) != Math.Sign(AngularVelocity)) && controlsVec.Y != 0d ? (sailAttr.scullSpeed * World.Config.GetFloat("joyofsailing.scullspeedmul", 1f)) : 0f;

            double desiredSpeed = sailAccuracy
                * Math.Max(Math.Max(windSpeed, sailAttr.minimumWindSpeed), World.Config.GetFloat("joyofsailing.minwindspeed", 0f)) // wind speed
                * sailAttr.windSpeedMultiplier * World.Config.GetFloat("joyofsailing.sailspeedmul", 1f) // wind speed multiplier
                * sailLevel
                + (scullSpeed);

            ForwardSpeed += (desiredSpeed - ForwardSpeed) * (double)dt;


            EntityPos sidedPos = base.SidedPos;
            if (ForwardSpeed != 0.0)
            {
                Vec3d vec3d = sidedPos.GetViewVector().Mul((float)(0.0 - ForwardSpeed)).ToVec3d();
                sidedPos.Motion.X = vec3d.X;
                sidedPos.Motion.Z = vec3d.Z;
            }

            EntityBehaviorPassivePhysicsMultiBox behavior = GetBehavior<EntityBehaviorPassivePhysicsMultiBox>();
            bool flag = true;
            if (AngularVelocity != 0.0)
            {
                float num = (float)AngularVelocity * dt * 30f;
                if (behavior.AdjustCollisionBoxesToYaw(dt, push: true, base.SidedPos.Yaw + num))
                {
                    sidedPos.Yaw += num;
                }
                else
                {
                    flag = false;
                }
            }
            else
            {
                flag = behavior.AdjustCollisionBoxesToYaw(dt, push: true, base.SidedPos.Yaw);
            }

            if (!flag)
            {
                if (behavior.AdjustCollisionBoxesToYaw(dt, push: true, base.SidedPos.Yaw - 0.1f))
                {
                    sidedPos.Yaw -= 0.0002f;
                }
                else if (behavior.AdjustCollisionBoxesToYaw(dt, push: true, base.SidedPos.Yaw + 0.1f))
                {
                    sidedPos.Yaw += 0.0002f;
                }
            }

            sidedPos.Roll = 0f;

            if (controlsVec != Vec3d.Zero || hasRatlinePassenger)
            {
                WatchedAttributes.SetFloat("josailing.sailLevel", sailLevel);
                WatchedAttributes.SetFloat("josailing.sailAngle", sailAngle);
            }

            sailLevel = WatchedAttributes.GetFloat("josailing.sailLevel", 0f);
            sailAngle = WatchedAttributes.GetFloat("josailing.sailAngle", 0f);
        }

        public virtual Vec3d SeatsToMotionSail(float dt, float climbDt, ref bool hasController, ref bool hasRatlinePassenger)
        {
            int rowerCount = 0;
            double forwardAxis = 0.0;
            double sideAxis = 0.0;
            double sprintAxis = 0.0;
            EntityBehaviorSeatable behavior = GetBehavior<EntityBehaviorSeatable>();
            behavior.Controller = null;
            IMountableSeat[] seats = behavior.Seats;
            for (int i = 0; i < seats.Length; i++)
            {
                EntityBoatSeat entityBoatSeat = seats[i] as EntityBoatSeat;
                if (entityBoatSeat == null)
                {
                    continue;
                }

                bool isRatlineSeat = IsRatlineSeat(entityBoatSeat);
                if (entityBoatSeat.Passenger == null)
                {
                    if (isRatlineSeat)
                    {
                        ResetRatlineClimb(entityBoatSeat);
                    }

                    continue;
                }

                if (isRatlineSeat)
                {
                    hasRatlinePassenger = true;
                    int climbDirection = UpdateRatlineClimb(entityBoatSeat, climbDt);
                    UpdateRatlineClimbAnimation(entityBoatSeat, climbDirection);
                }

                if (!(entityBoatSeat.Passenger is EntityPlayer))
                {
                    entityBoatSeat.Passenger.SidedPos.Yaw = base.SidedPos.Yaw;
                }

                if (entityBoatSeat.Config.BodyYawLimit.HasValue)
                {
                    EntityPlayer entityPlayer = entityBoatSeat.Passenger as EntityPlayer;
                    if (entityPlayer != null)
                    {
                        entityPlayer.BodyYawLimits = new AngleConstraint(Pos.Yaw + entityBoatSeat.Config.MountRotation.Y * (MathF.PI / 180f), entityBoatSeat.Config.BodyYawLimit.Value);
                        entityPlayer.HeadYawLimits = new AngleConstraint(Pos.Yaw + entityBoatSeat.Config.MountRotation.Y * (MathF.PI / 180f), MathF.PI / 2f);
                    }
                }

                if (!entityBoatSeat.Config.Controllable || behavior.Controller != null)
                {
                    continue;
                }

                hasController = true;

                EntityControls controls = entityBoatSeat.controls;
                behavior.Controller = entityBoatSeat.Passenger;
                if (!HasPaddle(entityBoatSeat.Passenger))
                {
                    entityBoatSeat.Passenger.AnimManager?.StopAnimation(MountAnimations["ready"]);
                    entityBoatSeat.actionAnim = null;
                    continue;
                }

                if (controls.Left == controls.Right)
                {
                    StopAnimation("turnLeft");
                    StopAnimation("turnRight");
                }

                if (controls.Left && !controls.Right)
                {
                    StartAnimation("turnLeft");
                    StopAnimation("turnRight");
                }

                if (controls.Right && !controls.Left)
                {
                    StopAnimation("turnLeft");
                    StartAnimation("turnRight");
                }

                // Auto sculling handling
                if (controls.Left && controls.Right)
                {
                    autoScullEnableTimer += dt;
                    if (autoScullEnableTimer > 1f)
                    {
                        isAutoSculling = true;
                        canDisableAutoSculling = false;
                        autoScullEnableTimer = 0f;
                    }
                }
                else
                {
                    autoScullEnableTimer = 0f;
                }

                if (!controls.Left && !controls.Right)
                {
                    canDisableAutoSculling = true;
                }

                if (!controls.TriesToMove && !(controls.Sprint || controls.Jump))
                {
                    entityBoatSeat.actionAnim = null;
                    if (entityBoatSeat.Passenger.AnimManager != null && !entityBoatSeat.Passenger.AnimManager.IsAnimationActive(MountAnimations["ready"]))
                    {
                        entityBoatSeat.Passenger.AnimManager.StartAnimation(MountAnimations["ready"]);
                    }

                    continue;
                }

                if (controls.Right && !controls.Backward && !controls.Forward)
                {
                    entityBoatSeat.actionAnim = MountAnimations["backwards"];
                }
                else
                {
                    entityBoatSeat.actionAnim = MountAnimations[controls.Backward ? "backwards" : "forwards"];
                }

                entityBoatSeat.Passenger.AnimManager?.StopAnimation(MountAnimations["ready"]);
                float rowerPower = ((++rowerCount == 1) ? 1f : 0.5f);
                if (controls.Left ^ controls.Right) // for anego: using XOR instead of the vanila OR to avoid double-presses being registered
                {
                    float keyPressed = (controls.Left ? 1 : (-1));
                    sideAxis += (double)(rowerPower * keyPressed * dt);

                    if (canDisableAutoSculling)
                    {
                        isAutoSculling = false;

                    }
                }

                if (controls.Forward ^ controls.Backward)
                {
                    float keyPressed = (controls.Forward ? 1 : (-1));

                    forwardAxis += (double)(rowerPower * keyPressed * dt * 2f);
                }

                if (controls.Sprint ^ controls.Jump)
                {
                    float keyPressed = (controls.Sprint ? 1 : -1);

                    sprintAxis += (double)(rowerPower * keyPressed * dt);
                }
            }

            if (isAutoSculling)
            {
                int scullingSide = (autoScullTimer > 1f) ? 1 : -1;
                sideAxis += (double)(scullingSide * dt / 2f);

                autoScullTimer += dt;
                autoScullTimer = autoScullTimer % 2f;
            }

            return new Vec3d(forwardAxis, sideAxis, sprintAxis);
        }

        private static bool IsRatlineSeat(EntityBoatSeat seat)
        {
            SeatConfig config = seat.Config;
            if (config == null)
            {
                return false;
            }

            if (IsRatlineAttachmentPoint(config.APName) || IsRatlineAttachmentPoint(config.SelectionBox))
            {
                return true;
            }

            return string.Equals(config.Animation, "climbidle", StringComparison.OrdinalIgnoreCase)
                && (config.Attributes?["tireWhenMounted"].AsBool(false) == true
                    || config.Attributes?["vigorStaminaWhenMounted"].AsBool(false) == true);
        }

        private static bool IsRatlineAttachmentPoint(string code)
        {
            return string.Equals(code, LeftRatlineAttachmentPoint, StringComparison.OrdinalIgnoreCase)
                || string.Equals(code, RightRatlineAttachmentPoint, StringComparison.OrdinalIgnoreCase);
        }

        private int UpdateRatlineClimb(EntityBoatSeat seat, float dt)
        {
            SeatConfig config = seat.Config;
            if (config == null)
            {
                return 0;
            }

            string seatKey = GetRatlineSeatKey(seat);
            if (seatKey == null)
            {
                return 0;
            }

            float climbHeight = ratlineClimbBySeat.TryGetValue(seatKey, out float value) ? value : 0f;
            float oldClimbHeight = climbHeight;
            EntityControls controls = seat.controls;
            if (controls != null && (controls.Forward ^ controls.Backward))
            {
                climbHeight += (controls.Forward ? 1f : -1f) * RatlineClimbDebugSettings.Speed * dt;
                climbHeight = GameMath.Clamp(climbHeight, RatlineClimbMinHeight, RatlineClimbDebugSettings.MaxClimbHeight);
            }

            ratlineClimbBySeat[seatKey] = climbHeight;

            ApplyRatlineClimbTransform(seat, GetRatlineBaseRiderOffset(seat), GetRatlineBaseMountRotation(seat), climbHeight);
            if (climbHeight > oldClimbHeight + 0.0001f)
            {
                return 1;
            }

            if (climbHeight < oldClimbHeight - 0.0001f)
            {
                return -1;
            }

            return 0;
        }

        private static void UpdateRatlineClimbAnimation(EntityBoatSeat seat, int climbDirection)
        {
            IAnimationManager animManager = seat.Passenger?.AnimManager;
            if (animManager == null)
            {
                return;
            }

            if (climbDirection == 0)
            {
                animManager.StopAnimation(RatlineClimbMoveAnimation);
                if (!animManager.IsAnimationActive(RatlineClimbIdleAnimation))
                {
                    animManager.StartAnimation(RatlineClimbIdleAnimation);
                }

                return;
            }

            animManager.StopAnimation(RatlineClimbIdleAnimation);
            AnimationMetaData moveAnimation = GetRatlineMoveAnimation(seat);
            if (moveAnimation == null)
            {
                if (!animManager.IsAnimationActive(RatlineClimbMoveAnimation))
                {
                    animManager.StartAnimation(RatlineClimbMoveAnimation);
                }

                return;
            }

            float speed = Math.Abs(moveAnimation.AnimationSpeed);
            if (speed <= 0f)
            {
                speed = 1f;
            }

            float desiredSpeed = climbDirection > 0 ? speed : -speed;
            if (IsRatlineMoveAnimationActive(animManager, moveAnimation, desiredSpeed))
            {
                return;
            }

            StopRatlineMoveAnimation(animManager, moveAnimation);
            AnimationMetaData animationToStart = moveAnimation.Clone();
            animationToStart.AnimationSpeed = desiredSpeed;
            animManager.StartAnimation(animationToStart);
        }

        private static AnimationMetaData GetRatlineMoveAnimation(EntityBoatSeat seat)
        {
            Dictionary<string, AnimationMetaData> animationsByMetaCode = seat.Passenger?.Properties?.Client?.AnimationsByMetaCode;
            if (animationsByMetaCode != null && animationsByMetaCode.TryGetValue(RatlineClimbMoveAnimation, out AnimationMetaData animation))
            {
                return animation;
            }

            return null;
        }

        private static bool IsRatlineMoveAnimationActive(IAnimationManager animManager, AnimationMetaData animation, float desiredSpeed)
        {
            AnimationMetaData activeAnimation = null;
            if (!string.IsNullOrEmpty(animation.Animation))
            {
                animManager.ActiveAnimationsByAnimCode?.TryGetValue(animation.Animation, out activeAnimation);
            }

            if (activeAnimation == null)
            {
                animManager.ActiveAnimationsByAnimCode?.TryGetValue(RatlineClimbMoveAnimation, out activeAnimation);
            }

            return activeAnimation != null && Math.Sign(activeAnimation.AnimationSpeed) == Math.Sign(desiredSpeed);
        }

        private static void StopRatlineMoveAnimation(IAnimationManager animManager, AnimationMetaData animation)
        {
            animManager.StopAnimation(RatlineClimbMoveAnimation);
            if (!string.IsNullOrEmpty(animation.Animation) && !string.Equals(animation.Animation, RatlineClimbMoveAnimation, StringComparison.OrdinalIgnoreCase))
            {
                animManager.StopAnimation(animation.Animation);
            }
        }

        private static Vec3f GetRatlinePathOffset(EntityBoatSeat seat, float climbHeight)
        {
            Vec3f pathPoint = GetRatlineTrianglePoint(seat, climbHeight);
            return new Vec3f(
                (pathPoint.X - RatlineClimbDebugSettings.AssetPathX) / RatlineClimbDebugSettings.ModelUnitsPerBlock,
                (pathPoint.Y - RatlineClimbDebugSettings.AssetStartY) / RatlineClimbDebugSettings.ModelUnitsPerBlock,
                (pathPoint.Z - GetAssetRatlinePathZ(seat)) / RatlineClimbDebugSettings.ModelUnitsPerBlock
            );
        }

        private static Vec3f GetRatlineTrianglePoint(EntityBoatSeat seat, float climbHeight)
        {
            float climbRange = GameMath.Max(0f, RatlineClimbDebugSettings.EndY - RatlineClimbDebugSettings.StartY);
            float side = IsLeftRatlineSeat(seat) ? 1f : -1f;
            float signedZOffset = side * RatlineClimbDebugSettings.PathZOffset;
            Vec3f bottomPoint = new Vec3f(
                RatlineClimbDebugSettings.PathX,
                RatlineClimbDebugSettings.StartY,
                RatlineClimbDebugSettings.PathCenterZ + signedZOffset
            );
            if (climbRange <= 0f)
            {
                return bottomPoint;
            }

            float climbedModelUnits = GameMath.Clamp(
                climbHeight * RatlineClimbDebugSettings.ModelUnitsPerBlock,
                0f,
                climbRange
            );
            float triangleHeight = GameMath.Max(0f, RatlineClimbDebugSettings.TriangleHeight);
            float triangleProgress = triangleHeight <= 0f ? 0f : GameMath.Clamp(climbedModelUnits / triangleHeight, 0f, 1f);
            Vec3f pathPoint = new Vec3f(
                RatlineClimbDebugSettings.PathX,
                RatlineClimbDebugSettings.StartY + climbedModelUnits,
                RatlineClimbDebugSettings.PathCenterZ + signedZOffset * (1f - triangleProgress)
            );

            Vec3f pathVector = new Vec3f(pathPoint.X - bottomPoint.X, pathPoint.Y - bottomPoint.Y, pathPoint.Z - bottomPoint.Z);
            RotateModelVectorX(pathVector, side * RatlineClimbDebugSettings.TiltDegrees);
            RotateModelVectorY(pathVector, side * RatlineClimbDebugSettings.LeanDegrees);
            return new Vec3f(bottomPoint.X + pathVector.X, bottomPoint.Y + pathVector.Y, bottomPoint.Z + pathVector.Z);
        }

        private static void RotateModelVectorX(Vec3f vector, float degrees)
        {
            float radians = degrees * GameMath.DEG2RAD;
            float cos = GameMath.Cos(radians);
            float sin = GameMath.Sin(radians);
            float y = vector.Y * cos - vector.Z * sin;
            float z = vector.Y * sin + vector.Z * cos;
            vector.Y = y;
            vector.Z = z;
        }

        private static void RotateModelVectorY(Vec3f vector, float degrees)
        {
            float radians = degrees * GameMath.DEG2RAD;
            float cos = GameMath.Cos(radians);
            float sin = GameMath.Sin(radians);
            float x = vector.X * cos + vector.Z * sin;
            float z = -vector.X * sin + vector.Z * cos;
            vector.X = x;
            vector.Z = z;
        }

        private static void RotateModelVectorZ(Vec3f vector, float degrees)
        {
            float radians = degrees * GameMath.DEG2RAD;
            float cos = GameMath.Cos(radians);
            float sin = GameMath.Sin(radians);
            float x = vector.X * cos - vector.Y * sin;
            float y = vector.X * sin + vector.Y * cos;
            vector.X = x;
            vector.Y = y;
        }

        private static Vec3f GetRatlineMountRotation(EntityBoatSeat seat, Vec3f baseRotation)
        {
            bool isLeft = IsLeftRatlineSeat(seat);
            float side = isLeft ? 1f : -1f;
            Vec3f rotation = Copy(baseRotation) ?? new Vec3f();
            rotation.X += isLeft ? RatlineClimbDebugSettings.LeftPlayerTiltDegrees : RatlineClimbDebugSettings.RightPlayerTiltDegrees;
            rotation.Y += side * RatlineClimbDebugSettings.PlayerRotationDegrees;
            rotation.Z += isLeft ? RatlineClimbDebugSettings.LeftPlayerLeanDegrees : -RatlineClimbDebugSettings.RightPlayerLeanDegrees;
            return rotation;
        }

        private static void ApplyRatlineClimbTransform(EntityBoatSeat seat, Vec3f baseOffset, Vec3f baseRotation, float climbHeight)
        {
            Vec3f climbOffset = Copy(baseOffset) ?? new Vec3f();
            climbOffset.Add(GetRatlinePathOffset(seat, climbHeight));
            SetSeatOffset(seat.Config, climbOffset);
            seat.Config.MountRotation = GetRatlineMountRotation(seat, baseRotation);
        }

        private Vec3d GetCachedRatlineSeatWorldPosition(EntityBoatSeat seat)
        {
            string seatKey = GetRatlineSeatKey(seat);
            if (seatKey != null && ratlineWorldPositionBySeat.TryGetValue(seatKey, out Vec3d position))
            {
                return position;
            }

            return null;
        }

        private Vec3f GetCachedRatlineSeatWorldRotation(EntityBoatSeat seat)
        {
            string seatKey = GetRatlineSeatKey(seat);
            if (seatKey != null && ratlineWorldRotationBySeat.TryGetValue(seatKey, out Vec3f rotation))
            {
                return rotation;
            }

            return null;
        }

        private void UpdateRatlineWorldPositionCache(EntityBoatSeat seat)
        {
            string seatKey = GetRatlineSeatKey(seat);
            if (seatKey == null)
            {
                return;
            }

            float climbHeight = ratlineClimbBySeat.TryGetValue(seatKey, out float value) ? value : 0f;
            Vec3d pathPos = GetRatlineTriangleWorldPositionAt(seat, climbHeight);
            if (pathPos != null)
            {
                ratlineWorldPositionBySeat[seatKey] = pathPos;
            }

            Vec3f pathRotation = GetRatlinePathWorldRotation(seat);
            if (pathRotation != null)
            {
                ratlineWorldRotationBySeat[seatKey] = pathRotation;
            }
        }

        private void ResetRatlineClimb(EntityBoatSeat seat)
        {
            string seatKey = GetRatlineSeatKey(seat);
            if (seatKey == null)
            {
                return;
            }

            SetSeatOffset(seat.Config, GetRatlineBaseRiderOffset(seat));
            seat.Config.MountRotation = GetRatlineBaseMountRotation(seat);
            ratlineClimbBySeat.Remove(seatKey);
            ratlineWorldPositionBySeat.Remove(seatKey);
            ratlineWorldRotationBySeat.Remove(seatKey);
        }

        private static string GetRatlineSeatKey(EntityBoatSeat seat)
        {
            SeatConfig config = seat.Config;
            if (config == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(config.APName))
            {
                return config.APName;
            }

            if (!string.IsNullOrEmpty(config.SelectionBox))
            {
                return config.SelectionBox;
            }

            return config.Animation;
        }

        private static Vec3f Copy(Vec3f value)
        {
            return value == null ? null : new Vec3f(value.X, value.Y, value.Z);
        }

        private static Vec3f GetSeatOffset(SeatConfig config)
        {
            if (ShouldUseRiderOffset(config))
            {
                return RiderOffsetField.GetValue(config) as Vec3f;
            }

#pragma warning disable CS0618
            return config.MountOffset;
#pragma warning restore CS0618
        }

        private static void SetSeatOffset(SeatConfig config, Vec3f offset)
        {
            if (ShouldUseRiderOffset(config))
            {
                RiderOffsetField.SetValue(config, offset);
                return;
            }

#pragma warning disable CS0618
            config.MountOffset = offset;
#pragma warning restore CS0618
        }

        private static bool ShouldUseRiderOffset(SeatConfig config)
        {
#pragma warning disable CS0618
            return RiderOffsetField != null && config.MountOffset == null;
#pragma warning restore CS0618
        }

        public void DrawRatlineDebugPaths(ICoreClientAPI capi)
        {
            EntityBehaviorSeatable behavior = GetBehavior<EntityBehaviorSeatable>();
            if (behavior?.Seats == null)
            {
                return;
            }

            bool drawPath = RatlineClimbDebugSettings.DrawPath;
            foreach (IMountableSeat mountableSeat in behavior.Seats)
            {
                EntityBoatSeat seat = mountableSeat as EntityBoatSeat;
                if (seat == null || !IsRatlineSeat(seat))
                {
                    continue;
                }

                UpdateRatlineWorldPositionCache(seat);
                if (drawPath)
                {
                    DrawRatlineDebugPath(capi, seat, IsLeftRatlineSeat(seat)
                        ? ColorUtil.ColorFromRgba(255, 64, 64, 255)
                        : ColorUtil.ColorFromRgba(64, 160, 255, 255));
                }
            }

            if (drawPath)
            {
                DrawMastDebugPath(capi);
            }
        }

        private void DrawMastDebugPath(ICoreClientAPI capi)
        {
            Vec3d lowerMastPos = GetSelectionBoxCenter(LowerMastAttachmentPoint);
            if (lowerMastPos == null)
            {
                return;
            }

            Vec3d mastAxis = GetRenderedBoatUpAxis();
            if (mastAxis == null)
            {
                return;
            }

            Vec3d xOffset = GetRenderedBoatXAxis() * RatlineClimbDebugSettings.MastDebugXOffset;
            Vec3d startPos = lowerMastPos + xOffset - mastAxis * MastDebugLineStartOffset;
            Vec3d endPos = lowerMastPos + xOffset + mastAxis * MastDebugLineLength;

            DrawDebugLine(capi, startPos, endPos, ColorUtil.ColorFromRgba(255, 224, 64, 255));
        }

        private Vec3d GetRenderedBoatXAxis()
        {
            return GetRenderedBoatAxis(new Vec4d(1.0, 0.0, 0.0, 0.0)) ?? Vec3d.Zero;
        }

        private Vec3d GetRenderedBoatUpAxis()
        {
            return GetRenderedBoatAxis(new Vec4d(0.0, 1.0, 0.0, 0.0));
        }

        private Vec3d GetRenderedBoatZAxis()
        {
            return GetRenderedBoatAxis(new Vec4d(0.0, 0.0, 1.0, 0.0)) ?? Vec3d.Zero;
        }

        private Vec3d GetRenderedBoatAxis(Vec4d modelAxis)
        {
            Matrixf transform = new Matrixf();
            transform.Identity();
            transform.RotateY((float)Math.PI / 2f + Pos.Yaw);

            EntityShapeRenderer entityShapeRenderer = base.Properties?.Client?.Renderer as EntityShapeRenderer;
            if (entityShapeRenderer != null)
            {
                transform.RotateX(GetSwaySign(RatlineClimbDebugSettings.InvertSwayX) * entityShapeRenderer.xangle);
                transform.RotateY(GetSwaySign(RatlineClimbDebugSettings.InvertSwayY) * entityShapeRenderer.yangle);
                transform.RotateZ(GetSwaySign(RatlineClimbDebugSettings.InvertSwayZ) * entityShapeRenderer.zangle);
                transform.RotateX(GetSwaySign(RatlineClimbDebugSettings.InvertSwaySwivel) * entityShapeRenderer.nowSwivelRad);
            }

            Vec3d axis = transform.TransformVector(modelAxis).XYZ;
            double axisLength = axis.Length();
            if (axisLength <= 0.0001)
            {
                return null;
            }

            axis.Mul(1.0 / axisLength);
            return axis;
        }

        private static float GetSwaySign(bool inverted)
        {
            return inverted ? -1f : 1f;
        }

        private void DrawRatlineDebugPath(ICoreClientAPI capi, EntityBoatSeat seat, int color)
        {
            Vec3d startPos = GetRatlineTriangleWorldPositionAt(seat, RatlineClimbMinHeight);
            Vec3d endPos = GetRatlineTriangleWorldPositionAt(seat, RatlineClimbDebugSettings.MaxClimbHeight);
            if (startPos == null || endPos == null)
            {
                return;
            }

            DrawDebugLine(capi, startPos, endPos, color);
        }

        private Vec3f GetRatlinePathWorldRotation(EntityBoatSeat seat)
        {
            Vec3d startPos = GetRatlineTriangleWorldPositionAt(seat, RatlineClimbMinHeight);
            Vec3d endPos = GetRatlineTriangleWorldPositionAt(seat, RatlineClimbDebugSettings.MaxClimbHeight);
            if (startPos == null || endPos == null)
            {
                return null;
            }

            Vec3d pathDirection = endPos - startPos;
            double pathLength = pathDirection.Length();
            if (pathLength <= 0.0001)
            {
                return new Vec3f();
            }

            pathDirection.Mul(1.0 / pathLength);
            double horizontalLength = Math.Sqrt(pathDirection.X * pathDirection.X + pathDirection.Z * pathDirection.Z);
            if (horizontalLength <= 0.0001)
            {
                return new Vec3f();
            }

            double angleFromWorldUp = Math.Atan2(horizontalLength, pathDirection.Y);
            double rotationAxisX = pathDirection.Z / horizontalLength;
            double rotationAxisZ = -pathDirection.X / horizontalLength;

            float mountYaw = (seat.Config?.MountRotation?.Y ?? 0f) * GameMath.DEG2RAD;
            float playerYaw = Pos.Yaw + mountYaw;
            var forwardVec = EntityPos.GetViewVector(0f, playerYaw);
            var rightVec = EntityPos.GetViewVector(0f, playerYaw + GameMath.PIHALF);

            Vec3d playerForward = NormalizeHorizontal(forwardVec.X, forwardVec.Z);
            Vec3d playerRight = NormalizeHorizontal(rightVec.X, rightVec.Z);
            if (playerForward == null || playerRight == null)
            {
                return new Vec3f();
            }

            double roll = angleFromWorldUp * (rotationAxisX * playerForward.X + rotationAxisZ * playerForward.Z);
            double pitch = angleFromWorldUp * (rotationAxisX * playerRight.X + rotationAxisZ * playerRight.Z);
            return new Vec3f((float)roll, 0f, (float)pitch);
        }

        private static Vec3d NormalizeHorizontal(double x, double z)
        {
            double length = Math.Sqrt(x * x + z * z);
            if (length <= 0.0001)
            {
                return null;
            }

            return new Vec3d(x / length, 0.0, z / length);
        }

        private void DrawDebugLine(ICoreClientAPI capi, Vec3d startPos, Vec3d endPos, int color)
        {
            BlockPos origin = Pos.AsBlockPos;
            capi.Render.RenderLine(
                origin,
                (float)(startPos.X - origin.X), (float)(startPos.Y - origin.Y), (float)(startPos.Z - origin.Z),
                (float)(endPos.X - origin.X), (float)(endPos.Y - origin.Y), (float)(endPos.Z - origin.Z),
                color
            );
        }

        private Vec3d GetSelectionBoxCenter(string attachmentPointCode)
        {
            if (behaviorSelectionBoxes?.selectionBoxes == null)
            {
                return null;
            }

            for (int i = 0; i < behaviorSelectionBoxes.selectionBoxes.Length; i++)
            {
                if (string.Equals(behaviorSelectionBoxes.selectionBoxes[i].AttachPoint.Code, attachmentPointCode, StringComparison.OrdinalIgnoreCase))
                {
                    return behaviorSelectionBoxes.GetCenterPosOfBox(i);
                }
            }

            return null;
        }

        private Vec3d GetRatlineTriangleWorldPositionAt(EntityBoatSeat seat, float climbHeight)
        {
            Vec3d lowerMastPos = GetSelectionBoxCenter(LowerMastAttachmentPoint);
            Vec3d xAxis = GetRenderedBoatXAxis();
            Vec3d yAxis = GetRenderedBoatUpAxis();
            Vec3d zAxis = GetRenderedBoatZAxis();
            if (lowerMastPos == null || xAxis == null || yAxis == null || zAxis == null)
            {
                return null;
            }

            Vec3f pathPoint = GetRatlineTrianglePoint(seat, climbHeight);
            double x = (pathPoint.X - RatlineClimbDebugSettings.DefaultPathX) / RatlineClimbDebugSettings.ModelUnitsPerBlock;
            double y = pathPoint.Y / RatlineClimbDebugSettings.ModelUnitsPerBlock;
            double z = pathPoint.Z / RatlineClimbDebugSettings.ModelUnitsPerBlock;
            return lowerMastPos + xAxis * x + yAxis * y + zAxis * z;
        }

        private static Vec3f GetRatlineBaseRiderOffset(EntityBoatSeat seat)
        {
            return Copy(IsLeftRatlineSeat(seat) ? LeftRatlineBaseRiderOffset : RightRatlineBaseRiderOffset);
        }

        private static Vec3f GetRatlineBaseMountRotation(EntityBoatSeat seat)
        {
            return Copy(RatlineNeutralMountRotation);
        }

        private static float GetRatlinePathZ(EntityBoatSeat seat)
        {
            return RatlineClimbDebugSettings.PathCenterZ + (IsLeftRatlineSeat(seat) ? 1f : -1f) * RatlineClimbDebugSettings.PathZOffset;
        }

        private static float GetAssetRatlinePathZ(EntityBoatSeat seat)
        {
            return IsLeftRatlineSeat(seat) ? RatlineClimbDebugSettings.AssetLeftPathZ : RatlineClimbDebugSettings.AssetRightPathZ;
        }

        private static bool IsLeftRatlineSeat(EntityBoatSeat seat)
        {
            SeatConfig config = seat.Config;
            return string.Equals(config?.APName, LeftRatlineAttachmentPoint, StringComparison.OrdinalIgnoreCase)
                || string.Equals(config?.SelectionBox, LeftRatlineAttachmentPoint, StringComparison.OrdinalIgnoreCase);
        }

        private class EntitySailboatSeat : EntityBoatSeat
        {
            public EntitySailboatSeat(IMountable mountablesupplier, string seatId, SeatConfig config)
                : base(mountablesupplier, seatId, config)
            {
            }

            public override EntityPos SeatPosition
            {
                get
                {
                    EntityPos pos = base.SeatPosition;
                    if (!IsRatlineSeat(this) || Config?.MountRotation == null)
                    {
                        return pos;
                    }

                    EntityPos cameraPos = pos.Copy();
                    if (Entity is EntitySailboat sailboat)
                    {
                        Vec3d pathPos = sailboat.GetCachedRatlineSeatWorldPosition(this);
                        if (pathPos != null)
                        {
                            cameraPos.SetPosWithDimension(pathPos);
                        }

                        Vec3f swayRotation = sailboat.GetCachedRatlineSeatWorldRotation(this);
                        if (swayRotation != null)
                        {
                            cameraPos.Roll -= swayRotation.X;
                            cameraPos.Yaw += swayRotation.Y;
                            cameraPos.Pitch -= swayRotation.Z;
                        }
                    }

                    cameraPos.Roll += Config.MountRotation.X * GameMath.DEG2RAD;
                    cameraPos.Yaw += Config.MountRotation.Y * GameMath.DEG2RAD;
                    cameraPos.Pitch += Config.MountRotation.Z * GameMath.DEG2RAD;
                    return cameraPos;
                }
            }

            public override Vec3f LocalEyePos
            {
                get
                {
                    Vec3f eyePos = base.LocalEyePos;
                    if (!IsRatlineSeat(this) || Config?.MountRotation == null)
                    {
                        return eyePos;
                    }

                    Vec3f adjustedEyePos = Copy(eyePos) ?? new Vec3f();
                    RotateModelVectorX(adjustedEyePos, Config.MountRotation.X);
                    RotateModelVectorZ(adjustedEyePos, IsLeftRatlineSeat(this) ? Config.MountRotation.Z : -Config.MountRotation.Z);
                    return adjustedEyePos;
                }
            }

            public override Matrixf RenderTransform
            {
                get
                {
                    if (!IsRatlineSeat(this) || IsLeftRatlineSeat(this) || Config?.MountRotation == null)
                    {
                        return base.RenderTransform;
                    }

#pragma warning disable CS0618
                    if (Config.MountOffset != null)
                    {
                        return base.RenderTransform;
                    }
#pragma warning restore CS0618

                    Matrixf transform = new Matrixf();
                    transform.Identity();

                    Entity passenger = Passenger;
                    float passengerSize = passenger?.Properties?.Client?.Size ?? 1f;
                    if (Config.RiderOffset != null && passengerSize != 1f)
                    {
                        transform.Translate(Config.RiderOffset.Clone().Mul(passengerSize - 1f));
                    }

                    transform.RotateX(Config.MountRotation.X * GameMath.DEG2RAD);
                    transform.RotateZ(-Config.MountRotation.Z * GameMath.DEG2RAD);
                    return transform;
                }
            }
        }

        public void updateWind()
        {
            Vec3d windVector = World.BlockAccessor.GetWindSpeedAt(SidedPos.XYZ).Clone();
            windAngle = Math.Atan2(windVector.Normalize().Z, windVector.Normalize().X) * GameMath.RAD2DEG - 90f;
            windSpeed = World.BlockAccessor.GetWindSpeedAt(SidedPos.XYZ).Length();

            //windSpeed = Math.Max(SailboatConfig.Current.minSpeed, windSpeed); // WIND MIN SPEED : A RETIRER SI CA MARCHE PAS
        }

        float calculateSailPower(float sailWindAngle)
        {
            //sailAttr.perfectAngle
            //sailAttr.falloffAngle

            bool shouldUseTackBonus = Math.Sign(sailWindAngle) != Math.Sign(sailAngle) && Math.Abs(sailAngle) >= sailAttr.maximumSailAngle; // Give extra tolerance to make it easier/possible to go against the wind

            //if (Api is ICoreClientAPI capi) capi.ShowChatMessage(sailWindAngle.ToString() + " against " + sailAngle.ToString() + ", sail bonus " + shouldUseTackBonus.ToString());

            float perfectAngle = sailAttr.perfectAngle + (shouldUseTackBonus ? sailAttr.fullyTackedExtraTolerance : 0f);
            float falloffAngle = sailAttr.falloffAngle + (shouldUseTackBonus ? sailAttr.fullyTackedExtraTolerance : 0f);

            if (Math.Abs(sailWindAngle) <= perfectAngle) return 1f;
            if (Math.Abs(sailWindAngle) > falloffAngle) return 0f;
            return 0f + (1f - 0f) * ((Math.Abs(sailWindAngle) - falloffAngle) / (perfectAngle - falloffAngle)); // Remap the value if in the falloff
        }
    }
    public class SailboatAttributes
    {
        public SailLevel[] sailLevels = new SailLevel[0];
        public float windSpeedMultiplier = 1.0f;
        public float minimumWindSpeed = 0.1f;
        public float scullSpeed = 1.0f;

        public float maximumSailAngle = 70f;
        public float perfectAngle = 10f;
        public float falloffAngle = 45f;
        public float fullyTackedExtraTolerance = 20.0f;

        public float speedPitchMultiplier = 1.0f;
        public float speedPitchMaximum = 15f;

        public Dictionary<string, float> rudderRotation = new Dictionary<string, float>();

        public string[] sailElements = new string[0];

        public string[] flagElements = new string[0];

        public SailboatCordage[] cordages = new SailboatCordage[0];
    }

    public class SailLevel
    {
        public float threshold = 0f;
        public string[] disableElements = new string[0];
        public string[] enableElements = new string[0];
        public Dictionary<string, float> sailSegmentsRotation = new Dictionary<string, float>();
    }

    public class SailboatCordage
    {
        public string fixedAPCode = "";
        public float ropeLength = 1.0f;

        public CordageAttachment[] cordageAttachments = new CordageAttachment[0];
    }

    public class  CordageAttachment
    {
        public string apCode = "";
        public float threshold = 0.0f;
    }
}

using HarmonyLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
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
        const double RatlineBasisDebugLineLength = 0.75;
        static readonly System.Reflection.FieldInfo RiderOffsetField = typeof(SeatConfig).GetField("RiderOffset");
        static readonly Vec3f RatlineBaseRiderOffset = new Vec3f();
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
        readonly Dictionary<string, Vec3f> ratlineWorldRotationBySeat = new Dictionary<string, Vec3f>();
        RatlineDebugRenderer ratlineDebugRenderer;
        long nextRatlineClimbWarningMs;

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
                    if (RatlineClimbDebugSettings.OverrideBoatSway)
                    {
                        entityShapeRenderer.xangle = RatlineClimbDebugSettings.BoatSwayXDegrees * GameMath.DEG2RAD;
                        entityShapeRenderer.yangle = RatlineClimbDebugSettings.BoatSwayYDegrees * GameMath.DEG2RAD;
                        entityShapeRenderer.zangle = RatlineClimbDebugSettings.BoatSwayZDegrees * GameMath.DEG2RAD;
                        entityShapeRenderer.nowSwivelRad = RatlineClimbDebugSettings.BoatSwivelDegrees * GameMath.DEG2RAD;
                    }
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

            if (!hasController && hasRatlinePassenger)
            {
                sailLevel = WatchedAttributes.GetFloat("josailing.sailLevel", sailLevel);
                sailAngle = WatchedAttributes.GetFloat("josailing.sailAngle", sailAngle);
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
            bool hasControllablePassenger = seats.Any(seat => seat is EntityBoatSeat boatSeat
                && boatSeat.Passenger != null
                && boatSeat.Config?.Controllable == true);
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
                    try
                    {
                        int climbDirection = UpdateRatlineClimb(entityBoatSeat, climbDt);
                        UpdateRatlineClimbAnimation(entityBoatSeat, climbDirection);
                    }
                    catch (Exception ex)
                    {
                        WarnRatlineClimbUpdateFailed(ex);
                    }

                    if (RatlineClimbDebugSettings.EnableRatlineSteering && !hasControllablePassenger)
                    {
                        EntityControls ratlineControls = entityBoatSeat.controls;
                        if (ratlineControls != null && (ratlineControls.Left ^ ratlineControls.Right))
                        {
                            hasController = true;
                            behavior.Controller ??= entityBoatSeat.Passenger;
                            float keyPressed = ratlineControls.Left ? 1f : -1f;
                            sideAxis += keyPressed * dt * RatlineClimbDebugSettings.RatlineSteeringMultiplier;
                        }
                    }
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

        private Vec3f GetRatlinePathOffset(EntityBoatSeat seat, float climbHeight)
        {
            Vec3f pathPoint = GetRatlineTrianglePoint(seat, climbHeight);
            Vec3f anchorPoint = TryGetRatlineAnchorPoint(seat) ?? GetAssetRatlineAnchorPoint(seat);
            Vec3f offset = new Vec3f();
            if (RatlineClimbDebugSettings.EnablePathOffset)
            {
                offset.Add(new Vec3f(
                    (pathPoint.X - anchorPoint.X) / RatlineClimbDebugSettings.ModelUnitsPerBlock,
                    (pathPoint.Y - anchorPoint.Y) / RatlineClimbDebugSettings.ModelUnitsPerBlock,
                    (pathPoint.Z - anchorPoint.Z) / RatlineClimbDebugSettings.ModelUnitsPerBlock
                ));
            }

            if (RatlineClimbDebugSettings.EnableSwayOffset)
            {
                offset.Add(TryGetRatlineSwayLocalOffset(seat, pathPoint));
            }

            return offset;
        }

        private Vec3f TryGetRatlineAnchorPoint(EntityBoatSeat seat)
        {
            try
            {
                return GetRatlineAnchorPoint(seat);
            }
            catch (Exception ex)
            {
                WarnRatlineClimbUpdateFailed(ex);
                return null;
            }
        }

        private Vec3f TryGetRatlineSwayLocalOffset(EntityBoatSeat seat, Vec3f pathPoint)
        {
            try
            {
                return GetRatlineSwayLocalOffset(seat, pathPoint);
            }
            catch (Exception ex)
            {
                WarnRatlineClimbUpdateFailed(ex);
                return new Vec3f();
            }
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
            if (RatlineClimbDebugSettings.EnablePathTilt)
            {
                RotateModelVectorX(pathVector, side * RatlineClimbDebugSettings.TiltDegrees);
            }

            if (RatlineClimbDebugSettings.EnablePathLean)
            {
                RotateModelVectorY(pathVector, side * RatlineClimbDebugSettings.LeanDegrees);
            }

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
            if (RatlineClimbDebugSettings.EnablePlayerTilt)
            {
                rotation.X += isLeft ? RatlineClimbDebugSettings.LeftPlayerTiltDegrees : RatlineClimbDebugSettings.RightPlayerTiltDegrees;
            }

            if (RatlineClimbDebugSettings.EnablePlayerYaw)
            {
                rotation.Y += side * RatlineClimbDebugSettings.PlayerRotationDegrees;
            }

            if (RatlineClimbDebugSettings.EnablePlayerLean)
            {
                rotation.Z += isLeft ? RatlineClimbDebugSettings.LeftPlayerLeanDegrees : -RatlineClimbDebugSettings.RightPlayerLeanDegrees;
            }

            return rotation;
        }

        private void ApplyRatlineClimbTransform(EntityBoatSeat seat, Vec3f baseOffset, Vec3f baseRotation, float climbHeight)
        {
            SetSeatOffset(seat.Config, Copy(baseOffset) ?? new Vec3f());
            seat.Config.MountRotation = GetRatlineMountRotation(seat, baseRotation);
        }

        private static Vec3f GetRatlinePlayerOffset(EntityBoatSeat seat)
        {
            return new Vec3f(
                RatlineClimbDebugSettings.PlayerOffsetX,
                RatlineClimbDebugSettings.PlayerOffsetY,
                IsLeftRatlineSeat(seat) ? -RatlineClimbDebugSettings.PlayerOffsetZ : RatlineClimbDebugSettings.PlayerOffsetZ
            );
        }

        private static Vec3d GetDebugSeatWorldOffset(float yaw)
        {
            Vec3f forwardVec = EntityPos.GetViewVector(0f, yaw);
            Vec3f rightVec = EntityPos.GetViewVector(0f, yaw + GameMath.PIHALF);
            return new Vec3d(
                rightVec.X * RatlineClimbDebugSettings.DebugSeatOffsetX + forwardVec.X * RatlineClimbDebugSettings.DebugSeatOffsetZ,
                RatlineClimbDebugSettings.DebugSeatOffsetY,
                rightVec.Z * RatlineClimbDebugSettings.DebugSeatOffsetX + forwardVec.Z * RatlineClimbDebugSettings.DebugSeatOffsetZ
            );
        }

        private static Vec3f GetDebugSeatRotationDegrees()
        {
            return new Vec3f(
                RatlineClimbDebugSettings.DebugSeatRollDegrees,
                RatlineClimbDebugSettings.DebugSeatYawDegrees,
                RatlineClimbDebugSettings.DebugSeatPitchDegrees
            );
        }

        private static Vec3f GetDebugEyeOffset()
        {
            return new Vec3f(
                RatlineClimbDebugSettings.DebugEyeOffsetX,
                RatlineClimbDebugSettings.DebugEyeOffsetY,
                RatlineClimbDebugSettings.DebugEyeOffsetZ
            );
        }

        private static Vec3f GetDebugModelOffset()
        {
            return new Vec3f(
                RatlineClimbDebugSettings.DebugModelOffsetX,
                RatlineClimbDebugSettings.DebugModelOffsetY,
                RatlineClimbDebugSettings.DebugModelOffsetZ
            );
        }

        private static Vec3f GetDebugModelRotationDegrees()
        {
            return new Vec3f(
                RatlineClimbDebugSettings.DebugModelRollDegrees,
                RatlineClimbDebugSettings.DebugModelYawDegrees,
                RatlineClimbDebugSettings.DebugModelPitchDegrees
            );
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

        private float GetRatlinePlayerFrontBackSwaySourcePitch()
        {
            float speedPitch = 0f;
            if (Swimming)
            {
                float maxAngle = sailAttr.speedPitchMaximum / 57.2958f;
                speedPitch = Math.Clamp((0f - (float)ForwardSpeed) * 1.3f * sailAttr.speedPitchMultiplier, -maxAngle, maxAngle);
            }

            return mountAngle.Z + speedPitch;
        }

        private float GetRatlinePlayerFrontBackSideSign(EntityBoatSeat seat)
        {
            return IsLeftRatlineSeat(seat) ? -1f : 1f;
        }

        private float GetRatlinePlayerFrontBackSwayPitch(EntityBoatSeat seat)
        {
            if (!RatlineClimbDebugSettings.EnablePlayerFrontBackSway)
            {
                return 0f;
            }

            float sign = RatlineClimbDebugSettings.InvertPlayerFrontBackSway ? -1f : 1f;
            return GetRatlinePlayerFrontBackSideSign(seat) * sign * GetRatlinePlayerFrontBackSwaySourcePitch();
        }

        public void AppendRatlinePlayerTransformDebug(IMountableSeat mountableSeat, StringBuilder left, StringBuilder right)
        {
            EntityBoatSeat seat = mountableSeat as EntityBoatSeat;
            if (seat == null)
            {
                left.AppendLine("Mounted seat is not an EntityBoatSeat.");
                return;
            }

            if (!IsRatlineSeat(seat))
            {
                left.AppendLine("Mounted on this sailboat, but not on a ratline seat.");
                left.AppendLine("Seat key: " + (GetRatlineSeatKey(seat) ?? "unknown"));
                return;
            }

            string seatKey = GetRatlineSeatKey(seat) ?? "unknown";
            bool isLeft = IsLeftRatlineSeat(seat);
            float climbHeight = ratlineClimbBySeat.TryGetValue(seatKey, out float storedClimbHeight) ? storedClimbHeight : 0f;
            Vec3f pathPoint = GetRatlineTrianglePoint(seat, climbHeight);
            Vec3d pathWorldPosition = GetRatlineTriangleWorldPositionAt(seat, climbHeight);
            Vec3d playerWorldOffset = GetRatlinePlayerWorldOffset(seat);
            Vec3d directSeatWorldPosition = TryGetRatlineSeatWorldPosition(seat);
            Vec3f anchorPoint = TryGetRatlineAnchorPoint(seat) ?? GetAssetRatlineAnchorPoint(seat);
            Vec3f pathDelta = new Vec3f(
                (pathPoint.X - anchorPoint.X) / RatlineClimbDebugSettings.ModelUnitsPerBlock,
                (pathPoint.Y - anchorPoint.Y) / RatlineClimbDebugSettings.ModelUnitsPerBlock,
                (pathPoint.Z - anchorPoint.Z) / RatlineClimbDebugSettings.ModelUnitsPerBlock
            );
            Vec3f swayDelta = TryGetRatlineSwayLocalOffset(seat, pathPoint);
            Vec3f playerOffset = GetRatlinePlayerOffset(seat);
            Vec3f finalSeatOffset = GetSeatOffset(seat.Config);
            Vec3f baseRotation = GetRatlineBaseMountRotation(seat);
            Vec3f mountRotation = seat.Config?.MountRotation;
            Vec3f cachedPathRotation = GetCachedRatlineSeatWorldRotation(seat) ?? new Vec3f();
            Vec3f livePathRotation = GetRatlinePathWorldRotation(seat) ?? new Vec3f();
            Vec3f debugSeatOffset = new Vec3f(
                RatlineClimbDebugSettings.DebugSeatOffsetX,
                RatlineClimbDebugSettings.DebugSeatOffsetY,
                RatlineClimbDebugSettings.DebugSeatOffsetZ
            );
            Vec3f debugSeatRotation = GetDebugSeatRotationDegrees();
            Vec3f debugEyeOffset = GetDebugEyeOffset();
            Vec3f debugModelOffset = GetDebugModelOffset();
            Vec3f debugModelRotation = GetDebugModelRotationDegrees();
            int debugAngleMode = RatlineClimbDebugSettings.DebugSeatAngleMode;
            float frontBackSourcePitch = GetRatlinePlayerFrontBackSwaySourcePitch();
            float frontBackAppliedPitch = GetRatlinePlayerFrontBackSwayPitch(seat);
            float seatFrontBackRoll = RatlineClimbDebugSettings.EnableSeatFrontBackSway ? frontBackAppliedPitch : 0f;
            float eyeFrontBackPitch = RatlineClimbDebugSettings.EnableEyeFrontBackSway ? frontBackAppliedPitch : 0f;
            float modelFrontBackPitch = RatlineClimbDebugSettings.EnableModelFrontBackSway ? frontBackAppliedPitch : 0f;
            EntityPos seatPosition = seat.SeatPosition;
            Vec3f localEyePos = seat.LocalEyePos;
            EntityShapeRenderer renderer = base.Properties?.Client?.Renderer as EntityShapeRenderer;
            RatlineModelFrame modelFrame = GetRatlineModelFrame(seat);

            left.AppendLine("--- Mounted Ratline ---");
            left.AppendLine("Seat: " + (isLeft ? "Left" : "Right"));
            left.AppendLine("Seat key: " + seatKey);
            left.AppendLine("Passenger: " + (seat.Passenger?.Code?.ToString() ?? "none"));
            left.AppendLine("AngleMode: " + seat.AngleMode + " (debug override disabled, value " + debugAngleMode + ")");
            left.AppendLine("Climb: " + F(climbHeight) + " / " + F(RatlineClimbDebugSettings.MaxClimbHeight) + " blocks");
            left.AppendLine("ForwardSpeed: " + F((float)ForwardSpeed));
            left.AppendLine("Swimming: " + Swimming);
            left.AppendLine("Ratline steering: " + RatlineClimbDebugSettings.EnableRatlineSteering
                + " x" + F(RatlineClimbDebugSettings.RatlineSteeringMultiplier));

            left.AppendLine();
            left.AppendLine("--- Active Contribution Switches ---");
            left.AppendLine("Direct seat lock: path world point + player align offset");
            left.AppendLine("Player align offset enabled: " + RatlineClimbDebugSettings.EnablePlayerOffset);
            left.AppendLine("Path tilt / lean: "
                + RatlineClimbDebugSettings.EnablePathTilt + " / "
                + RatlineClimbDebugSettings.EnablePathLean);
            left.AppendLine("Model correction tilt / yaw / lean: "
                + RatlineClimbDebugSettings.EnablePlayerTilt + " / "
                + RatlineClimbDebugSettings.EnablePlayerYaw + " / "
                + RatlineClimbDebugSettings.EnablePlayerLean);
            left.AppendLine("Seat path/pose/direct FB experiments: disabled");

            left.AppendLine();
            left.AppendLine("--- Offset Pipeline (blocks) ---");
            left.AppendLine("Base rider offset: " + Vec(GetRatlineBaseRiderOffset(seat)));
            left.AppendLine("Anchor model point: " + Vec(anchorPoint));
            left.AppendLine("Path model point: " + Vec(pathPoint));
            left.AppendLine("Path delta: " + Vec(pathDelta));
            left.AppendLine("Sway delta: " + Vec(swayDelta));
            left.AppendLine("Player align offset: " + Vec(playerOffset));
            left.AppendLine("Final config offset: " + Vec(finalSeatOffset));
            left.AppendLine("Path world position: " + Vec(pathWorldPosition));
            left.AppendLine("Player world offset: " + Vec(playerWorldOffset));
            left.AppendLine("Direct seat world: " + Vec(directSeatWorldPosition));
            left.AppendLine("Debug seat local offset: " + Vec(debugSeatOffset));

            left.AppendLine();
            left.AppendLine("--- Final Player Positions ---");
            left.AppendLine("SeatPosition XYZ: " + PosXYZ(seatPosition));
            left.AppendLine("SeatPosition RYP: " + PosRot(seatPosition));
            left.AppendLine("LocalEyePos: " + Vec(localEyePos));
            if (seat.Passenger != null)
            {
                left.AppendLine("Passenger Pos XYZ: " + PosXYZ(seat.Passenger.Pos));
                left.AppendLine("Passenger Pos RYP: " + PosRot(seat.Passenger.Pos));
            }

            right.AppendLine("--- Mount Rotation (degrees) ---");
            right.AppendLine("Base mount rotation: " + Vec(baseRotation));
            right.AppendLine("Config MountRotation: " + Vec(mountRotation));
            right.AppendLine("Player tilt setting: " + F(isLeft ? RatlineClimbDebugSettings.LeftPlayerTiltDegrees : RatlineClimbDebugSettings.RightPlayerTiltDegrees));
            right.AppendLine("Player yaw setting: " + F((isLeft ? 1f : -1f) * RatlineClimbDebugSettings.PlayerRotationDegrees));
            right.AppendLine("Player lean setting: " + F(isLeft ? RatlineClimbDebugSettings.LeftPlayerLeanDegrees : -RatlineClimbDebugSettings.RightPlayerLeanDegrees));
            right.AppendLine("Debug seat rotation: " + Vec(debugSeatRotation) + " (not applied)");
            right.AppendLine("Debug angle mode: " + debugAngleMode + " (not applied)");
            right.AppendLine("Patched player roll/yaw: removed");
            right.AppendLine("Debug eye offset: " + Vec(debugEyeOffset) + " (not applied)");
            right.AppendLine("Debug model offset: " + Vec(debugModelOffset));
            right.AppendLine("Debug model rotation: " + Vec(debugModelRotation));

            right.AppendLine();
            right.AppendLine("--- Sway Rotation Inputs ---");
            right.AppendLine("override sway: " + RatlineClimbDebugSettings.OverrideBoatSway);
            right.AppendLine("debug sway deg: ("
                + F(RatlineClimbDebugSettings.BoatSwayXDegrees) + ", "
                + F(RatlineClimbDebugSettings.BoatSwayYDegrees) + ", "
                + F(RatlineClimbDebugSettings.BoatSwayZDegrees) + ", swivel "
                + F(RatlineClimbDebugSettings.BoatSwivelDegrees) + ")");
            right.AppendLine("override yaw: " + RatlineClimbDebugSettings.OverrideBoatYaw
                + " -> " + F(RatlineClimbDebugSettings.BoatYawDegrees) + " deg");
            right.AppendLine("mountAngle: " + VecRad(mountAngle));
            right.AppendLine("direct FB source: " + Deg(frontBackSourcePitch));
            right.AppendLine("direct FB side sign: " + F(GetRatlinePlayerFrontBackSideSign(seat)));
            right.AppendLine("direct FB invert: " + RatlineClimbDebugSettings.InvertPlayerFrontBackSway);
            right.AppendLine("direct FB applied: " + Deg(frontBackAppliedPitch));
            if (renderer != null)
            {
                right.AppendLine("renderer xangle: " + Deg(renderer.xangle));
                right.AppendLine("renderer yangle: " + Deg(renderer.yangle));
                right.AppendLine("renderer zangle: " + Deg(renderer.zangle));
                right.AppendLine("renderer swivel: " + Deg(renderer.nowSwivelRad));
            }

            right.AppendLine();
            right.AppendLine("--- Basis / Matrix Pipeline ---");
            if (modelFrame == null)
            {
                right.AppendLine("Model frame: unavailable");
            }
            else
            {
                right.AppendLine("Path start: " + Vec(modelFrame.StartPosition));
                right.AppendLine("Path end: " + Vec(modelFrame.EndPosition));
                right.AppendLine("Mast base: " + Vec(modelFrame.MastBasePosition));
                right.AppendLine("Path side/up/fwd: " + Vec(modelFrame.PathSide) + " / " + Vec(modelFrame.PathUp) + " / " + Vec(modelFrame.PathForward));
                right.AppendLine("Seat axes: " + MatrixAxes(modelFrame.SeatWorldMatrix));
                right.AppendLine("Path axes: " + MatrixAxes(modelFrame.PathWorldMatrix));
                right.AppendLine("Correction axes: " + MatrixAxes(modelFrame.ModelCorrectionMatrix));
                right.AppendLine("Render local axes: " + MatrixAxes(modelFrame.RenderLocalMatrix));
            }

            right.AppendLine();
            right.AppendLine("--- Legacy Path Rotation Diagnostics ---");
            right.AppendLine("Cached path rot: " + VecRad(cachedPathRotation));
            right.AppendLine("Live path rot: " + VecRad(livePathRotation));
            right.AppendLine("Not applied to SeatPosition in direct-lock mode.");
            right.AppendLine("Direct FB roll setting: " + Deg(seatFrontBackRoll));

            right.AppendLine();
            right.AppendLine("--- Eye / Model Operations ---");
            right.AppendLine("SeatPosition XYZ is direct world path lock.");
            right.AppendLine("SeatPosition yaw: boat yaw + config yaw.");
            right.AppendLine("SeatPosition roll/pitch are zeroed.");
            right.AppendLine("LocalEyePos: vanilla/base seat eye position.");
            right.AppendLine("RenderTransform: model offset + path frame * model correction * inverse(seat frame).");
            right.AppendLine("Old seat/eye/direct-FB settings are not applied.");
        }

        private void UpdateRatlineWorldPositionCache(EntityBoatSeat seat)
        {
            string seatKey = GetRatlineSeatKey(seat);
            if (seatKey == null)
            {
                return;
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
            return GetBoatAxis(new Vec4d(1.0, 0.0, 0.0, 0.0), true) ?? Vec3d.Zero;
        }

        private Vec3d GetRenderedBoatUpAxis()
        {
            return GetBoatAxis(new Vec4d(0.0, 1.0, 0.0, 0.0), true);
        }

        private Vec3d GetRenderedBoatZAxis()
        {
            return GetBoatAxis(new Vec4d(0.0, 0.0, 1.0, 0.0), true) ?? Vec3d.Zero;
        }

        private Vec3d GetStaticBoatXAxis()
        {
            return GetBoatAxis(new Vec4d(1.0, 0.0, 0.0, 0.0), false) ?? Vec3d.Zero;
        }

        private Vec3d GetStaticBoatUpAxis()
        {
            return GetBoatAxis(new Vec4d(0.0, 1.0, 0.0, 0.0), false);
        }

        private Vec3d GetStaticBoatZAxis()
        {
            return GetBoatAxis(new Vec4d(0.0, 0.0, 1.0, 0.0), false) ?? Vec3d.Zero;
        }

        private Vec3d GetBoatAxis(Vec4d modelAxis, bool includeSway)
        {
            Matrixf transform = new Matrixf();
            transform.Identity();
            float yaw = RatlineClimbDebugSettings.OverrideBoatYaw
                ? RatlineClimbDebugSettings.BoatYawDegrees * GameMath.DEG2RAD
                : Pos.Yaw;
            transform.RotateY((float)Math.PI / 2f + yaw);

            EntityShapeRenderer entityShapeRenderer = base.Properties?.Client?.Renderer as EntityShapeRenderer;
            if (includeSway && entityShapeRenderer != null)
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
            DrawRatlineBasisDebugLines(capi, seat);
        }

        private void DrawRatlineBasisDebugLines(ICoreClientAPI capi, EntityBoatSeat seat)
        {
            RatlineModelFrame frame = GetRatlineModelFrame(seat);
            if (frame == null)
            {
                return;
            }

            Vec3d origin = frame.StartPosition;
            DrawDebugLine(capi, origin, origin + frame.PathSide * RatlineBasisDebugLineLength, ColorUtil.ColorFromRgba(255, 96, 255, 255));
            DrawDebugLine(capi, origin, origin + frame.PathUp * RatlineBasisDebugLineLength, ColorUtil.ColorFromRgba(96, 255, 96, 255));
            DrawDebugLine(capi, origin, origin + frame.PathForward * RatlineBasisDebugLineLength, ColorUtil.ColorFromRgba(96, 255, 255, 255));
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
            return GetRatlinePathRotationRelativeToSeatYaw(seat, pathDirection);
        }

        private Vec3f GetRatlinePathRotationRelativeToSeatYaw(EntityBoatSeat seat, Vec3d pathDirection)
        {
            float yaw = GetRatlineSeatYaw(seat);
            Vec3f forwardVec = EntityPos.GetViewVector(0f, yaw);
            Vec3f rightVec = EntityPos.GetViewVector(0f, yaw + GameMath.PIHALF);
            Vec3d forward = new Vec3d(forwardVec.X, forwardVec.Y, forwardVec.Z);
            Vec3d right = new Vec3d(rightVec.X, rightVec.Y, rightVec.Z);
            forward.Y = 0.0;
            right.Y = 0.0;

            double forwardLength = forward.Length();
            double rightLength = right.Length();
            if (forwardLength <= 0.0001 || rightLength <= 0.0001)
            {
                return new Vec3f();
            }

            forward.Mul(1.0 / forwardLength);
            right.Mul(1.0 / rightLength);

            double upright = pathDirection.Y;
            double forwardTilt = Dot(pathDirection, forward);
            double rightTilt = Dot(pathDirection, right);
            double pitch = Math.Atan2(forwardTilt, upright);
            double roll = -Math.Atan2(rightTilt, upright);
            return new Vec3f((float)roll, 0f, (float)pitch);
        }

        private float GetRatlineClimbHeight(EntityBoatSeat seat)
        {
            string seatKey = GetRatlineSeatKey(seat);
            if (seatKey != null && ratlineClimbBySeat.TryGetValue(seatKey, out float climbHeight))
            {
                return climbHeight;
            }

            return RatlineClimbMinHeight;
        }

        private Vec3d TryGetRatlineSeatWorldPosition(EntityBoatSeat seat)
        {
            try
            {
                return GetRatlineSeatWorldPosition(seat);
            }
            catch (Exception ex)
            {
                WarnRatlineClimbUpdateFailed(ex);
                return null;
            }
        }

        private Vec3d GetRatlineSeatWorldPosition(EntityBoatSeat seat)
        {
            Vec3d pathPosition = GetRatlineTriangleWorldPositionAt(seat, GetRatlineClimbHeight(seat));
            if (pathPosition == null)
            {
                return null;
            }

            if (!RatlineClimbDebugSettings.EnablePlayerOffset)
            {
                return pathPosition;
            }

            return pathPosition + GetRatlinePlayerWorldOffset(seat);
        }

        private Vec3d GetRatlinePlayerWorldOffset(EntityBoatSeat seat)
        {
            Vec3f localOffset = GetRatlinePlayerOffset(seat);
            Vec3d xAxis = GetRenderedBoatXAxis();
            Vec3d yAxis = GetRenderedBoatUpAxis();
            Vec3d zAxis = GetRenderedBoatZAxis();
            if (xAxis == null || yAxis == null || zAxis == null)
            {
                return Vec3d.Zero;
            }

            return xAxis * localOffset.X + yAxis * localOffset.Y + zAxis * localOffset.Z;
        }

        private float GetRatlineSeatYaw(EntityBoatSeat seat)
        {
            float yaw = RatlineClimbDebugSettings.OverrideBoatYaw
                ? RatlineClimbDebugSettings.BoatYawDegrees * GameMath.DEG2RAD
                : Pos.Yaw;

            if (seat.Config?.MountRotation != null)
            {
                yaw += seat.Config.MountRotation.Y * GameMath.DEG2RAD;
            }

            return yaw;
        }

        private static double NormalizeAngleRad(double angle)
        {
            while (angle > Math.PI)
            {
                angle -= Math.PI * 2.0;
            }

            while (angle < -Math.PI)
            {
                angle += Math.PI * 2.0;
            }

            return angle;
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
                string code = behaviorSelectionBoxes.selectionBoxes[i]?.AttachPoint?.Code;
                if (string.Equals(code, attachmentPointCode, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        return GetSelectionBoxCenter(behaviorSelectionBoxes.selectionBoxes[i]);
                    }
                    catch (Exception ex)
                    {
                        WarnRatlineClimbUpdateFailed(ex);
                        return null;
                    }
                }
            }

            return null;
        }

        private Vec3d GetSelectionBoxCenter(AttachmentPointAndPose apap)
        {
            if (apap?.AttachPoint?.ParentElement == null)
            {
                return null;
            }

            Matrixf transform = new Matrixf();
            transform.Identity();
            ApplySelectionBoxTransform(transform, apap);

            ShapeElement parentElement = apap.AttachPoint.ParentElement;
            Vec4d boxCenter = new Vec4d(
                (parentElement.To[0] - parentElement.From[0]) / 2.0 / RatlineClimbDebugSettings.ModelUnitsPerBlock,
                (parentElement.To[1] - parentElement.From[1]) / 2.0 / RatlineClimbDebugSettings.ModelUnitsPerBlock,
                (parentElement.To[2] - parentElement.From[2]) / 2.0 / RatlineClimbDebugSettings.ModelUnitsPerBlock,
                1.0
            );

            return transform.TransformVector(boxCenter).XYZ.Add(Pos.XYZ);
        }

        private void ApplySelectionBoxTransform(Matrixf transform, AttachmentPointAndPose apap)
        {
            EntityShapeRenderer renderer = base.Properties?.Client?.Renderer as EntityShapeRenderer;
            transform.RotateY(GameMath.PIHALF + Pos.Yaw);
            if (renderer != null)
            {
                transform.Translate(0f, (float)SelectionBox.Y2 / 2f, 0f);
                transform.RotateX(renderer.xangle);
                transform.RotateY(renderer.yangle);
                transform.RotateZ(renderer.zangle);
                transform.Translate(0f, (float)-SelectionBox.Y2 / 2f, 0f);
            }

            transform.Translate(0f, 0.7f, 0f);
            transform.RotateX(renderer?.nowSwivelRad ?? 0f);
            transform.Translate(0f, -0.7f, 0f);

            float size = base.Properties?.Client?.Size ?? 1f;
            transform.Scale(size, size, size);
            transform.Translate(-0.5f, 0f, -0.5f);
            transform.Mul(apap.AnimModelMatrix);

            ShapeElement parentElement = apap.AttachPoint.ParentElement;
            transform.Scale(
                (float)(parentElement.To[0] - parentElement.From[0]) / RatlineClimbDebugSettings.ModelUnitsPerBlock,
                (float)(parentElement.To[1] - parentElement.From[1]) / RatlineClimbDebugSettings.ModelUnitsPerBlock,
                (float)(parentElement.To[2] - parentElement.From[2]) / RatlineClimbDebugSettings.ModelUnitsPerBlock
            );
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

        private Vec3f GetRatlineSwayLocalOffset(EntityBoatSeat seat, Vec3f pathPoint)
        {
            Vec3d staticXAxis = GetStaticBoatXAxis();
            Vec3d staticYAxis = GetStaticBoatUpAxis();
            Vec3d staticZAxis = GetStaticBoatZAxis();
            Vec3d renderedXAxis = GetRenderedBoatXAxis();
            Vec3d renderedYAxis = GetRenderedBoatUpAxis();
            Vec3d renderedZAxis = GetRenderedBoatZAxis();
            if (staticXAxis == null || staticYAxis == null || staticZAxis == null
                || renderedXAxis == null || renderedYAxis == null || renderedZAxis == null)
            {
                return new Vec3f();
            }

            double x = (pathPoint.X - RatlineClimbDebugSettings.DefaultPathX) / RatlineClimbDebugSettings.ModelUnitsPerBlock;
            double y = pathPoint.Y / RatlineClimbDebugSettings.ModelUnitsPerBlock;
            double z = pathPoint.Z / RatlineClimbDebugSettings.ModelUnitsPerBlock;
            Vec3d swayDelta =
                (renderedXAxis - staticXAxis) * x
                + (renderedYAxis - staticYAxis) * y
                + (renderedZAxis - staticZAxis) * z;

            return new Vec3f(
                (float)Dot(swayDelta, staticXAxis),
                (float)Dot(swayDelta, staticYAxis),
                (float)Dot(swayDelta, staticZAxis)
            );
        }

        private Vec3f GetRatlineAnchorPoint(EntityBoatSeat seat)
        {
            Vec3d lowerMastPos = GetSelectionBoxCenter(LowerMastAttachmentPoint);
            Vec3d ratlinePos = GetSelectionBoxCenter(IsLeftRatlineSeat(seat) ? LeftRatlineAttachmentPoint : RightRatlineAttachmentPoint);
            Vec3d xAxis = GetStaticBoatXAxis();
            Vec3d yAxis = GetStaticBoatUpAxis();
            Vec3d zAxis = GetStaticBoatZAxis();
            if (lowerMastPos == null || ratlinePos == null || xAxis == null || yAxis == null || zAxis == null)
            {
                return null;
            }

            Vec3d delta = ratlinePos - lowerMastPos;
            return new Vec3f(
                RatlineClimbDebugSettings.DefaultPathX + (float)Dot(delta, xAxis) * RatlineClimbDebugSettings.ModelUnitsPerBlock,
                (float)Dot(delta, yAxis) * RatlineClimbDebugSettings.ModelUnitsPerBlock,
                (float)Dot(delta, zAxis) * RatlineClimbDebugSettings.ModelUnitsPerBlock
            );
        }

        private static Vec3f GetAssetRatlineAnchorPoint(EntityBoatSeat seat)
        {
            return new Vec3f(
                RatlineClimbDebugSettings.AssetPathX,
                RatlineClimbDebugSettings.AssetStartY,
                GetAssetRatlinePathZ(seat)
            );
        }

        private void WarnRatlineClimbUpdateFailed(Exception ex)
        {
            long elapsedMs = World?.ElapsedMilliseconds ?? 0L;
            if (elapsedMs < nextRatlineClimbWarningMs)
            {
                return;
            }

            nextRatlineClimbWarningMs = elapsedMs + 5000L;
            Api?.Logger?.Warning("Joy of Sailing ratline climb update fell back: {0}", ex);
        }

        private static double Dot(Vec3d a, Vec3d b)
        {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }

        private static Vec3d Cross(Vec3d a, Vec3d b)
        {
            return new Vec3d(
                a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X
            );
        }

        private static Vec3d Normalize(Vec3d vector)
        {
            if (vector == null)
            {
                return null;
            }

            double length = vector.Length();
            if (length <= 0.0001)
            {
                return null;
            }

            return new Vec3d(vector.X / length, vector.Y / length, vector.Z / length);
        }

        private static Vec3d ProjectPerpendicular(Vec3d vector, Vec3d normal)
        {
            return vector - normal * Dot(vector, normal);
        }

        private static Matrixf BuildBasisMatrix(Vec3d xAxis, Vec3d yAxis, Vec3d zAxis)
        {
            float[] values = Mat4f.Create();
            values[0] = (float)xAxis.X;
            values[1] = (float)xAxis.Y;
            values[2] = (float)xAxis.Z;
            values[3] = 0f;

            values[4] = (float)yAxis.X;
            values[5] = (float)yAxis.Y;
            values[6] = (float)yAxis.Z;
            values[7] = 0f;

            values[8] = (float)zAxis.X;
            values[9] = (float)zAxis.Y;
            values[10] = (float)zAxis.Z;
            values[11] = 0f;

            values[12] = 0f;
            values[13] = 0f;
            values[14] = 0f;
            values[15] = 1f;
            return new Matrixf(values);
        }

        private static Matrixf BuildSeatYawWorldMatrix(float yaw)
        {
            Matrixf matrix = new Matrixf();
            matrix.Identity();
            matrix.RotateY(yaw);
            return matrix;
        }

        private static Vec3d GetMatrixAxis(Matrixf matrix, int column)
        {
            int index = column * 4;
            return new Vec3d(matrix.Values[index], matrix.Values[index + 1], matrix.Values[index + 2]);
        }

        private Matrixf BuildRatlineModelCorrectionMatrix(EntityBoatSeat seat)
        {
            Matrixf correction = new Matrixf();
            correction.Identity();
            correction.RotateDeg(GetRatlineMountRotation(seat, GetRatlineBaseMountRotation(seat)));
            correction.RotateDeg(GetDebugModelRotationDegrees());
            return correction;
        }

        private RatlineModelFrame GetRatlineModelFrame(EntityBoatSeat seat)
        {
            Vec3d startPos = GetRatlineTriangleWorldPositionAt(seat, RatlineClimbMinHeight);
            Vec3d endPos = GetRatlineTriangleWorldPositionAt(seat, RatlineClimbDebugSettings.MaxClimbHeight);
            Vec3d mastBase = GetSelectionBoxCenter(LowerMastAttachmentPoint);
            if (startPos == null || endPos == null || mastBase == null)
            {
                return null;
            }

            Vec3d pathUp = Normalize(endPos - startPos);
            if (pathUp == null)
            {
                return null;
            }

            Vec3d pathSide = Normalize(ProjectPerpendicular(startPos - mastBase, pathUp));
            if (pathSide == null)
            {
                Vec3d renderedZ = GetRenderedBoatZAxis();
                if (renderedZ == null)
                {
                    return null;
                }

                pathSide = Normalize(ProjectPerpendicular(renderedZ * (IsLeftRatlineSeat(seat) ? 1.0 : -1.0), pathUp));
            }

            if (pathSide == null)
            {
                return null;
            }

            Vec3d pathForward = Normalize(Cross(pathSide, pathUp));
            if (pathForward == null)
            {
                return null;
            }

            pathSide = Normalize(Cross(pathUp, pathForward));
            if (pathSide == null)
            {
                return null;
            }

            Matrixf pathWorldMatrix = BuildBasisMatrix(pathSide, pathUp, pathForward);
            Matrixf modelCorrectionMatrix = BuildRatlineModelCorrectionMatrix(seat);
            Matrixf desiredWorldMatrix = pathWorldMatrix.Clone().Mul(modelCorrectionMatrix);

            float seatYaw = GetRatlineSeatYaw(seat);
            Matrixf seatWorldMatrix = BuildSeatYawWorldMatrix(seatYaw);
            Matrixf renderLocalMatrix = desiredWorldMatrix.Clone().Mul(seatWorldMatrix.Clone().Invert());

            return new RatlineModelFrame
            {
                StartPosition = startPos,
                EndPosition = endPos,
                MastBasePosition = mastBase,
                PathSide = pathSide,
                PathUp = pathUp,
                PathForward = pathForward,
                PathWorldMatrix = pathWorldMatrix,
                ModelCorrectionMatrix = modelCorrectionMatrix,
                DesiredWorldMatrix = desiredWorldMatrix,
                SeatWorldMatrix = seatWorldMatrix,
                RenderLocalMatrix = renderLocalMatrix
            };
        }

        private Matrixf GetRatlineRenderLocalModelMatrix(EntityBoatSeat seat)
        {
            return GetRatlineModelFrame(seat)?.RenderLocalMatrix;
        }

        private class RatlineModelFrame
        {
            public Vec3d StartPosition;
            public Vec3d EndPosition;
            public Vec3d MastBasePosition;
            public Vec3d PathSide;
            public Vec3d PathUp;
            public Vec3d PathForward;
            public Matrixf PathWorldMatrix;
            public Matrixf ModelCorrectionMatrix;
            public Matrixf DesiredWorldMatrix;
            public Matrixf SeatWorldMatrix;
            public Matrixf RenderLocalMatrix;
        }

        private static Vec3f GetRatlineBaseRiderOffset(EntityBoatSeat seat)
        {
            return Copy(RatlineBaseRiderOffset);
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

        private static string F(float value)
        {
            return value.ToString("0.000", CultureInfo.InvariantCulture);
        }

        private static string F(double value)
        {
            return value.ToString("0.000", CultureInfo.InvariantCulture);
        }

        private static string Deg(float radians)
        {
            return F(radians * GameMath.RAD2DEG) + " deg";
        }

        private static string Vec(Vec3f value)
        {
            return value == null ? "(null)" : "(" + F(value.X) + ", " + F(value.Y) + ", " + F(value.Z) + ")";
        }

        private static string Vec(Vec3d value)
        {
            return value == null ? "(null)" : "(" + F(value.X) + ", " + F(value.Y) + ", " + F(value.Z) + ")";
        }

        private static string VecRad(Vec3f value)
        {
            return value == null
                ? "(null)"
                : "(" + Deg(value.X) + ", " + Deg(value.Y) + ", " + Deg(value.Z) + ")";
        }

        private static string MatrixAxes(Matrixf value)
        {
            return value == null
                ? "(null)"
                : "X" + Vec(GetMatrixAxis(value, 0)) + " Y" + Vec(GetMatrixAxis(value, 1)) + " Z" + Vec(GetMatrixAxis(value, 2));
        }

        private static string PosXYZ(EntityPos pos)
        {
            return pos == null ? "(null)" : "(" + F(pos.X) + ", " + F(pos.Y) + ", " + F(pos.Z) + ")";
        }

        private static string PosRot(EntityPos pos)
        {
            return pos == null
                ? "(null)"
                : "(roll " + Deg(pos.Roll) + ", yaw " + Deg(pos.Yaw) + ", pitch " + Deg(pos.Pitch) + ")";
        }

        private static bool IsLeftRatlineSeat(EntityBoatSeat seat)
        {
            SeatConfig config = seat.Config;
            return string.Equals(config?.APName, LeftRatlineAttachmentPoint, StringComparison.OrdinalIgnoreCase)
                || string.Equals(config?.SelectionBox, LeftRatlineAttachmentPoint, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsRatlineMount(IMountableSeat seat)
        {
            return seat is EntityBoatSeat boatSeat && IsRatlineSeat(boatSeat);
        }

        private class EntitySailboatSeat : EntityBoatSeat
        {
            public EntitySailboatSeat(IMountable mountablesupplier, string seatId, SeatConfig config)
                : base(mountablesupplier, seatId, config)
            {
            }

            public override EnumMountAngleMode AngleMode
            {
                get
                {
                    return base.AngleMode;
                }
            }

            public override EntityPos SeatPosition
            {
                get
                {
                    if (!IsRatlineSeat(this) || Entity is not EntitySailboat sailboat)
                    {
                        return base.SeatPosition;
                    }

                    Vec3d worldPosition = sailboat.TryGetRatlineSeatWorldPosition(this);
                    if (worldPosition == null)
                    {
                        return base.SeatPosition;
                    }

                    seatPos.SetFrom(Entity.Pos);
                    seatPos.X = worldPosition.X;
                    seatPos.Y = worldPosition.Y;
                    seatPos.Z = worldPosition.Z;
                    seatPos.Roll = 0f;
                    seatPos.Yaw = sailboat.GetRatlineSeatYaw(this);
                    seatPos.Pitch = 0f;

                    return seatPos;
                }
            }

            public override Vec3f LocalEyePos
            {
                get
                {
                    if (!IsRatlineSeat(this))
                    {
                        return base.LocalEyePos;
                    }

                    return base.LocalEyePos;
                }
            }

            public override Matrixf RenderTransform
            {
                get
                {
                    if (!IsRatlineSeat(this) || Config?.MountRotation == null || Entity is not EntitySailboat sailboat)
                    {
                        return base.RenderTransform;
                    }

                    Matrixf transform = new Matrixf();
                    transform.Identity();

                    Entity passenger = Passenger;
                    float passengerSize = passenger?.Properties?.Client?.Size ?? 1f;
                    Vec3f riderOffset = GetSeatOffset(Config);
                    if (riderOffset != null && passengerSize != 1f)
                    {
                        transform.Translate(Copy(riderOffset).Mul(passengerSize - 1f));
                    }

                    transform.Translate(GetDebugModelOffset());
                    transform.Mul(sailboat.GetRatlineRenderLocalModelMatrix(this) ?? BuildRatlineModelCorrectionFallback(this));
                    return transform;
                }
            }

            private static Matrixf BuildRatlineModelCorrectionFallback(EntityBoatSeat seat)
            {
                Matrixf transform = new Matrixf();
                transform.Identity();
                transform.RotateDeg(GetRatlineMountRotation(seat, GetRatlineBaseMountRotation(seat)));
                transform.RotateDeg(GetDebugModelRotationDegrees());
                return transform;
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

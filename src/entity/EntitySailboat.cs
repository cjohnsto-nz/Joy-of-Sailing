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
    public class EntitySailboat : EntityBoat
    {
        // WORLDCONFIG OPTIONS
        // joyofsailing.minwindspeed : adjusts the minimum wind speed
        // joyofsailing.sailspeedmul : multiplies the speed of all sailboats when sailing
        // joyofsailing.scullspeedmul : multiplies the sculling speed of all sailboats

        const string LeftRatlineAttachmentPoint = "RatlineLAP";
        const string RightRatlineAttachmentPoint = "RatlineRAP";
        const float RatlineClimbMinHeight = 0f;
        static readonly System.Reflection.FieldInfo RiderOffsetField = typeof(SeatConfig).GetField("RiderOffset");
        static readonly Vec3f LeftRatlineBaseRiderOffset = new Vec3f(0f, 0f, 0.375f);
        static readonly Vec3f RightRatlineBaseRiderOffset = new Vec3f(-0.0625f, 0.0625f, -0.375f);
        static readonly Vec3f LeftRatlineBaseMountRotation = new Vec3f(15f, -85f, 4f);
        static readonly Vec3f RightRatlineBaseMountRotation = new Vec3f(-15f, 85f, 4f);

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
        RatlineDebugRenderer ratlineDebugRenderer;

        float autoScullTimer = 0f;
        float autoScullEnableTimer = 0f;
        bool isAutoSculling = false;
        bool canDisableAutoSculling = false;

        private float curRotMountAngleZ;

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
                    UpdateRatlineClimb(entityBoatSeat, climbDt);
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

        private void UpdateRatlineClimb(EntityBoatSeat seat, float dt)
        {
            SeatConfig config = seat.Config;
            if (config == null)
            {
                return;
            }

            string seatKey = GetRatlineSeatKey(seat);
            if (seatKey == null)
            {
                return;
            }

            float climbHeight = ratlineClimbBySeat.TryGetValue(seatKey, out float value) ? value : 0f;
            EntityControls controls = seat.controls;
            if (controls != null && (controls.Forward ^ controls.Backward))
            {
                climbHeight += (controls.Forward ? 1f : -1f) * RatlineClimbDebugSettings.Speed * dt;
                climbHeight = GameMath.Clamp(climbHeight, RatlineClimbMinHeight, RatlineClimbDebugSettings.MaxClimbHeight);
            }

            ratlineClimbBySeat[seatKey] = climbHeight;

            ApplyRatlineClimbTransform(seat, GetRatlineBaseRiderOffset(seat), GetRatlineBaseMountRotation(seat), climbHeight);
        }

        private static Vec3f GetRatlinePathOffset(EntityBoatSeat seat, float climbHeight)
        {
            float pathZ = GetRatlinePathZ(seat);
            Vec3f offset = new Vec3f(
                (RatlineClimbDebugSettings.PathX - RatlineClimbDebugSettings.AssetPathX) / RatlineClimbDebugSettings.ModelUnitsPerBlock,
                (RatlineClimbDebugSettings.StartY - RatlineClimbDebugSettings.AssetStartY) / RatlineClimbDebugSettings.ModelUnitsPerBlock,
                (pathZ - GetAssetRatlinePathZ(seat)) / RatlineClimbDebugSettings.ModelUnitsPerBlock
            );

            offset.Add(GetRatlineClimbVector(seat, climbHeight));
            return offset;
        }

        private static Vec3f GetRatlineClimbVector(EntityBoatSeat seat, float climbHeight)
        {
            float maxHeight = RatlineClimbDebugSettings.MaxClimbHeight;
            if (maxHeight <= 0f)
            {
                return new Vec3f();
            }

            float side = IsLeftRatlineSeat(seat) ? 1f : -1f;
            float progress = GameMath.Clamp(climbHeight / maxHeight, 0f, 1f);
            Vec3f climbVector = new Vec3f(0f, (RatlineClimbDebugSettings.EndY - RatlineClimbDebugSettings.StartY) * progress, 0f);
            RotateModelVectorX(climbVector, side * RatlineClimbDebugSettings.TiltDegrees);
            RotateModelVectorY(climbVector, side * RatlineClimbDebugSettings.LeanDegrees);
            climbVector.Mul(1f / RatlineClimbDebugSettings.ModelUnitsPerBlock);
            return climbVector;
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
            float side = IsLeftRatlineSeat(seat) ? 1f : -1f;
            Vec3f rotation = Copy(baseRotation) ?? new Vec3f();
            rotation.X += side * RatlineClimbDebugSettings.TiltDegrees;
            rotation.Y += side * RatlineClimbDebugSettings.LeanDegrees;
            return rotation;
        }

        private static void ApplyRatlineClimbTransform(EntityBoatSeat seat, Vec3f baseOffset, Vec3f baseRotation, float climbHeight)
        {
            Vec3f climbOffset = Copy(baseOffset) ?? new Vec3f();
            climbOffset.Add(GetRatlinePathOffset(seat, climbHeight));
            SetSeatOffset(seat.Config, climbOffset);
            seat.Config.MountRotation = GetRatlineMountRotation(seat, baseRotation);
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
            if (!RatlineClimbDebugSettings.DrawPath)
            {
                return;
            }

            EntityBehaviorSeatable behavior = GetBehavior<EntityBehaviorSeatable>();
            if (behavior?.Seats == null)
            {
                return;
            }

            foreach (IMountableSeat mountableSeat in behavior.Seats)
            {
                EntityBoatSeat seat = mountableSeat as EntityBoatSeat;
                if (seat == null || !IsRatlineSeat(seat))
                {
                    continue;
                }

                DrawRatlineDebugPath(capi, seat, IsLeftRatlineSeat(seat)
                    ? ColorUtil.ColorFromRgba(255, 64, 64, 255)
                    : ColorUtil.ColorFromRgba(64, 160, 255, 255));
            }
        }

        private void DrawRatlineDebugPath(ICoreClientAPI capi, EntityBoatSeat seat, int color)
        {
            SeatConfig config = seat.Config;
            if (config == null)
            {
                return;
            }

            Vec3f currentRotation = Copy(config.MountRotation) ?? new Vec3f();
            Vec3f currentOffset = Copy(GetSeatOffset(config));
            Vec3f baseOffset = GetRatlineBaseRiderOffset(seat);
            Vec3f baseRotation = GetRatlineBaseMountRotation(seat);
            EntityPos startPos = GetRatlineSeatPositionAt(seat, baseOffset, baseRotation, RatlineClimbMinHeight);
            EntityPos endPos = GetRatlineSeatPositionAt(seat, baseOffset, baseRotation, RatlineClimbDebugSettings.MaxClimbHeight);
            SetSeatOffset(config, currentOffset);
            config.MountRotation = currentRotation;

            BlockPos origin = Pos.AsBlockPos;
            capi.Render.RenderLine(
                origin,
                (float)(startPos.X - origin.X), (float)(startPos.Y - origin.Y), (float)(startPos.Z - origin.Z),
                (float)(endPos.X - origin.X), (float)(endPos.Y - origin.Y), (float)(endPos.Z - origin.Z),
                color
            );
        }

        private EntityPos GetRatlineSeatPositionAt(EntityBoatSeat seat, Vec3f baseOffset, Vec3f baseRotation, float climbHeight)
        {
            ApplyRatlineClimbTransform(seat, baseOffset, baseRotation, climbHeight);
            return seat.SeatPosition.Copy();
        }

        private static Vec3f GetRatlineBaseRiderOffset(EntityBoatSeat seat)
        {
            return Copy(IsLeftRatlineSeat(seat) ? LeftRatlineBaseRiderOffset : RightRatlineBaseRiderOffset);
        }

        private static Vec3f GetRatlineBaseMountRotation(EntityBoatSeat seat)
        {
            return Copy(IsLeftRatlineSeat(seat) ? LeftRatlineBaseMountRotation : RightRatlineBaseMountRotation);
        }

        private static float GetRatlinePathZ(EntityBoatSeat seat)
        {
            return IsLeftRatlineSeat(seat) ? RatlineClimbDebugSettings.LeftPathZ : RatlineClimbDebugSettings.RightPathZ;
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

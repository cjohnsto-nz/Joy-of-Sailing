using System;
using Vintagestory.API.Client;

namespace joyofsailing
{
    public class RatlineDebugRenderer : IRenderer
    {
        private readonly ICoreClientAPI capi;
        private readonly EntitySailboat sailboat;

        public double RenderOrder => 1.0;
        public int RenderRange => 999;

        public RatlineDebugRenderer(ICoreClientAPI capi, EntitySailboat sailboat)
        {
            this.capi = capi;
            this.sailboat = sailboat;
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            sailboat.DrawRatlineDebugPaths(capi);
        }

        public void Dispose()
        {
        }
    }
}

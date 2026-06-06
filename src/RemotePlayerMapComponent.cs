using System;
using System.Text;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ResonanceTracker
{
    public class RemotePlayerMapComponent : MapComponent
    {
        public readonly Entity PlayerEntity;
        private readonly LoadedTexture texture;
        private readonly MeshRef quadModel;
        private Vec2f viewPos = new Vec2f();
        private readonly Matrixf mvMat = new Matrixf();

        public RemotePlayerMapComponent(ICoreClientAPI capi, LoadedTexture texture, Entity entity) : base(capi)
        {
            this.PlayerEntity = entity;
            this.texture = texture;
            this.quadModel = capi.Render.UploadMesh(QuadMeshUtil.GetQuad());
        }

        public override void Render(GuiElementMap map, float dt)
        {
            if (texture.Disposed || quadModel.Disposed) return;

            map.TranslateWorldPosToViewPos(PlayerEntity.Pos.XYZ, ref viewPos);
            float num = (float)(map.Bounds.renderX + (double)viewPos.X);
            float num2 = (float)(map.Bounds.renderY + (double)viewPos.Y);
            
            capi.Render.GlToggleBlend(true, (EnumBlendMode)0);
            IShaderProgram engineShader = capi.Render.GetEngineShader((EnumShaderProgram)17);
            
            engineShader.Uniform("rgbaIn", ColorUtil.WhiteArgbVec);
            engineShader.Uniform("applyColor", 0);
            engineShader.Uniform("extraGlow", 0);
            engineShader.Uniform("noTexture", 0f);
            engineShader.BindTexture2D("tex2d", texture.TextureId, 0);
            
            mvMat.Set(capi.Render.CurrentModelviewMatrix)
                .Translate(num, num2, 60f)
                .Scale((float)texture.Width, (float)texture.Height, 0f)
                .Scale(0.5f, 0.5f, 0f)
                .RotateZ(0f - PlayerEntity.Pos.Yaw + (float)Math.PI);
                
            engineShader.UniformMatrix("projectionMatrix", capi.Render.CurrentProjectionMatrix);
            engineShader.UniformMatrix("modelViewMatrix", mvMat.Values);
            capi.Render.RenderMesh(quadModel);
        }

        public override void OnMouseMove(MouseEvent args, GuiElementMap mapElem, StringBuilder hoverText)
        {
            Vec2f val = new Vec2f();
            mapElem.TranslateWorldPosToViewPos(PlayerEntity.Pos.XYZ, ref val);
            double num = (double)args.X - mapElem.Bounds.renderX;
            double num2 = (double)args.Y - mapElem.Bounds.renderY;
            double num3 = GuiElement.scaled(5.0);
            if (Math.Abs(val.X - num) < num3 && Math.Abs(val.Y - num2) < num3)
            {
                if (PlayerEntity is EntityPlayer entityPlayer)
                {
                    IPlayer player = capi.World.PlayerByUid(entityPlayer.PlayerUID);
                    hoverText.AppendLine("Player " + (player != null ? player.PlayerName : "Unknown"));
                }
                else
                {
                    hoverText.AppendLine(PlayerEntity.GetName());
                }
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            quadModel.Dispose();
        }
    }
}

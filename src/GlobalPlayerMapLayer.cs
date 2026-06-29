using System;
using System.Collections.Generic;
using System.Text;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ResonanceTracker
{
    public class GlobalPlayerMapLayer : PlayerMapLayer
    {
        private readonly Dictionary<IPlayer, RemotePlayerMapComponent> MapComps = new Dictionary<IPlayer, RemotePlayerMapComponent>();
        private readonly ICoreClientAPI capi;
        private readonly Dictionary<string, LoadedTexture> textureCache = new Dictionary<string, LoadedTexture>();
        private readonly ResonanceTrackerModSystem modSystem;

        public override string Title => "Players";
        public override EnumMapAppSide DataSide => EnumMapAppSide.Client;
        public override string LayerGroupCode => "terrain";

        public GlobalPlayerMapLayer(ICoreAPI api, IWorldMapManager mapsink) : base(api, mapsink)
        {
            capi = api as ICoreClientAPI;
            if (capi != null)
            {
                modSystem = capi.ModLoader.GetModSystem<ResonanceTrackerModSystem>(true);
            }
        }

        private LoadedTexture GetOrCreateTexture(string fillColorHex, string borderColorHex)
        {
            string key = $"{fillColorHex}-{borderColorHex}";
            if (textureCache.TryGetValue(key, out var tex))
            {
                if (tex != null && !tex.Disposed) return tex;
            }

            int size = (int)GuiElement.scaled(32.0);
            using (var surface = new ImageSurface(Format.Argb32, size, size))
            {
                using (var ctx = new Context(surface))
                {
                    ctx.SetSourceRGBA(0, 0, 0, 0);
                    ctx.Paint();
                    
                    double[] fill = HexToRGBA(fillColorHex);
                    double[] border = HexToRGBA(borderColorHex);
                    capi.Gui.Icons.DrawMapPlayer(ctx, 0, 0, size, size, border, fill);
                    var newTex = new LoadedTexture(capi, capi.Gui.LoadCairoTexture(surface, false), size / 2, size / 2);
                    textureCache[key] = newTex;
                    return newTex;
                }
            }
        }

        private void GenerateEntityMapComponent(IPlayer player)
        {
            var entity = player.Entity as EntityPlayer;
            if (MapComps.TryGetValue(player, out var comp))
            {
                comp.Dispose();
                MapComps.Remove(player);
            }

            RemotePlayerMapComponent newComp;

            string fill = modSystem?.Config?.OtherFillColorHex ?? "#C0C0C0";
            string border = modSystem?.Config?.OtherBorderColorHex ?? "#4D4D4D";

            bool isMe = player.PlayerUID == capi.World.Player.PlayerUID;
            if (isMe)
            {
                fill = modSystem?.Config?.OwnFillColorHex ?? "#FFFFFF";
                border = modSystem?.Config?.OwnBorderColorHex ?? "#000000";
            }
            else if (modSystem != null && modSystem.Config != null && modSystem.Config.UsePlayerCustomColors && 
                     modSystem.RemotePlayerDict.TryGetValue(player.PlayerUID, out var remoteData) && 
                     !string.IsNullOrEmpty(remoteData.FillColorHex))
            {
                fill = remoteData.FillColorHex;
                border = remoteData.BorderColorHex;
            }

            var tex = GetOrCreateTexture(fill, border);

            if (entity == null)
            {
                if (modSystem == null || !modSystem.RemotePlayerDict.TryGetValue(player.PlayerUID, out var remoteData))
                {
                    return;
                }

                var entityType = capi.World.GetEntityType(new AssetLocation("player"));
                entity = capi.ClassRegistry.CreateEntity(entityType) as EntityPlayer;
                if (entity != null)
                {
                    entity.Initialize(entityType, capi, 0L);
                }
                else
                {
                    entity = new EntityPlayer();
                }

                entity.Pos.SetPos(remoteData.Position.X, remoteData.Position.Y, remoteData.Position.Z);
                entity.Pos.SetYaw(remoteData.Yaw);
                entity.WatchedAttributes.SetString("playerUID", player.PlayerUID);
                
                newComp = new RemotePlayerMapComponent(capi, tex, entity);
            }
            else
            {
                newComp = new RemotePlayerMapComponent(capi, tex, entity);
            }

            MapComps[player] = newComp;
        }

        private void Event_PlayerDespawn(IClientPlayer byPlayer)
        {
            if (capi == null) return;
            var mapHideOthers = capi.World.Config.GetBool("mapHideOtherPlayers", false);
            var isMe = byPlayer.PlayerUID == capi.World.Player.PlayerUID;

            if (!mapHideOthers || isMe)
            {
                GenerateEntityMapComponent(byPlayer);
            }
        }

        private void Event_PlayerSpawn(IClientPlayer byPlayer)
        {
            if (capi == null) return;
            var mapHideOthers = capi.World.Config.GetBool("mapHideOtherPlayers", false);
            var isMe = byPlayer.PlayerUID == capi.World.Player.PlayerUID;

            if ((!mapHideOthers || isMe) && mapSink.IsOpened)
            {
                GenerateEntityMapComponent(byPlayer);
            }
        }

        private void Event_PlayerJoin(IClientPlayer byPlayer)
        {
            if (capi == null) return;
            var mapHideOthers = capi.World.Config.GetBool("mapHideOtherPlayers", false);
            var isMe = byPlayer.PlayerUID == capi.World.Player.PlayerUID;

            if ((!mapHideOthers || isMe) && mapSink.IsOpened)
            {
                GenerateEntityMapComponent(byPlayer);
            }
        }

        private void Event_PlayerLeave(IClientPlayer byPlayer)
        {
            if (MapComps.TryGetValue(byPlayer, out var comp))
            {
                comp.Dispose();
                MapComps.Remove(byPlayer);
            }
        }

        public override void OnLoaded()
        {
            if (capi != null)
            {
                capi.Event.PlayerEntitySpawn += Event_PlayerSpawn;
                capi.Event.PlayerEntityDespawn += Event_PlayerDespawn;
                capi.Event.PlayerJoin += Event_PlayerJoin;
                capi.Event.PlayerLeave += Event_PlayerLeave;
            }
        }

        private double[] HexToRGBA(string hex)
        {
            try
            {
                if (string.IsNullOrEmpty(hex)) return new double[] { 1, 1, 1, 1 };
                if (hex.StartsWith("#")) hex = hex.Substring(1);
                int r = Convert.ToInt32(hex.Substring(0, 2), 16);
                int g = Convert.ToInt32(hex.Substring(2, 2), 16);
                int b = Convert.ToInt32(hex.Substring(4, 2), 16);
                return new double[] { r / 255.0, g / 255.0, b / 255.0, 1.0 };
            }
            catch
            {
                return new double[] { 1, 1, 1, 1 };
            }
        }

        public override void OnMapOpenedClient()
        {
            if (capi == null || modSystem == null || modSystem.Config == null) return;

            foreach (var player in capi.World.AllOnlinePlayers)
            {
                var mapHideOthers = capi.World.Config.GetBool("mapHideOtherPlayers", false);
                var isMe = player.PlayerUID == capi.World.Player.PlayerUID;

                if (!mapHideOthers || isMe)
                {
                    GenerateEntityMapComponent(player);
                }
            }
        }

        public void RegenerateTextures()
        {
            if (capi == null) return;

            foreach (var tex in textureCache.Values)
            {
                tex?.Dispose();
            }
            textureCache.Clear();

            OnMapOpenedClient();
        }

        public override void Render(GuiElementMap mapElem, float dt)
        {
            if (!Active) return;

            foreach (var kvp in MapComps)
            {
                var player = kvp.Key;
                var comp = kvp.Value;

                string fill = modSystem?.Config?.OtherFillColorHex ?? "#C0C0C0";
                string border = modSystem?.Config?.OtherBorderColorHex ?? "#4D4D4D";

                bool isMe = player.PlayerUID == capi.World.Player.PlayerUID;
                if (isMe)
                {
                    fill = modSystem?.Config?.OwnFillColorHex ?? "#FFFFFF";
                    border = modSystem?.Config?.OwnBorderColorHex ?? "#000000";
                }
                else if (modSystem != null && modSystem.Config != null && modSystem.Config.UsePlayerCustomColors && 
                         modSystem.RemotePlayerDict.TryGetValue(player.PlayerUID, out var remoteData) && 
                         !string.IsNullOrEmpty(remoteData.FillColorHex))
                {
                    fill = remoteData.FillColorHex;
                    border = remoteData.BorderColorHex;
                }

                var expectedTex = GetOrCreateTexture(fill, border);
                if (comp.Texture != expectedTex)
                {
                    comp.Texture = expectedTex;
                }

                if (player.Entity == null && modSystem != null && modSystem.RemotePlayerDict.TryGetValue(player.PlayerUID, out var remoteData2))
                {
                    comp.PlayerEntity.Pos.SetPos(remoteData2.Position.X, remoteData2.Position.Y, remoteData2.Position.Z);
                    comp.PlayerEntity.Pos.SetYaw(remoteData2.Yaw);
                }
                comp.Render(mapElem, dt);
            }
        }

        public override void OnMouseMoveClient(MouseEvent args, GuiElementMap mapElem, StringBuilder hoverText)
        {
            if (!Active) return;

            foreach (var kvp in MapComps)
            {
                kvp.Value.OnMouseMove(args, mapElem, hoverText);
            }
        }

        public override void OnMouseUpClient(MouseEvent args, GuiElementMap mapElem)
        {
            if (!Active) return;

            foreach (var kvp in MapComps)
            {
                kvp.Value.OnMouseUpOnElement(args, mapElem);
            }
        }

        public override void Dispose()
        {
            foreach (var kvp in MapComps)
            {
                kvp.Value?.Dispose();
            }
            MapComps.Clear();

            foreach (var tex in textureCache.Values)
            {
                tex?.Dispose();
            }
            textureCache.Clear();
        }
    }
}

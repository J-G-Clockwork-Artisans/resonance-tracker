using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.API.Config;

namespace ResonanceTracker
{
    public class ResonanceTrackerServerConfig
    {
        public int UpdateIntervalMs { get; set; } = 1000;
        public bool HideCrouchingPlayers { get; set; } = false;
    }

    public class ResonanceTrackerModSystem : ModSystem
    {
        public IServerNetworkChannel ServerNetChannel { get; set; }
        public IClientNetworkChannel ClientNetChannel { get; set; }
        public ICoreAPI API { get; set; }
        public ICoreClientAPI CAPI { get; set; }
        public Dictionary<string, RemotePlayerData> RemotePlayerDict { get; set; } = new Dictionary<string, RemotePlayerData>();
        public ResonanceTrackerConfig Config { get; set; }
        public ResonanceTrackerServerConfig ServerConfig { get; set; }

        private long tickListenerId;

        public override void StartServerSide(ICoreServerAPI api)
        {
            API = api;

            // Load Server Configuration
            try
            {
                ServerConfig = api.LoadModConfig<ResonanceTrackerServerConfig>("ResonanceTrackerServerConfig.json");
            }
            catch 
            { 
                ServerConfig = null; 
            }

            if (ServerConfig == null)
            {
                ServerConfig = new ResonanceTrackerServerConfig();
                api.StoreModConfig(ServerConfig, "ResonanceTrackerServerConfig.json");
            }

            ServerNetChannel = api.Network.RegisterChannel("mapPlayers")
                .RegisterMessageType<PlayerPositionBatch>()
                .RegisterMessageType<OpenGuiPacket>()
                .RegisterMessageType<RequestOpenGuiPacket>()
                .SetMessageHandler<RequestOpenGuiPacket>(OnRequestOpenGui)
                .RegisterMessageType<SaveAdminConfigPacket>()
                .SetMessageHandler<SaveAdminConfigPacket>(OnSaveAdminConfig);
            
            StartTickListener();

            // Register Server Chat Command (invoked with slash: /resonance)
            api.ChatCommands.Create("resonance")
                .WithDescription("Resonance Tracker settings")
                .RequiresPrivilege(Privilege.chat)
                .BeginSubCommand("gui")
                    .WithDescription("Opens the Resonance Tracker config GUI")
                    .HandleWith(OnServerOpenGuiCommand)
                .EndSubCommand()
                .HandleWith(OnServerOpenGuiCommand);
        }

        private void StartTickListener()
        {
            tickListenerId = API.World.RegisterGameTickListener(SendPlayerPositions, ServerConfig.UpdateIntervalMs, 0);
        }

        private void RestartTickListener()
        {
            API.World.UnregisterGameTickListener(tickListenerId);
            StartTickListener();
        }

        private void OnRequestOpenGui(IServerPlayer player, RequestOpenGuiPacket packet)
        {
            bool isAdmin = player.HasPrivilege("control") || player.Role.Code == "admin";
            ServerNetChannel.SendPacket(new OpenGuiPacket 
            { 
                IsAdmin = isAdmin,
                UpdateIntervalMs = ServerConfig.UpdateIntervalMs,
                HideCrouchingPlayers = ServerConfig.HideCrouchingPlayers
            }, player);
        }

        private void OnSaveAdminConfig(IServerPlayer player, SaveAdminConfigPacket packet)
        {
            bool isAdmin = player.HasPrivilege("control") || player.Role.Code == "admin";
            if (!isAdmin)
            {
                player.SendMessage(GlobalConstants.GeneralChatGroup, "You do not have permission to change server settings.", EnumChatType.CommandError);
                return;
            }

            ServerConfig.UpdateIntervalMs = GameMath.Clamp(packet.UpdateIntervalMs, 100, 10000);
            ServerConfig.HideCrouchingPlayers = packet.HideCrouchingPlayers;

            API.StoreModConfig(ServerConfig, "ResonanceTrackerServerConfig.json");
            RestartTickListener();

            player.SendMessage(GlobalConstants.GeneralChatGroup, "Server settings saved successfully.", EnumChatType.CommandSuccess);
        }

        private TextCommandResult OnServerOpenGuiCommand(TextCommandCallingArgs args)
        {
            var serverPlayer = args.Caller.Player as IServerPlayer;
            if (serverPlayer != null)
            {
                bool isAdmin = serverPlayer.HasPrivilege("control") || serverPlayer.Role.Code == "admin";
                ServerNetChannel.SendPacket(new OpenGuiPacket 
                { 
                    IsAdmin = isAdmin,
                    UpdateIntervalMs = ServerConfig.UpdateIntervalMs,
                    HideCrouchingPlayers = ServerConfig.HideCrouchingPlayers
                }, serverPlayer);
            }
            return TextCommandResult.Success();
        }

        private void SendPlayerPositions(float dt)
        {
            var packet = new PlayerPositionBatch(API.World.AllOnlinePlayers, ServerConfig.HideCrouchingPlayers);
            ServerNetChannel.BroadcastPacket(packet);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            CAPI = api;
            ClientNetChannel = api.Network.RegisterChannel("mapPlayers")
                .RegisterMessageType<PlayerPositionBatch>()
                .SetMessageHandler<PlayerPositionBatch>(OnReceivedPlayerPositions)
                .RegisterMessageType<OpenGuiPacket>()
                .SetMessageHandler<OpenGuiPacket>(OnOpenGuiPacketReceived)
                .RegisterMessageType<RequestOpenGuiPacket>()
                .RegisterMessageType<SaveAdminConfigPacket>();

            // Register custom player map layer (client-side only!)
            api.ModLoader.GetModSystem<WorldMapManager>(true).RegisterMapLayer<GlobalPlayerMapLayer>("players", 0.5);

            // Load Configuration
            try
            {
                Config = api.LoadModConfig<ResonanceTrackerConfig>("ResonanceTrackerConfig.json");
            }
            catch 
            { 
                Config = null; 
            }

            if (Config == null)
            {
                Config = new ResonanceTrackerConfig();
                api.StoreModConfig(Config, "ResonanceTrackerConfig.json");
            }

            // Register Client Chat Command (invoked with dot: .resonance)
            api.ChatCommands.Create("resonance")
                .WithDescription("Resonance Tracker settings")
                .BeginSubCommand("gui")
                    .WithDescription("Opens the Resonance Tracker config GUI")
                    .HandleWith(OnClientOpenGuiCommand)
                .EndSubCommand()
                .HandleWith(OnClientOpenGuiCommand);
        }

        private void OnOpenGuiPacketReceived(OpenGuiPacket packet)
        {
            CAPI.Event.EnqueueMainThreadTask(() => 
            {
                var dialog = new GuiDialogResonanceTrackerConfig(
                    CAPI, 
                    Config, 
                    OnConfigChanged, 
                    ClientNetChannel, 
                    packet.IsAdmin, 
                    packet.UpdateIntervalMs, 
                    packet.HideCrouchingPlayers
                );
                dialog.TryOpen();
            }, "openresonancegui");
        }

        private TextCommandResult OnClientOpenGuiCommand(TextCommandCallingArgs args)
        {
            ClientNetChannel.SendPacket(new RequestOpenGuiPacket());
            return TextCommandResult.Success();
        }

        private void OnConfigChanged()
        {
            CAPI.StoreModConfig(Config, "ResonanceTrackerConfig.json");
            var mapManager = CAPI.ModLoader.GetModSystem<WorldMapManager>();
            var layer = mapManager.MapLayers.FirstOrDefault(l => l is GlobalPlayerMapLayer) as GlobalPlayerMapLayer;
            layer?.RegenerateTextures();
        }

        private void OnReceivedPlayerPositions(PlayerPositionBatch packet)
        {
            RemotePlayerDict.Clear();
            if (packet?.Players == null || packet.Players.Length == 0)
            {
                return;
            }

            foreach (var playerPos in packet.Players)
            {
                RemotePlayerDict[playerPos.PlayerUID] = new RemotePlayerData(
                    new Vec3d(playerPos.X, playerPos.Y, playerPos.Z), 
                    playerPos.Yaw
                );
            }
        }
    }

    [ProtoContract]
    public class PlayerPositionBatch
    {
        [ProtoMember(1)]
        private readonly PlayerPositionPacket[] players;

        public PlayerPositionPacket[] Players => players;

        public PlayerPositionBatch(IEnumerable<IPlayer> players, bool hideCrouching)
        {
            var list = new List<PlayerPositionPacket>();
            foreach (var player in players)
            {
                if (player?.Entity != null)
                {
                    if (hideCrouching && player.Entity.Controls.Sneak)
                    {
                        continue;
                    }

                    list.Add(new PlayerPositionPacket(
                        player.PlayerUID,
                        Convert.ToInt32(player.Entity.Pos.X),
                        Convert.ToInt16(player.Entity.Pos.Y),
                        Convert.ToInt32(player.Entity.Pos.Z),
                        (byte)(player.Entity.Pos.Yaw % 256f)
                    ));
                }
            }
            this.players = list.ToArray();
        }

        private PlayerPositionBatch()
        {
            players = Array.Empty<PlayerPositionPacket>();
        }
    }

    [ProtoContract]
    public readonly struct PlayerPositionPacket
    {
        [ProtoMember(1)]
        public readonly string PlayerUID;

        [ProtoMember(2)]
        public readonly int X;

        [ProtoMember(3)]
        public readonly short Y;

        [ProtoMember(4)]
        public readonly int Z;

        [ProtoMember(5)]
        public readonly byte Yaw;

        public PlayerPositionPacket(string playerUID, int x, short y, int z, byte yaw)
        {
            PlayerUID = playerUID;
            X = x;
            Y = y;
            Z = z;
            Yaw = yaw;
        }
    }

    [ProtoContract]
    public class OpenGuiPacket
    {
        [ProtoMember(1)]
        public bool IsAdmin { get; set; }

        [ProtoMember(2)]
        public int UpdateIntervalMs { get; set; }

        [ProtoMember(3)]
        public bool HideCrouchingPlayers { get; set; }
    }

    [ProtoContract]
    public class RequestOpenGuiPacket
    {
    }

    [ProtoContract]
    public class SaveAdminConfigPacket
    {
        [ProtoMember(1)]
        public int UpdateIntervalMs { get; set; }

        [ProtoMember(2)]
        public bool HideCrouchingPlayers { get; set; }
    }

    public readonly struct RemotePlayerData
    {
        public readonly Vec3d Position;
        public readonly float Yaw;

        public RemotePlayerData(Vec3d position, float yaw)
        {
            Position = position;
            Yaw = yaw;
        }
    }
}

﻿namespace MinecraftClient.Protocol.Handlers
{
    /// <summary>
    /// Incomming packet types
    /// </summary>
    public enum PacketTypesIn
    {
        SpawnEntity,
        SpawnExperienceOrb,
        SpawnWeatherEntity,
        SpawnLivingEntity,
        SpawnPainting,
        SpawnPlayer,
        EntityAnimation,
        Statistics,
        AcknowledgePlayerDigging,
        BlockBreakAnimation,
        BlockEntityData,
        BlockAction,
        BlockChange,
        BossBar,
        ServerDifficulty,
        ChatMessage,
        MultiBlockChange,
        TabComplete,
        DeclareCommands,
        WindowConfirmation,
        CloseWindow,
        WindowItems,
        WindowProperty,
        SetSlot,
        SetCooldown,
        PluginMessage,
        NamedSoundEffect,
        Disconnect,
        EntityStatus,
        Explosion,
        UnloadChunk,
        ChangeGameState,
        OpenHorseWindow,
        KeepAlive,
        ChunkData,
        Effect,
        Particle,
        UpdateLight,
        JoinGame,
        MapData,
        TradeList,
        EntityPosition,
        EntityPositionAndRotation,
        EntityRotation,
        EntityMovement,
        VehicleMove,
        OpenBook,
        OpenWindow,
        OpenSignEditor,
        CraftRecipeResponse,
        PlayerAbilities,
        CombatEvent,
        PlayerInfo,
        FacePlayer,
        PlayerPositionAndLook,
        UnlockRecipes,
        DestroyEntities,
        RemoveEntityEffect,
        ResourcePackSend,
        Respawn,
        EntityHeadLook,
        SelectAdvancementTab,
        WorldBorder,
        Camera,
        HeldItemChange,
        UpdateViewPosition,
        UpdateViewDistance,
        DisplayScoreboard,
        EntityMetadata,
        AttachEntity,
        EntityVelocity,
        EntityEquipment,
        SetExperience,
        UpdateHealth,
        ScoreboardObjective,
        SetPassengers,
        Teams,
        UpdateScore,
        SpawnPosition,
        TimeUpdate,
        Title,
        EntitySoundEffect,
        SoundEffect,
        StopSound,
        PlayerListHeaderAndFooter,
        NBTQueryResponse,
        CollectItem,
        EntityTeleport,
        Advancements,
        EntityProperties,
        EntityEffect,
        DeclareRecipes,
        SetTitleTime,
        SetTitleText,
        SetTitleSubTitle,
        WorldBorderWarningReach,
        WorldBorderWarningDelay,
        WorldBorderSize,
        WorldBorderLerpSize,
        WorldBorderCenter,
        ActionBar,
        Tags,
        DeathCombatEvent,
        EnterCombatEvent,
        EndCombatEvent,
        Ping,
        InitializeWorldBorder,
        SkulkVibrationSignal,
        ClearTiles,
        UseBed, // For 1.13.2 or below
        MapChunkBulk, // For 1.8 or below
        SetCompression, // For 1.8 or below
        UpdateSign, // For 1.8 or below
        UpdateEntityNBT, // For 1.8 or below
        Unknown, // For old version packet that have been removed and not used by mcc 
        UpdateSimulationDistance,
        // 1.19 Additions
        BlockChangedAck,
        ChatPreview,
        ServerData,
        SetDisplayChatPreview,
        SystemChat,
    }
}

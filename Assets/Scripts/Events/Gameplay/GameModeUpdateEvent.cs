using MinecraftClient.Mapping;

namespace MinecraftClient.Event
{
    public record GameModeUpdateEvent : BaseEvent
    {
        public GameMode GameMode { get; }

        public GameModeUpdateEvent(GameMode gamemode)
        {
            GameMode = gamemode;
        }

    }
}
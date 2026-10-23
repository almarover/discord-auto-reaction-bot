using System.Text.Json.Serialization;

namespace DiscordReactionManager_WinUI;

public sealed class BotConfig
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 3;

    [JsonPropertyName("guilds")]
    public Dictionary<string, GuildConfig> Guilds { get; set; } = [];
}

public sealed class GuildConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("common_emoji_ids")]
    public List<string> CommonReactions { get; set; } = [];

    [JsonPropertyName("common_channel_ids")]
    public List<string> CommonChannelIds { get; set; } = [];

    [JsonPropertyName("common_random_enabled")]
    public bool CommonRandomEnabled { get; set; }

    [JsonPropertyName("common_reaction_weights")]
    public Dictionary<string, double> CommonReactionWeights { get; set; } = [];

    [JsonPropertyName("common_no_reaction_enabled")]
    public bool CommonNoReactionEnabled { get; set; }

    [JsonPropertyName("common_no_reaction_probability")]
    public double CommonNoReactionProbability { get; set; }

    [JsonPropertyName("targets")]
    public List<TargetConfig> Targets { get; set; } = [];
}

public sealed class TargetConfig
{
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = "";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("emoji_ids")]
    public List<string> Reactions { get; set; } = [];

    [JsonPropertyName("random_enabled")]
    public bool RandomEnabled { get; set; }

    [JsonPropertyName("reaction_weights")]
    public Dictionary<string, double> ReactionWeights { get; set; } = [];

    [JsonPropertyName("channel_ids")]
    public List<string> ChannelIds { get; set; } = [];

    [JsonPropertyName("no_reaction_enabled")]
    public bool NoReactionEnabled { get; set; }

    [JsonPropertyName("no_reaction_probability")]
    public double NoReactionProbability { get; set; }
}

public sealed class RuntimeState
{
    [JsonPropertyName("running")]
    public bool Running { get; set; }

    [JsonPropertyName("updated_at")]
    public string UpdatedAt { get; set; } = "";

    [JsonPropertyName("bot")]
    public RuntimeBot Bot { get; set; } = new();

    [JsonPropertyName("guilds")]
    public List<RuntimeGuild> Guilds { get; set; } = [];
}

public sealed class RuntimeBot
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("username")]
    public string Username { get; set; } = "";
}

public sealed class RuntimeGuild
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("channels")]
    public List<RuntimeChannel> Channels { get; set; } = [];
}

public sealed class RuntimeChannel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public sealed class AppSettings
{
    public bool AutoStartManager { get; set; }
    public bool AutoStartBot { get; set; }
    public bool LaunchHidden { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
}

public sealed record GuildOption(string Id, string Name)
{
    public override string ToString() => $"{Name} ({Id})";
}

public sealed record ChannelOption(string Id, string Name)
{
    public override string ToString() => $"#{Name}";
}

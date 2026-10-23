using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace DiscordReactionManager_WinUI;

public static class AppServices
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static string ProjectDirectory { get; private set; } = "";
    public static string ConfigPath => Path.Combine(ProjectDirectory, ".bot-config.json");
    public static string RuntimePath => Path.Combine(ProjectDirectory, ".bot-runtime.json");
    public static string EnvPath => Path.Combine(ProjectDirectory, ".env");
    public static string SettingsPath => Path.Combine(ProjectDirectory, ".manager-winui-settings.json");
    public static string LogPath => Path.Combine(ProjectDirectory, "manager-winui.log");
    public static BotConfig Config { get; private set; } = new();
    public static RuntimeState Runtime { get; private set; } = new();
    public static AppSettings Settings { get; private set; } = new();
    public static BotProcessService Bot { get; private set; } = null!;

    public static void Initialize()
    {
        ProjectDirectory = FindProjectDirectory();
        Config = ReadJson(ConfigPath, new BotConfig());
        Runtime = ReadJson(RuntimePath, new RuntimeState());
        Settings = ReadJson(SettingsPath, new AppSettings());
        MergeRuntimeGuilds();
        Bot = new BotProcessService(ProjectDirectory, LogPath);
    }

    public static void RefreshRuntime()
    {
        Runtime = ReadJson(RuntimePath, new RuntimeState());
        MergeRuntimeGuilds();
    }

    public static void ReloadConfig()
    {
        Config = ReadJson(ConfigPath, new BotConfig());
        MergeRuntimeGuilds();
    }

    public static void SaveConfig()
    {
        Config.Version = 3;
        AtomicWrite(ConfigPath, JsonSerializer.Serialize(Config, JsonOptions));
    }

    public static List<ChannelOption> ChannelOptions(string guildId)
    {
        RefreshRuntime();
        RuntimeGuild? guild = Runtime.Guilds.FirstOrDefault(item => item.Id == guildId);
        return (guild?.Channels ?? [])
            .Where(channel => !string.IsNullOrWhiteSpace(channel.Id))
            .Select(channel => new ChannelOption(channel.Id, string.IsNullOrWhiteSpace(channel.Name) ? channel.Id : channel.Name))
            .OrderBy(channel => channel.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static void SaveSettings()
    {
        AtomicWrite(SettingsPath, JsonSerializer.Serialize(Settings, JsonOptions));
    }

    public static string LoadToken()
    {
        if (!File.Exists(EnvPath))
        {
            return "";
        }

        foreach (string rawLine in File.ReadAllLines(EnvPath, Encoding.UTF8))
        {
            string line = rawLine.Trim();
            if (line.StartsWith("DISCORD_TOKEN=", StringComparison.Ordinal))
            {
                return line["DISCORD_TOKEN=".Length..].Trim();
            }
        }
        return "";
    }

    public static void SaveToken(string token)
    {
        List<string> lines = File.Exists(EnvPath)
            ? [.. File.ReadAllLines(EnvPath, Encoding.UTF8)]
            : [];
        int index = lines.FindIndex(line => line.TrimStart().StartsWith("DISCORD_TOKEN=", StringComparison.Ordinal));
        string replacement = $"DISCORD_TOKEN={token.Trim()}";
        if (index >= 0)
        {
            lines[index] = replacement;
        }
        else
        {
            lines.Insert(0, replacement);
        }
        AtomicWrite(EnvPath, string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    public static List<string> ParseReactions(string text)
    {
        return text
            .Replace(',', ' ')
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public static List<GuildOption> GuildOptions()
    {
        MergeRuntimeGuilds();
        return Config.Guilds
            .Select(pair => new GuildOption(pair.Key, string.IsNullOrWhiteSpace(pair.Value.Name) ? pair.Key : pair.Value.Name))
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static GuildConfig EnsureGuild(string id, string name)
    {
        if (!Config.Guilds.TryGetValue(id, out GuildConfig? guild))
        {
            guild = new GuildConfig { Name = name };
            Config.Guilds[id] = guild;
        }
        else if (!string.IsNullOrWhiteSpace(name))
        {
            guild.Name = name;
        }
        return guild;
    }

    public static void ApplyManagerAutostart(bool enabled)
    {
        string startupDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        string shortcutPath = Path.Combine(startupDirectory, "Discord Auto Reaction Bot.lnk");
        const string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(runKey, true))
        {
            key.DeleteValue("Discord Auto Reaction Manager", false);
        }

        if (!enabled)
        {
            if (File.Exists(shortcutPath)) File.Delete(shortcutPath);
            return;
        }

        Type shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("Windows Script Host 不可用，无法创建启动快捷方式。");
        object shell = Activator.CreateInstance(shellType)!;
        object shortcut = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, [shortcutPath])!;
        Type shortcutType = shortcut.GetType();
        try
        {
            string launcher = Path.Combine(ProjectDirectory, "Discord Auto Reaction Bot.exe");
            string exe = File.Exists(launcher)
                ? launcher
                : Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "DiscordReactionManager.WinUI.exe");
            shortcutType.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, [exe]);
            shortcutType.InvokeMember("Arguments", System.Reflection.BindingFlags.SetProperty, null, shortcut, ["--startup"]);
            shortcutType.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, shortcut, [ProjectDirectory]);
            shortcutType.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, shortcut, ["Discord Auto Reaction Bot WinUI Manager"]);
            shortcutType.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
        }
        finally
        {
            if (Marshal.IsComObject(shortcut)) Marshal.FinalReleaseComObject(shortcut);
            if (Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
        }
    }

    private static void MergeRuntimeGuilds()
    {
        Config.Guilds ??= [];
        foreach (RuntimeGuild runtimeGuild in Runtime.Guilds ?? [])
        {
            if (!string.IsNullOrWhiteSpace(runtimeGuild.Id))
            {
                EnsureGuild(runtimeGuild.Id, runtimeGuild.Name);
            }
        }
    }

    private static string FindProjectDirectory()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        for (int depth = 0; depth < 10 && current is not null; depth++, current = current.Parent)
        {
            bool isPortableApp = File.Exists(Path.Combine(current.FullName, ".discord-auto-reaction-app"));
            bool isDevelopmentProject = File.Exists(Path.Combine(current.FullName, "bot.py"))
                && Directory.Exists(Path.Combine(current.FullName, "dist"));
            if (isPortableApp || isDevelopmentProject)
            {
                return current.FullName;
            }
        }
        return AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    }

    private static T ReadJson<T>(string path, T fallback)
    {
        try
        {
            if (!File.Exists(path))
            {
                return fallback;
            }
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path, Encoding.UTF8), JsonOptions) ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static void AtomicWrite(string path, string content)
    {
        string tempPath = path + ".tmp";
        File.WriteAllText(tempPath, content, new UTF8Encoding(false));
        File.Move(tempPath, path, true);
    }
}

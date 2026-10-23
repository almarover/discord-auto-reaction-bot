using System.Diagnostics;
using System.Text;

namespace DiscordReactionManager_WinUI;

public sealed class BotProcessService : IDisposable
{
    private readonly string _projectDirectory;
    private readonly string _logPath;
    private readonly object _logLock = new();
    private readonly List<string> _lines = [];
    private Process? _process;

    public event Action<string>? LogReceived;

    public BotProcessService(string projectDirectory, string logPath)
    {
        _projectDirectory = projectDirectory;
        _logPath = logPath;
        Append("[Manager] WinUI 管理器已启动。");
    }

    public bool IsOwnedProcessRunning => _process is { HasExited: false };

    public bool IsRunning
    {
        get
        {
            if (IsOwnedProcessRunning)
            {
                return true;
            }

            List<Process> existingProcesses = FindExistingBotProcesses();
            bool running = existingProcesses.Count > 0;
            existingProcesses.ForEach(process => process.Dispose());
            return running;
        }
    }

    public IReadOnlyList<string> LogSnapshot()
    {
        lock (_logLock)
        {
            return [.. _lines];
        }
    }

    public void ClearLogs()
    {
        lock (_logLock)
        {
            _lines.Clear();
        }
    }

    public Task<bool> StartAsync()
    {
        if (IsOwnedProcessRunning)
        {
            Append("[Manager] Bot 已经在运行。");
            return Task.FromResult(true);
        }

        List<Process> existingProcesses = FindExistingBotProcesses();
        if (existingProcesses.Count > 0)
        {
            existingProcesses.ForEach(process => process.Dispose());
            Append("[Manager] 检测到现有 Bot 进程，不会重复启动。");
            return Task.FromResult(true);
        }

        string botExe = Path.Combine(_projectDirectory, "dist", "DiscordReactionBot.exe");
        string pythonExe = Path.Combine(_projectDirectory, ".venv", "Scripts", "python.exe");
        string botScript = Path.Combine(_projectDirectory, "bot.py");
        ProcessStartInfo startInfo;
        if (File.Exists(botExe))
        {
            startInfo = new ProcessStartInfo(botExe);
        }
        else if (File.Exists(pythonExe) && File.Exists(botScript))
        {
            startInfo = new ProcessStartInfo(pythonExe, $"\"{botScript}\"");
        }
        else
        {
            Append("[Manager Error] 找不到 DiscordReactionBot.exe 或 Python Bot。");
            return Task.FromResult(false);
        }

        startInfo.WorkingDirectory = _projectDirectory;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.StandardOutputEncoding = Encoding.UTF8;
        startInfo.StandardErrorEncoding = Encoding.UTF8;
        startInfo.Environment["PYTHONUTF8"] = "1";

        try
        {
            _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            _process.OutputDataReceived += (_, args) => { if (args.Data is not null) Append(args.Data); };
            _process.ErrorDataReceived += (_, args) => { if (args.Data is not null) Append(args.Data); };
            _process.Exited += (_, _) => Append($"[Manager] Bot 已退出，代码：{_process?.ExitCode ?? -1}");
            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
            Append("[Manager] Bot 启动命令已发送。");
            return Task.FromResult(true);
        }
        catch (Exception error)
        {
            Append($"[Manager Error] Bot 启动失败：{error.Message}");
            return Task.FromResult(false);
        }
    }

    public async Task StopAsync()
    {
        Append("[Manager] 正在停止 Bot...");
        List<Process> targets = [];
        if (_process is { HasExited: false })
        {
            targets.Add(_process);
        }

        foreach (Process process in FindExistingBotProcesses())
        {
            if (targets.All(item => item.Id != process.Id))
            {
                targets.Add(process);
            }
        }

        foreach (Process process in targets)
        {
            try
            {
                process.Kill(true);
                await process.WaitForExitAsync();
            }
            catch (Exception error)
            {
                Append($"[Manager Error] 停止进程失败：{error.Message}");
            }
        }
        _process = null;
        Append("[Manager] Bot 已停止。");
    }

    private List<Process> FindExistingBotProcesses()
    {
        List<Process> matches = [];
        string expectedPath = Path.GetFullPath(Path.Combine(_projectDirectory, "dist", "DiscordReactionBot.exe"));
        foreach (Process process in Process.GetProcessesByName("DiscordReactionBot"))
        {
            try
            {
                if (string.Equals(process.MainModule?.FileName, expectedPath, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(process);
                }
                else
                {
                    process.Dispose();
                }
            }
            catch
            {
                process.Dispose();
            }
        }
        return matches;
    }

    public void Append(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        lock (_logLock)
        {
            _lines.Add(line);
            if (_lines.Count > 5000)
            {
                _lines.RemoveRange(0, _lines.Count - 5000);
            }
            try
            {
                File.AppendAllText(_logPath, line + Environment.NewLine, new UTF8Encoding(false));
            }
            catch
            {
            }
        }
        LogReceived?.Invoke(line);
    }

    public void Dispose()
    {
        _process?.Dispose();
    }
}

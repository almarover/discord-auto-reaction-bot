using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

internal static class Launcher
{
    [STAThread]
    private static void Main(string[] args)
    {
        string root = AppDomain.CurrentDomain.BaseDirectory;
        string manager = Path.Combine(root, "dist", "winui-manager", "DiscordReactionManager.WinUI.exe");
        if (!File.Exists(manager))
        {
            MessageBox.Show(
                "找不到管理器运行文件：\n" + manager,
                "Discord Auto Reaction Bot",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = manager,
                Arguments = string.Join(" ", args.Select(QuoteArgument)),
                WorkingDirectory = Path.GetDirectoryName(manager),
                UseShellExecute = true,
            });
        }
        catch (Exception error)
        {
            MessageBox.Show(
                "启动失败：\n" + error.Message,
                "Discord Auto Reaction Bot",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static string QuoteArgument(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}

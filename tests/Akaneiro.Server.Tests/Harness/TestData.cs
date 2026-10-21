using System.Runtime.CompilerServices;
using Akaneiro.Server.Game;

namespace Akaneiro.Server.Tests.Harness;

/// <summary>The game data tables, loaded once for all tests (they are only read).</summary>
public static class TestData
{
    private static readonly Lazy<GameData> _data = new(() =>
        new GameData(FindClientAssets(), Path.Combine(AppContext.BaseDirectory, "GameData")));

    public static GameData Data => _data.Value;

    /// <summary>Finds akaneiro-client/Assets next to the server checkout, wherever the tests were built to.</summary>
    private static string FindClientAssets([CallerFilePath] string sourceFile = "")
    {
        foreach (string start in new[] { AppContext.BaseDirectory, Path.GetDirectoryName(sourceFile) })
        {
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "akaneiro-client", "Assets");
                if (Directory.Exists(candidate))
                    return candidate;
            }
        }
        throw new DirectoryNotFoundException("akaneiro-client/Assets not found above " + AppContext.BaseDirectory);
    }
}

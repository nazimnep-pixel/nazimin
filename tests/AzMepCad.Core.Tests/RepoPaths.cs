namespace AzMepCad.Core.Tests;

internal static class RepoPaths
{
    public static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AzMepCad.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate AzMepCad.sln from test output directory.");
    }
}

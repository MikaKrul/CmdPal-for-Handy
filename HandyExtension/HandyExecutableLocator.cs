using System;
using System.IO;
using System.Linq;

namespace HandyExtension;

internal static class HandyExecutableLocator
{
    private static readonly string[] CandidateNames = ["handy.exe", "Handy.exe"];

    public static string? Find(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var configured = NormalizePath(configuredPath);
            return File.Exists(configured) ? configured : null;
        }

        foreach (var name in CandidateNames)
        {
            var onPath = Environment.GetEnvironmentVariable("PATH")?
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(directory => Path.Combine(directory.Trim().Trim('"'), name))
                .FirstOrDefault(File.Exists);
            if (onPath is not null)
            {
                return onPath;
            }
        }

        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        };

        return roots.Where(Directory.Exists)
            .SelectMany(root => CandidateNames.SelectMany(name => new[]
            {
                Path.Combine(root, "Handy", name),
                Path.Combine(root, "Programs", "Handy", name),
                Path.Combine(root, "Programs", "Handy", "Handy.exe"),
            }))
            .FirstOrDefault(File.Exists);
    }

    private static string NormalizePath(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        return Path.GetFullPath(expanded);
    }
}

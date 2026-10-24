using System;
using System.IO;
using System.Linq;

namespace HandyExtension;

internal static class HandyExecutableLocator
{
    private static readonly string[] CandidateNames = ["handy.exe", "Handy.exe"];
    private static readonly object DiscoveryLock = new();
    private static string? _discoveredExecutable;

    public static string? Find(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var configured = NormalizePath(configuredPath);
            return File.Exists(configured) ? configured : null;
        }

        // Discovery probes PATH and the common install folders on every history
        // query, so cache the found path - but only while it still exists, so a
        // Handy that was installed or removed is picked up on the next call.
        lock (DiscoveryLock)
        {
            if (_discoveredExecutable is { } cached && File.Exists(cached))
            {
                return cached;
            }
        }

        var found = Discover();
        if (found is not null)
        {
            lock (DiscoveryLock)
            {
                _discoveredExecutable = found;
            }
        }

        return found;
    }

    /// <summary>
    /// Drops the cached discovery result. Called by the tests, which change PATH
    /// and the file system between runs.
    /// </summary>
    internal static void ClearDiscoveryCache()
    {
        lock (DiscoveryLock)
        {
            _discoveredExecutable = null;
        }
    }

    private static string? Discover()
    {
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

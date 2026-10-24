using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandyExtension.Tests;

[TestClass]
public sealed class HandyExecutableLocatorTests
{
    private string _directory = string.Empty;
    private string? _originalPath;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"HandyExtension tests {Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _originalPath = Environment.GetEnvironmentVariable("PATH");
    }

    [TestCleanup]
    public void Cleanup()
    {
        Environment.SetEnvironmentVariable("PATH", _originalPath);
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [TestMethod]
    public void Find_UsesConfiguredPathWithSpaces()
    {
        var executable = Path.Combine(_directory, "handy.exe");
        File.WriteAllBytes(executable, []);

        Assert.AreEqual(executable, HandyExecutableLocator.Find($"\"{executable}\""));
    }

    [TestMethod]
    public void Find_DoesNotSilentlyUseAnotherExecutableWhenConfiguredPathIsInvalid()
    {
        var executable = Path.Combine(_directory, "handy.exe");
        File.WriteAllBytes(executable, []);
        Environment.SetEnvironmentVariable("PATH", _directory);

        Assert.IsNull(HandyExecutableLocator.Find(Path.Combine(_directory, "missing.exe")));
    }

    [TestMethod]
    public void Find_SearchesPathWhenNoOverrideIsConfigured()
    {
        var executable = Path.Combine(_directory, "handy.exe");
        File.WriteAllBytes(executable, []);
        Environment.SetEnvironmentVariable("PATH", $"\"{_directory}\"");

        Assert.AreEqual(executable, HandyExecutableLocator.Find(null));
    }
}

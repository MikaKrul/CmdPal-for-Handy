using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandyExtension.Tests;

[TestClass]
public sealed class HandyMenuConfigTests
{
    private string _directory = string.Empty;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"HandyMenuConfig tests {Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string StorePath => Path.Combine(_directory, "menu.json");

    [TestMethod]
    public void LoadWithoutFileReturnsDefaultsForEveryItem()
    {
        var placements = HandyMenuConfig.Load(StorePath);

        Assert.AreEqual(HandyMenuItems.Items.Length, placements.Count);
        Assert.AreEqual(HandyMenuPlacement.Home, placements["searchTranscripts"]);
        Assert.AreEqual(HandyMenuPlacement.Home, placements["selectMicrophone"]);
        Assert.AreEqual(HandyMenuPlacement.More, placements["toggleRecording"]);
        Assert.AreEqual(HandyMenuPlacement.More, placements["openHandy"]);
    }

    [TestMethod]
    public void SetPlacementSavesAndLoadReturnsIt()
    {
        HandyMenuConfig.SetPlacement("selectModel", HandyMenuPlacement.More, StorePath);
        HandyMenuConfig.SetPlacement("toggleRecording", HandyMenuPlacement.Home, StorePath);

        var placements = HandyMenuConfig.Load(StorePath);

        Assert.AreEqual(HandyMenuPlacement.More, placements["selectModel"]);
        Assert.AreEqual(HandyMenuPlacement.Home, placements["toggleRecording"]);
        Assert.AreEqual(HandyMenuPlacement.Home, placements["searchTranscripts"]);
    }

    [TestMethod]
    public void SetPlacementWritesReadableJsonValues()
    {
        HandyMenuConfig.SetPlacement("toggleRecording", HandyMenuPlacement.Hidden, StorePath);

        var contents = File.ReadAllText(StorePath);

        Assert.IsTrue(contents.Contains("\"toggleRecording\":\"hidden\""));
    }

    [TestMethod]
    public void LoadIgnoresUnknownIdsAndInvalidValues()
    {
        File.WriteAllText(StorePath, "{\"selectModel\":\"banana\",\"unknownItem\":\"home\",\"toggleRecording\":\"hidden\",\"pasteLastTranscript\":\"HOME\"}");

        var placements = HandyMenuConfig.Load(StorePath);

        Assert.AreEqual(HandyMenuPlacement.Home, placements["selectModel"]);
        Assert.AreEqual(HandyMenuPlacement.Hidden, placements["toggleRecording"]);
        Assert.AreEqual(HandyMenuPlacement.Home, placements["pasteLastTranscript"]);
        Assert.AreEqual(HandyMenuItems.Items.Length, placements.Count);
    }

    [TestMethod]
    public void LoadFallsBackToDefaultsOnCorruptFile()
    {
        File.WriteAllText(StorePath, "{ this is not json");

        var placements = HandyMenuConfig.Load(StorePath);

        Assert.AreEqual(HandyMenuPlacement.Home, placements["searchTranscripts"]);
        Assert.AreEqual(HandyMenuPlacement.More, placements["toggleRecording"]);
    }

    [TestMethod]
    public void ResetPlacementRestoresTheDefault()
    {
        HandyMenuConfig.SetPlacement("selectModel", HandyMenuPlacement.Hidden, StorePath);

        HandyMenuConfig.ResetPlacement("selectModel", StorePath);

        Assert.AreEqual(HandyMenuPlacement.Home, HandyMenuConfig.Load(StorePath)["selectModel"]);
    }

    [TestMethod]
    public void ResetAllRemovesTheStoreAndRestoresDefaults()
    {
        HandyMenuConfig.SetPlacement("selectModel", HandyMenuPlacement.Hidden, StorePath);

        HandyMenuConfig.ResetAll(StorePath);

        Assert.IsFalse(File.Exists(StorePath));
        Assert.AreEqual(HandyMenuPlacement.Home, HandyMenuConfig.Load(StorePath)["selectModel"]);
    }

    [TestMethod]
    public void EveryMenuItemHasAUniqueOrderedDefinition()
    {
        Assert.AreEqual(HandyMenuItems.Items.Length, HandyMenuItems.Items.Select(item => item.Id).Distinct().Count());
        Assert.IsTrue(HandyMenuItems.Items.All(item => !string.IsNullOrWhiteSpace(item.Id)));
        Assert.IsTrue(HandyMenuItems.Items.All(item => !string.IsNullOrWhiteSpace(item.Title)));
    }
}

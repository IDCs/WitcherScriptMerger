using System;
using System.IO;
using ReleaseTools;
using WitcherScriptMerger.Common;
using Xunit;

namespace WitcherScriptMerger.Common.Tests
{
    public sealed class ReleaseContractTests : IDisposable
    {
        private readonly string _root;

        public ReleaseContractTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "wsm-release-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
        }

        [Theory]
        [InlineData("refs/tags/v0.6.6", "0.6.6")]
        [InlineData("v0.6.6", "0.6.6")]
        [InlineData("0.6.6", "0.6.6")]
        [InlineData("  v1.0.0  ", "1.0.0")]
        public void ResolvesTagsToBareSemver(string input, string expected)
        {
            Assert.True(ReleaseContract.TryResolveVersion(input, out var version, out _));
            Assert.Equal(expected, version);
        }

        [Fact]
        public void KeepsTheVersionByteIdenticalToTheTag()
        {
            // Rewriting the number would leave the release name disagreeing
            // with the pushed tag, and gh release create --verify-tag then
            // fails after a successful build.
            Assert.True(ReleaseContract.TryResolveVersion("refs/tags/v10.20.30", out var version, out _));
            Assert.Equal("10.20.30", version);
            Assert.Equal("v10.20.30", ReleaseContract.TagName(version));
        }

        [Theory]
        [InlineData("0.6")]
        [InlineData("v0.6.6-beta.1")]
        [InlineData("latest")]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("v0.6.06")]  // semver forbids leading zeros
        [InlineData("01.2.3")]
        public void RejectsAnythingVortexWouldIgnore(string input)
        {
            // Vortex keeps only releases whose name passes semver.valid, so a
            // release named like this is published and then never seen.
            Assert.False(ReleaseContract.TryResolveVersion(input, out _, out var error));
            Assert.NotEmpty(error);
        }

        [Fact]
        public void ReleaseIsTitledWithBareSemverWhileTheTagKeepsItsPrefix()
        {
            Assert.True(ReleaseContract.TryResolveVersion("refs/tags/v0.6.6", out var version, out _));
            Assert.Equal("0.6.6", ReleaseContract.ReleaseTitle(version));
            Assert.Equal("v0.6.6", ReleaseContract.TagName(version));
        }

        [Fact]
        public void ArchiveIsNamedAfterTheVersion()
        {
            Assert.Equal("WitcherScriptMerger-0.6.6.7z", ReleaseContract.ArchiveName("0.6.6"));
        }

        /// <summary>Shaped like the shipped config: Vortex rewrites these three by key.</summary>
        private const string ShippedConfig = """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <appSettings>
                <add key="GameDirectory" value="" />
                <add key="VanillaScriptsDirectory" value="" />
                <add key="ModsDirectory" value="" />
              </appSettings>
            </configuration>
            """;

        private string Staged(Action<string> customise = null)
        {
            var dir = Path.Combine(_root, "staged-" + Guid.NewGuid().ToString("N"));
            void Add(string relative, string content)
            {
                var full = Path.Combine(dir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllText(full, content);
            }
            Add(KnownPaths.Executable, "exe");
            Add(KnownPaths.Executable + ".config", ShippedConfig);
            Add(KnownPaths.BundleScript, "math IS_REMASTER = 0");
            Add(KnownPaths.QuickBmsExe, "bms");
            Add(KnownPaths.KDiff3Exe, "kdiff");
            Add(KnownPaths.WccLiteExe, "wcc");
            customise?.Invoke(dir);
            return dir;
        }

        [Fact]
        public void AcceptsAStagedTreeMatchingThePublishedLayout()
        {
            Assert.True(ReleaseContract.TryValidateStaging(Staged(), out var problems));
            Assert.Empty(problems);
        }

        [Fact]
        public void RejectsAnArchiveWithoutTheExeConfigVortexWritesTo()
        {
            // A plain .NET build produces WitcherScriptMerger.dll.config instead.
            var dir = Staged(d => File.Delete(Path.Combine(d, KnownPaths.Executable + ".config")));

            Assert.False(ReleaseContract.TryValidateStaging(dir, out var problems));
            Assert.Contains(problems, p => p.Contains(".config"));
        }

        [Theory]
        [InlineData("GameDirectory")]
        [InlineData("VanillaScriptsDirectory")]
        [InlineData("ModsDirectory")]
        public void RejectsAConfigMissingASettingVortexRewrites(string key)
        {
            // setMergerConfig rewrites by key and does nothing when one is
            // absent, so this would ship and leave the merger unconfigured.
            var dir = Staged(d =>
            {
                var path = Path.Combine(d, KnownPaths.Executable + ".config");
                File.WriteAllText(path, ShippedConfig.Replace($"\"{key}\"", "\"Removed\""));
            });

            Assert.False(ReleaseContract.TryValidateStaging(dir, out var problems));
            Assert.Contains(problems, p => p.Contains(key));
        }

        [Fact]
        public void RejectsABundleScriptThatPredatesTheRemasterLayout()
        {
            var dir = Staged(d => File.WriteAllText(
                Path.Combine(d, KnownPaths.BundleScript), "getdstring NAME 0x100"));

            Assert.False(ReleaseContract.TryValidateStaging(dir, out var problems));
            Assert.Contains(problems, p => p.Contains("Remastered"));
        }

        [Fact]
        public void RejectsAnArchiveCarryingUserState()
        {
            var dir = Staged(d => File.WriteAllText(Path.Combine(d, KnownPaths.Inventory), "<MergeInventory/>"));

            Assert.False(ReleaseContract.TryValidateStaging(dir, out var problems));
            Assert.Contains(problems, p => p.Contains(KnownPaths.Inventory));
        }

        [Fact]
        public void RejectsAnArchiveMissingTheBundledTools()
        {
            var dir = Staged(d => File.Delete(Path.Combine(d, KnownPaths.WccLiteExe)));

            Assert.False(ReleaseContract.TryValidateStaging(dir, out var problems));
            Assert.Contains(problems, p => p.Contains("wcc_lite"));
        }
    }
}

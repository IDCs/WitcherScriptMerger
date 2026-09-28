using System;
using System.IO;
using WitcherScriptMerger.Common;
using Xunit;

namespace WitcherScriptMerger.Common.Tests
{
    public sealed class KnownPathsTests : IDisposable
    {
        private readonly string _root;

        public KnownPathsTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "wsm-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
        }

        private string GameDir(params string[] relativeExecutables)
        {
            var dir = Path.Combine(_root, "game-" + Guid.NewGuid().ToString("N"));
            foreach (var relative in relativeExecutables)
            {
                var full = Path.Combine(dir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllText(full, string.Empty);
            }
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static readonly string Legacy = Path.Combine("bin", "x64", "witcher3.exe");
        private static readonly string Dx12 = Path.Combine("bin", "x64_dx12", "witcher3.exe");

        [Fact]
        public void KeepsLaunchingTheLegacyBinaryWhenAnInstallHasBoth()
        {
            // Next-Gen ships both. Preferring DirectX 12 here would silently move
            // every existing install onto a different renderer.
            var dir = GameDir(Legacy, Dx12);

            Assert.Equal(Path.Combine(dir, Legacy), KnownPaths.ResolveGameExe(dir));
        }

        [Fact]
        public void ResolvesTheLegacyBinaryWhenItIsTheOnlyOne()
        {
            var dir = GameDir(Legacy);

            Assert.Equal(Path.Combine(dir, Legacy), KnownPaths.ResolveGameExe(dir));
        }

        [Fact]
        public void ResolvesTheDirectX12BinaryWhenTheLegacyOneIsAbsent()
        {
            // The Remastered edition drops bin\x64 entirely.
            var dir = GameDir(Dx12);

            Assert.Equal(Path.Combine(dir, Dx12), KnownPaths.ResolveGameExe(dir));
        }

        [Fact]
        public void ReportsTheLegacyPathWhenTheDirectoryHoldsNoGame()
        {
            var dir = GameDir();

            Assert.Equal(Path.Combine(dir, Legacy), KnownPaths.ResolveGameExe(dir));
        }

        [Fact]
        public void ShippedLayoutNamesTheBundleScriptUnderQuickBms()
        {
            // appsettings.json ships QuickBmsPluginPath with this same value; the
            // release archive is validated against it.
            Assert.Equal(Path.Combine("Tools", "QuickBMS", "witcher3.bms"), KnownPaths.BundleScript);
        }

        [Fact]
        public void InventoryIsUserStateNotAShippedFile()
        {
            Assert.Equal("MergeInventory.xml", KnownPaths.Inventory);
        }
    }
}

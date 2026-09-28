using WitcherScriptMerger.Common;
using Xunit;

namespace WitcherScriptMerger.Common.Tests
{
    public sealed class BundleSizeTests
    {
        // Measured on the shipped installs: the largest Next-Gen bundle is
        // content4\movies0.bundle, and the Remastered edition consolidates the
        // per-content movie and buffer bundles into one of each.
        private const long NextGenLargest = 4_111_000_000;   // 3.83 GB
        private const long RemasterMovies = 7_679_844_000;   // 7.15 GB
        private const long RemasterBuffers = 7_645_106_208;  // 7.12 GB

        [Fact]
        public void ExtractsFromEveryBundleTheOlderEditionsShip()
        {
            Assert.True(KnownPaths.CanReadBundle(NextGenLargest));
        }

        [Theory]
        [InlineData(RemasterMovies)]
        [InlineData(RemasterBuffers)]
        public void RefusesToExtractWhereOffsetsWouldBeTruncated(long size)
        {
            // QuickBMS reports 32 bit offsets past this point, so extraction
            // would read from the wrong place and hand back plausible rubbish.
            // Listing stays accurate, which is why only extraction is gated.
            Assert.False(KnownPaths.CanReadBundle(size));
        }

        [Fact]
        public void TreatsFourGigabytesAsTheLimit()
        {
            Assert.True(KnownPaths.CanReadBundle(KnownPaths.QuickBmsMaxBundleSize - 1));
            Assert.False(KnownPaths.CanReadBundle(KnownPaths.QuickBmsMaxBundleSize));
        }

        [Theory]
        [InlineData("foo.ws")]
        [InlineData("foo.xml")]
        [InlineData("foo.txt")]
        [InlineData("foo.csv")]
        [InlineData("FOO.WS")]
        public void RecognisesTheTypesTheMergerCanMerge(string path)
        {
            Assert.True(KnownPaths.IsMergeableFile(path));
        }

        [Theory]
        [InlineData("cs704.usm")]
        [InlineData("terrain.buffer")]
        [InlineData("dialogue.subs")]
        [InlineData("blob0.bundle")]
        [InlineData(null)]
        public void LeavesEverythingElseAlone(string path)
        {
            // The oversized Remastered bundles hold only these types, so the
            // extraction gate costs no merge in practice.
            Assert.False(KnownPaths.IsMergeableFile(path));
        }
    }
}

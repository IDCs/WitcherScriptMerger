using System.Linq;
using WitcherScriptMerger.Common;
using Xunit;

namespace WitcherScriptMerger.Common.Tests
{
    public sealed class VanillaCoverageTests
    {
        const string Vanilla = """
            function Alpha( value : int ) : bool
            	return value > 0;
            function Beta( value : int ) : bool
            	return value < 0;
            event OnAnimEvent_ClimbCameraOn( name : name )
            	DoClimb();
            event OnChainAnimStart()
            	DoChain();
            """;

        [Fact]
        public void ReportsNothingMissingForACopyOfTheInstalledVanilla()
        {
            Assert.Equal(0d, VanillaCoverage.MissingFraction(Vanilla, Vanilla));
            Assert.False(VanillaCoverage.WouldDropSignificantVanilla(Vanilla, Vanilla));
        }

        [Fact]
        public void IgnoresAModsOwnEdits()
        {
            // A mod built against this vanilla changes a line or two; everything
            // else is still there, so nothing would be dropped by merging it.
            var mod = Vanilla.Replace("return value > 0;", "return value > 100;");

            Assert.False(VanillaCoverage.WouldDropSignificantVanilla(Vanilla, mod));
        }

        [Fact]
        public void MeasuresWhatAnOlderCopyIsMissingEvenOnASmallFile()
        {
            // An older copy has none of the events the installed edition added.
            var older = """
                function Alpha( value : int ) : bool
                	return value > 0;
                function Beta( value : int ) : bool
                	return value < 0;
                """;

            Assert.True(VanillaCoverage.MissingFraction(Vanilla, older) > 0.4);
        }

        [Fact]
        public void StaysQuietWhenTooFewLinesAreAtStakeToJudge()
        {
            // The share is high only because the sample is tiny. Real scripts
            // run to thousands of lines, where a deliberate edit is noise.
            var older = "function Alpha( value : int ) : bool\n\treturn value > 0;";

            Assert.True(VanillaCoverage.MissingFraction(Vanilla, older) > 0.4);
            Assert.False(VanillaCoverage.WouldDropSignificantVanilla(Vanilla, older));
        }

        [Fact]
        public void FlagsACopyThatPredatesTheInstalledVanilla()
        {
            // Shaped like the real case: r4Player.ws is around 6,500 distinct
            // lines and the Next-Gen copies are missing roughly 939 of them.
            var shared = string.Join("\n", Enumerable.Range(0, 5500)
                .Select(i => $"	sharedStatement{i}();"));
            var installed = shared + "\n" + string.Join("\n", Enumerable.Range(0, 939)
                .Select(i => $"	addedInFiveZero{i}();"));

            Assert.Equal(939, VanillaCoverage.MissingLineCount(installed, shared));
            Assert.True(VanillaCoverage.WouldDropSignificantVanilla(installed, shared));
        }

        [Fact]
        public void IgnoresCommentsAndBlankLines()
        {
            var commented = "// a note\n\n" + Vanilla + "\n// trailing\n";

            Assert.Equal(0d, VanillaCoverage.MissingFraction(Vanilla, commented));
        }

        [Fact]
        public void IgnoresLineEndingStyle()
        {
            Assert.Equal(0d, VanillaCoverage.MissingFraction(Vanilla, Vanilla.Replace("\n", "\r\n")));
        }

        [Fact]
        public void TreatsAnEmptyVanillaFileAsNothingToLose()
        {
            Assert.Equal(0d, VanillaCoverage.MissingFraction("", "anything at all here"));
        }

        [Fact]
        public void TreatsAnEmptyModCopyAsLosingEverything()
        {
            Assert.Equal(1d, VanillaCoverage.MissingFraction(Vanilla, ""));
        }

        [Fact]
        public void NeedsASignificantShareEvenWhenManyLinesAreMissing()
        {
            // A large file the mod trims: plenty of lines gone, small share.
            var installed = string.Join("\n", Enumerable.Range(0, 10000)
                .Select(i => $"	statement{i}();"));
            var trimmed = string.Join("\n", Enumerable.Range(0, 9600)
                .Select(i => $"	statement{i}();"));

            Assert.True(VanillaCoverage.MissingLineCount(installed, trimmed) >= VanillaCoverage.MinimumMissingLines);
            Assert.True(VanillaCoverage.MissingFraction(installed, trimmed) < VanillaCoverage.WarnThreshold);
            Assert.False(VanillaCoverage.WouldDropSignificantVanilla(installed, trimmed));
        }

        [Fact]
        public void NeedsEnoughLinesEvenWhenTheShareIsLarge()
        {
            // A modest file the mod rewrote: large share, too few lines to act on.
            var installed = string.Join("\n", Enumerable.Range(0, 400)
                .Select(i => $"	statement{i}();"));
            var rewritten = string.Join("\n", Enumerable.Range(0, 200)
                .Select(i => $"	statement{i}();"));

            Assert.True(VanillaCoverage.MissingFraction(installed, rewritten) >= VanillaCoverage.WarnThreshold);
            Assert.True(VanillaCoverage.MissingLineCount(installed, rewritten) < VanillaCoverage.MinimumMissingLines);
            Assert.False(VanillaCoverage.WouldDropSignificantVanilla(installed, rewritten));
        }

        /// <summary>
        /// Builds a vanilla file of <paramref name="vanillaLines"/> distinct
        /// lines and a mod copy missing <paramref name="missingLines"/> of them.
        /// </summary>
        static (string Vanilla, string Mod) Sized(int vanillaLines, int missingLines)
        {
            var kept = Enumerable.Range(0, vanillaLines - missingLines).Select(i => $"	shared{i}();");
            var added = Enumerable.Range(0, missingLines).Select(i => $"	onlyInInstalled{i}();");
            return (string.Join("\n", kept.Concat(added)), string.Join("\n", kept));
        }

        [Theory]
        // Measured on the real installs: distinct lines of the installed vanilla
        // file, and how many of them each mod's copy no longer has.
        [InlineData(207, 0, false)]      // modXPx5 levelManager.ws, built from 5.0
        [InlineData(6478, 939, true)]    // r4Player.ws, Brothers In Arms
        [InlineData(6478, 949, true)]    // r4Player.ws, Indestructible Items
        [InlineData(5512, 926, true)]    // playerWitcher.ws, modRazorShaving
        [InlineData(2034, 1249, true)]   // ingameMenu.ws, CommunityPatch-Base
        [InlineData(1039, 374, true)]    // mapMenu.ws, Fast Travel from Anywhere
        [InlineData(3200, 250, false)]   // real drift, below the line count gate
        public void MatchesTheJudgementMadeOnRealMods(int vanillaLines, int missingLines, bool shouldWarn)
        {
            var (vanilla, mod) = Sized(vanillaLines, missingLines);

            Assert.Equal(missingLines, VanillaCoverage.MissingLineCount(vanilla, mod));
            Assert.Equal(shouldWarn, VanillaCoverage.WouldDropSignificantVanilla(vanilla, mod));
        }
    }
}

using System;
using System.Collections.Generic;

namespace WitcherScriptMerger.Common
{
    /// <summary>
    /// Measures how much of the installed vanilla script a mod's copy still
    /// contains. A copy built against a different version of the game is missing
    /// whatever that version added, and merging it drops that code silently.
    /// Needs only the installed vanilla, which is all a merge has to hand.
    /// </summary>
    public static class VanillaCoverage
    {
        /// <summary>
        /// Share of vanilla a merge may drop before it is worth interrupting the
        /// user. A risk judgement: scores across real mods form a continuous
        /// spread rather than distinct groups.
        /// </summary>
        public const double WarnThreshold = 0.10;

        /// <summary>
        /// Lines that must be at stake before the share is trusted. On a short
        /// script a single deliberate edit is a large share of the file, so the
        /// share alone would warn about ordinary mods.
        /// </summary>
        public const int MinimumMissingLines = 300;

        /// <summary>
        /// What a merge would drop: how many of the vanilla file's distinct
        /// lines the mod's copy no longer contains, and that as a share. Both
        /// come from one pass, since the line sets are costly on a large script.
        /// </summary>
        public static (int MissingLines, double MissingShare) Measure(string vanillaText, string modText)
        {
            var vanilla = DistinctLines(vanillaText);
            if (vanilla.Count == 0)
            {
                return (0, 0d);
            }

            var mod = DistinctLines(modText);
            var missing = 0;
            foreach (var line in vanilla)
            {
                if (!mod.Contains(line))
                {
                    missing += 1;
                }
            }
            return (missing, (double)missing / vanilla.Count);
        }

        /// <summary>Distinct vanilla lines the mod's copy no longer contains.</summary>
        public static int MissingLineCount(string vanillaText, string modText) =>
            Measure(vanillaText, modText).MissingLines;

        /// <summary>
        /// Fraction of the vanilla file's distinct lines that the mod's copy no
        /// longer contains, between 0 and 1.
        /// </summary>
        public static double MissingFraction(string vanillaText, string modText) =>
            Measure(vanillaText, modText).MissingShare;

        /// <summary>
        /// Whether merging this copy risks dropping enough of the installed
        /// vanilla to be worth warning about. Needs both a large share and a
        /// meaningful number of lines, so ordinary edits to small scripts pass
        /// quietly.
        /// </summary>
        public static bool WouldDropSignificantVanilla(string vanillaText, string modText)
        {
            var (missingLines, missingShare) = Measure(vanillaText, modText);
            return missingLines >= MinimumMissingLines && missingShare >= WarnThreshold;
        }

        /// <summary>
        /// Comment and blank lines carry no meaning for this comparison and
        /// differ freely between editions, and very short lines are mostly
        /// braces, so they are all left out.
        /// </summary>
        static HashSet<string> DistinctLines(string text)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(text))
            {
                return result;
            }

            foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length > 3 && !line.StartsWith("//", StringComparison.Ordinal))
                {
                    result.Add(line);
                }
            }
            return result;
        }
    }
}

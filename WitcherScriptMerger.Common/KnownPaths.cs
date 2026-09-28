using System;
using System.IO;
using System.Linq;

namespace WitcherScriptMerger.Common
{
    /// <summary>
    /// Paths and file names that describe the shipped layout and the game's
    /// layout. Deliberately free of UI and settings dependencies so the release
    /// tooling can validate an archive without starting the application.
    /// </summary>
    public static class KnownPaths
    {
        public const string Executable = "WitcherScriptMerger.exe";

        /// <summary>Written at runtime, so it must never ship inside a release archive.</summary>
        public const string Inventory = "MergeInventory.xml";

        public const string TempBundleContent = "tempbundlecontent";
        public const string MergedBundleContent = "Merged Bundle Content";
        public const string BundleBase = "content";

        /// <summary>
        /// Size past which QuickBMS reports 32 bit offsets, so content stored
        /// beyond the boundary cannot be located in the file.
        /// </summary>
        public const long QuickBmsMaxBundleSize = 4L * 1024 * 1024 * 1024;

        /// <summary>
        /// Whether content can be extracted from a bundle of this size. Listing
        /// a larger one is still accurate, since entry names come from the table
        /// at the head of the file and only the offsets truncate. Keyed on size
        /// rather than name so any oversized bundle is covered.
        /// </summary>
        public static bool CanReadBundle(long bundleSizeInBytes) =>
            bundleSizeInBytes < QuickBmsMaxBundleSize;

        /// <summary>
        /// File types the merger can merge, whether loose or inside a bundle.
        /// </summary>
        public static bool IsMergeableFile(string path) =>
            path != null && MergeableExtensions.Any(
                ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

        internal static readonly string[] MergeableExtensions = { ".ws", ".xml", ".txt", ".csv" };

        public static readonly string ModScriptBase = Path.Combine("content", "scripts");
        public static readonly string VanillaScriptBase = Path.Combine("content", "content0", "scripts");

        /// <summary>
        /// Default locations of the bundled third-party tools. appsettings.json
        /// can override these at runtime; these are the layout a release ships.
        /// </summary>
        public static readonly string BundleScript = Path.Combine("Tools", "QuickBMS", "witcher3.bms");
        public static readonly string QuickBmsExe = Path.Combine("Tools", "QuickBMS", "quickbms.exe");
        public static readonly string KDiff3Exe = Path.Combine("Tools", "KDiff3", "KDiff3.exe");
        public static readonly string WccLiteExe = Path.Combine("Tools", "wcc_lite", "bin", "x64", "wcc_lite.exe");

        /// <summary>
        /// Game executable candidates, in probe order. Installs carrying both
        /// binaries launch the DirectX 11 one; the DirectX 12 path serves
        /// installs that ship no other.
        /// </summary>
        public static readonly string[] GameExeRelativePaths =
        {
            Path.Combine("bin", "x64", "witcher3.exe"),
            Path.Combine("bin", "x64_dx12", "witcher3.exe"),
        };

        /// <summary>
        /// First game executable that exists under <paramref name="gameDirectory"/>,
        /// or the DirectX 11 path when the directory holds no game.
        /// </summary>
        public static string ResolveGameExe(string gameDirectory)
        {
            foreach (var relative in GameExeRelativePaths)
            {
                var candidate = Path.Combine(gameDirectory, relative);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            return Path.Combine(gameDirectory, GameExeRelativePaths[0]);
        }
    }
}

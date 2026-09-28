using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WitcherScriptMerger.Common;

namespace ReleaseTools
{
    /// <summary>
    /// The parts of the release that Vortex depends on. Vortex takes the newest
    /// release by tag and downloads the asset named after that version, so a
    /// mistake here produces a release Vortex cannot find.
    /// </summary>
    public static class ReleaseContract
    {
        /// <summary>
        /// Turns a tag or a hand-typed version into the bare semver Vortex compares.
        /// Returns false with a reason instead of throwing so the caller can
        /// report it as a build annotation.
        /// </summary>
        public static bool TryResolveVersion(string tagOrVersion, out string version, out string error)
        {
            version = string.Empty;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(tagOrVersion))
            {
                error = "No version or tag supplied.";
                return false;
            }

            var candidate = tagOrVersion.Trim();
            const string refPrefix = "refs/tags/";
            if (candidate.StartsWith(refPrefix, StringComparison.Ordinal))
            {
                candidate = candidate.Substring(refPrefix.Length);
            }
            if (candidate.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                candidate = candidate.Substring(1);
            }

            var parts = candidate.Split('.');
            if (parts.Length != 3 || parts.Any(part => part.Length == 0 || !part.All(char.IsDigit)))
            {
                error = $"'{tagOrVersion}' does not resolve to bare semver (major.minor.patch), "
                      + "which Vortex needs to compare it against the installed merger.";
                return false;
            }

            // Rejected rather than normalised: semver forbids leading zeros, and
            // rewriting the number leaves the release name disagreeing with the
            // pushed tag.
            if (parts.Any(part => part.Length > 1 && part[0] == '0'))
            {
                error = $"'{tagOrVersion}' has a leading zero, which semver does not allow. "
                      + "Use the number without it.";
                return false;
            }

            version = candidate;
            return true;
        }

        private const string ProductName = "WitcherScriptMerger";

        /// <summary>Asset Vortex downloads, named as mergerRelease.ts in game-witcher3 derives it.</summary>
        public static string ArchiveName(string version) => $"{ProductName}-{version}.7z";

        /// <summary>
        /// Release title. Deliberately not a version: older Vortex builds offer
        /// any release whose name passes semver.valid, then fail the download
        /// because they have no checksum for it.
        /// </summary>
        public static string ReleaseTitle(string version) => $"{ProductName} {version}";

        /// <summary>Release tag, named as mergerRelease.ts in game-witcher3 derives it.</summary>
        public static string TagName(string version) => "v" + version;

        /// <summary>
        /// Checks a staged tree before it is compressed. Every rule here
        /// corresponds to something Vortex or the merger reads by name.
        /// </summary>
        public static bool TryValidateStaging(string stagingDir, out IReadOnlyList<string> problems)
        {
            var found = new List<string>();

            if (!File.Exists(Path.Combine(stagingDir, KnownPaths.Executable)))
            {
                found.Add($"{KnownPaths.Executable} is missing. Vortex checks for it by name.");
            }

            // Vortex writes the game, mod and script directories into this file
            // by name. A .NET build emits WitcherScriptMerger.dll.config, which
            // the merger reads but Vortex never writes.
            var config = Path.Combine(stagingDir, KnownPaths.Executable + ".config");
            if (!File.Exists(config))
            {
                found.Add($"{KnownPaths.Executable}.config is missing. Vortex would configure a file nothing reads.");
            }
            else
            {
                // Vortex rewrites these by key and does nothing when one is
                // absent, leaving the merger pointed at no game.
                var text = File.ReadAllText(config);
                foreach (var key in VortexConfiguredKeys)
                {
                    if (!text.Contains($"\"{key}\"", StringComparison.Ordinal))
                    {
                        found.Add($"{KnownPaths.Executable}.config has no '{key}' setting. "
                                + "Vortex rewrites it by name and would silently skip it.");
                    }
                }
            }

            var bundleScript = Path.Combine(stagingDir, KnownPaths.BundleScript);
            if (!File.Exists(bundleScript))
            {
                found.Add($"{KnownPaths.BundleScript} is missing. Bundle conflict detection would be dead.");
            }
            else if (!File.ReadAllText(bundleScript).Contains(DualLayoutMarker, StringComparison.Ordinal))
            {
                found.Add($"{KnownPaths.BundleScript} predates the Remastered bundle layout. "
                        + "An older copy from the tools payload has overwritten the tracked one.");
            }

            foreach (var tool in new[] { KnownPaths.QuickBmsExe, KnownPaths.KDiff3Exe, KnownPaths.WccLiteExe })
            {
                if (!File.Exists(Path.Combine(stagingDir, tool)))
                {
                    found.Add($"{tool} is missing from the staged archive.");
                }
            }

            if (File.Exists(Path.Combine(stagingDir, KnownPaths.Inventory)))
            {
                found.Add($"{KnownPaths.Inventory} is user state and is absent from the published layout.");
            }

            problems = found;
            return found.Count == 0;
        }

        /// <summary>Marker proving the staged script understands the 5.x bundle table.</summary>
        private const string DualLayoutMarker = "IS_REMASTER";

        /// <summary>
        /// Settings Vortex rewrites when it configures the merger, from
        /// setMergerConfig in the game-witcher3 extension.
        /// </summary>
        private static readonly string[] VortexConfiguredKeys =
        {
            "GameDirectory",
            "VanillaScriptsDirectory",
            "ModsDirectory",
        };
    }
}

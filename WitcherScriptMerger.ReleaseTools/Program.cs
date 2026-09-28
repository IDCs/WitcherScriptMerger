using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using WitcherScriptMerger.Common;

namespace ReleaseTools
{
    /// <summary>
    /// Release steps that need real logic, kept out of the workflow so they are
    /// unit testable and runnable locally.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                return Fail("Usage: version <tag> | validate <stagingDir> | checksums <archive> <stagingDir>");
            }

            try
            {
                return args[0] switch
                {
                    "version" => Version(args.Skip(1).ToArray()),
                    "validate" => Validate(args.Skip(1).ToArray()),
                    "checksums" => Checksums(args.Skip(1).ToArray()),
                    _ => Fail($"Unknown command '{args[0]}'."),
                };
            }
            catch (Exception err)
            {
                return Fail(err.Message);
            }
        }

        static int Version(string[] args)
        {
            if (args.Length != 1)
            {
                return Fail("Usage: version <tag-or-version>");
            }
            if (!ReleaseContract.TryResolveVersion(args[0], out var version, out var error))
            {
                return Fail(error);
            }

            Output("version", version);
            Output("archive", ReleaseContract.ArchiveName(version));
            Output("tag", ReleaseContract.TagName(version));
            Output("title", ReleaseContract.ReleaseTitle(version));
            return 0;
        }

        static int Validate(string[] args)
        {
            if (args.Length != 1)
            {
                return Fail("Usage: validate <stagingDir>");
            }
            if (ReleaseContract.TryValidateStaging(args[0], out var problems))
            {
                Console.WriteLine($"Staged archive matches the published layout: {args[0]}");
                return 0;
            }

            foreach (var problem in problems)
            {
                Console.WriteLine($"::error::{problem}");
            }
            return 1;
        }

        static int Checksums(string[] args)
        {
            if (args.Length != 2)
            {
                return Fail("Usage: checksums <archive> <stagingDir>");
            }

            var archive = Md5(args[0]);
            var executable = Md5(Path.Combine(args[1], KnownPaths.Executable));

            Output("archiveChecksum", archive);
            Output("execChecksum", executable);

            // Vortex verifies both before it will use a download, so a release is
            // inert until this entry is added on that side.
            var version = Environment.GetEnvironmentVariable("RELEASE_VERSION") ?? string.Empty;
            Summary($$"""
                ## MD5Cache.json entry

                Add to `extensions/games/game-witcher3/assets/MD5Cache.json` in Vortex.
                The release is ignored until this lands.

                ```json
                {
                  "version": "{{version}}",
                  "archiveChecksum": "{{archive}}",
                  "execChecksum": "{{executable}}"
                }
                ```
                """);
            return 0;
        }

        static string Md5(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(MD5.HashData(stream));
        }

        static void Output(string key, string value)
        {
            Console.WriteLine($"{key}={value}");
            var file = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
            if (!string.IsNullOrEmpty(file))
            {
                File.AppendAllText(file, $"{key}={value}{Environment.NewLine}");
            }
        }

        static void Summary(string markdown)
        {
            var file = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
            if (!string.IsNullOrEmpty(file))
            {
                File.AppendAllText(file, markdown + Environment.NewLine);
            }
            else
            {
                Console.WriteLine(markdown);
            }
        }

        static int Fail(string message)
        {
            Console.WriteLine($"::error::{message}");
            return 1;
        }
    }
}

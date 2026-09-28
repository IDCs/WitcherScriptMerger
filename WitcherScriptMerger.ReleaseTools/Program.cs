using System;
using System.IO;
using System.Linq;

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
                return Fail("Usage: version <tag> | validate <stagingDir>");
            }

            try
            {
                return args[0] switch
                {
                    "version" => Version(args.Skip(1).ToArray()),
                    "validate" => Validate(args.Skip(1).ToArray()),
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

        static void Output(string key, string value)
        {
            Console.WriteLine($"{key}={value}");
            var file = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
            if (!string.IsNullOrEmpty(file))
            {
                File.AppendAllText(file, $"{key}={value}{Environment.NewLine}");
            }
        }

        static int Fail(string message)
        {
            Console.WriteLine($"::error::{message}");
            return 1;
        }
    }
}

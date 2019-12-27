using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace pudding
{
    class Program
    {
        const string helpDoc = @"
Patch some lines to existing text file as well as has the ability to undo the patch
Usage: pudding [-u] [-s] [-c comment-symbol] patch target

    -u      Do unpatch instead of patch
    -U      Remove ALL patches patched by this tool, in this case, <patch> can be omitted
    -s      Safe mode, backup the original target file as <target>.bak
    -c      Comment symbol used by the target file, defaults to #

    The name of a patch file is ending with .patch, defaults to patch.patch

";

        static void Main(string[] args)
        {
            try
            {
                WrapMain(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
        }

        static void WrapMain(string[] args)
        {
            string op = null;
            bool? safe = null;
            string sep = null;
            string patch = null;
            string target = null;

            if (args.Length == 0 ||
                args.Length == 1 && (args[0] == "--help" || args[0] == "/?" || args[0] == "-h"))
            {
                Console.WriteLine(helpDoc);
                return;
            }

            for (int i = 0; i < args.Length; i++)
            {
                if (op == null && args[i] == "-u")
                {
                    op = "unpatch";
                }
                else if (op == null && args[i] == "-U")
                {
                    op = "UNPATCH";
                    Console.WriteLine("Warning: patch file will be ignored in -U mode");
                }
                else if (safe == null && args[i] == "-s")
                {
                    safe = true;
                }
                else if (sep == null && args[i] == "-c" && i < args.Length - 1)
                {
                    sep = args[++i];
                }
                /* This line should be before target == null */
                else if (patch == null && args[i].EndsWith(".patch"))
                {
                    patch = args[i];
                }
                else if (target == null)
                {
                    target = args[i];
                }
                else
                {
                    Console.WriteLine($"Warning: unexpected argument {args[i]}");
                }
            }

            op = op ?? "patch";
            safe = safe ?? false;
            sep = sep ?? "#";
            patch = patch ?? "patch.patch";
            Trace.Assert(target != null, "You must specify the target file");

            Trace.Assert(op == "UNPATCH" || File.Exists(patch), $"Patch file {patch} doesn't exist");
            Trace.Assert(File.Exists(target), $"Target file {target} doesn't exist");

            Console.WriteLine($@"
op:             {op}
safe mode:      {safe}
comment symbol: {sep}
patch file:     {patch}
target file:    {target}

");

            if (safe.Value)
            {
                string backup = $"{target}.bak";
                Console.WriteLine($"Copying {target} to {backup}");
                File.Copy(target, backup, true);
            }

            switch (op)
            {
                case "patch":
                    Console.WriteLine("Patching...");
                    Patch(patch, target, sep);
                    break;
                case "unpatch":
                    Console.WriteLine("Unpatching...");
                    Unpatch(patch, target, sep);
                    break;
                case "UNPATCH":
                    Console.WriteLine("Unpatching...");
                    UnpatchAll(target, sep);
                    break;
            }

            Console.WriteLine("Done.");
        }

        #region OPs
        static void Patch(string patch, string target, string sep)
        {
            string[] linesInPatchFile = File.ReadAllLines(patch);
            var linesToAppend = linesInPatchFile.Select(patchLineRaw => TagPatchLine(patchLineRaw, sep));

            bool hasEOL = false;
            using (FileStream fs = File.OpenRead(target))
            {
                Trace.Assert(fs.CanSeek && fs.CanRead);
                fs.Seek(-1, SeekOrigin.End);
                hasEOL = fs.ReadByte() == '\n';
            }

            File.AppendAllLines(target, hasEOL ?
                linesToAppend :
                new string[] { string.Empty }.Concat(linesToAppend));
        }

        static void Unpatch(string patch, string target, string sep)
        {
            List<string> linesInPatchFile = File.ReadAllLines(patch).ToList();
            string[] linesInTargetFile = File.ReadAllLines(target);
            List<string> linesToOutput = new List<string>();

            foreach (var line in linesInTargetFile)
            {
                bool keep = true;
                if (IsPatchedLine(line, sep, out string patchLineRaw) &&
                    linesInPatchFile.Contains(patchLineRaw))
                {
                    keep = false;
                    linesInPatchFile.Remove(patchLineRaw);
                }
                if (keep)
                {
                    linesToOutput.Add(line);
                }
            }

            File.WriteAllLines(target, linesToOutput);
        }

        static void UnpatchAll(string target, string sep)
        {
            string[] linesInTargetFile = File.ReadAllLines(target);
            var linesToOutput = linesInTargetFile.Where(x => !IsPatchedLine(x, sep, out _));
            File.WriteAllLines(target, linesToOutput);
        }
        #endregion

        #region Tagging and Recognition
        static string TagPatchLine(string patchLineRaw, string sep)
        {
            int p = rnd.Next();
            int q = rnd.Next();
            int r = p ^ q;
            return $"{patchLineRaw} {sep} pudding|{p.ToString("X8")},{q.ToString("X8")},{r.ToString("X8")}";
        }

        static bool IsPatchedLine(string line, string sep, out string patchLineRaw)
        {
            int lastSep = line.LastIndexOf(sep);

            if (lastSep >= 1 && lastSep < line.Length - 2)
            {
                patchLineRaw = line.Substring(0, lastSep - 1);
                string tag = line.Substring(lastSep + 2);

                Regex regex = new Regex(@"^pudding\|(?<p>[\dA-F]+),(?<q>[\dA-F]+),(?<r>[\dA-F]+)$");
                Match match = regex.Match(tag);
                if (match.Success &&
                    (int.Parse(match.Groups["p"].Value, NumberStyles.HexNumber) ^
                    int.Parse(match.Groups["q"].Value, NumberStyles.HexNumber)) ==
                    int.Parse(match.Groups["r"].Value, NumberStyles.HexNumber))
                {
                    return true;
                }
            }

            patchLineRaw = null;
            return false;
        }

        static readonly Random rnd = new Random();
        #endregion
    }
}

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

    -u      do unpatch instead of patch
    -s      safe mode, backup the original target file as <target>.bak
    -u      comment symbol used by the target file, defaults to #

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
                else if (safe == null && args[i] == "-s")
                {
                    safe = true;
                }
                else if (sep == null && args[i] == "-c" && i < args.Length - 1)
                {
                    sep = args[++i];
                }
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
            Trace.Assert(target != null, "Please specify the file to patch");

            Trace.Assert(File.Exists(patch), $"Patch file {patch} doesn't exist");
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
            }

            Console.WriteLine("Done.");
        }

        static void Patch(string patch, string target, string sep)
        {
            Random rnd = new Random();
            string[] patchLines = File.ReadAllLines(patch);
            for (int i = 0; i < patchLines.Length; i++)
            {
                int p = rnd.Next();
                int q = rnd.Next();
                int r = p ^ q;
                patchLines[i] = $"{patchLines[i]} {sep} pudding|{p.ToString("X8")},{q.ToString("X8")},{r.ToString("X8")}";
            }

            bool hasEOL = false;
            using (FileStream fs = File.OpenRead(target))
            {
                Trace.Assert(fs.CanSeek && fs.CanRead);
                fs.Seek(-1, SeekOrigin.End);
                hasEOL = fs.ReadByte() == '\n';
            }

            File.AppendAllLines(target, hasEOL ?
                patchLines :
                new string[] { string.Empty }.Union(patchLines));
        }

        static void Unpatch(string patch, string target, string sep)
        {
            List<string> patchLineList = File.ReadAllLines(patch).ToList();
            string[] targetLines = File.ReadAllLines(target);
            List<string> outputTarget = new List<string>();

            foreach (var targetLine in targetLines)
            {
                bool keep = true;
                int lastSep = targetLine.LastIndexOf(sep);

                if (lastSep >= 1 && lastSep < targetLine.Length - 2)
                {
                    string patchLine = targetLine.Substring(0, lastSep - 1);
                    if (patchLineList.Contains(patchLine))
                    {
                        string comment = targetLine.Substring(lastSep + 2);
                        Regex regex = new Regex(@"^pudding\|(?<p>[\dA-F]+),(?<q>[\dA-F]+),(?<r>[\dA-F]+)$");
                        Match match = regex.Match(comment);
                        if (match.Success &&
                            (int.Parse(match.Groups["p"].Value, NumberStyles.HexNumber) ^
                            int.Parse(match.Groups["q"].Value, NumberStyles.HexNumber)) ==
                            int.Parse(match.Groups["r"].Value, NumberStyles.HexNumber))
                        {
                            keep = false;
                            patchLineList.Remove(patchLine);
                        }
                    }
                }

                if (keep)
                {
                    outputTarget.Add(targetLine);
                }
            }

            File.WriteAllLines(target, outputTarget);
        }
    }
}

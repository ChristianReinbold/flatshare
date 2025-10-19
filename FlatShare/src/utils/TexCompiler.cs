using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Zio;
using Zio.FileSystems;

namespace de.creinbold.FlatShare
{
    public class TexCompiler : IDisposable
    {
        public static string EscapeSpecialCharacters(string s)
        {
            // Escape outside of inline math expressions
            var strings = s.Split('$');
            for (int i = 0; i < strings.Length; i += 2)
            {
                strings[i] = strings[i].Replace(@"&", @"\&");
                strings[i] = strings[i].Replace(@"%", @"\%");
                strings[i] = strings[i].Replace(@"#", @"\#");
                strings[i] = strings[i].Replace(@"_", @"\_");
                strings[i] = strings[i].Replace(@"{", @"\{");
                strings[i] = strings[i].Replace(@"}", @"\}");
            }
            return String.Join("$", strings);
        }

        public static string PreprocessTemplateString(string templateString)
        {
            // By default, escape all "}" characters. We "unescape" the escaped ones. Thus,
            // we do not have to bother about curly brackets in <x:{ ... }> blocks. However, now
            // we have to close these blocks as follows: <x:{ ... #\}>
            templateString = templateString.Replace(@"}", @"#\}");
            templateString = templateString.Replace(@"#\#\}", @"}");

            // Do not use backslash as escape character (bad for tex-code), but "#\" instead
            templateString = templateString.Replace(@"\", @"\\");
            templateString = templateString.Replace(@"#\\", @"\");
            return templateString;
        }

        private bool _CancelCompilation;
        private TemporaryDirectory _TempDir;

        public IFileSystem WorkingDirectory { get; private set; }
        public UPath SourcePath { get; set; }
        public UPath CompiledPath { get { return SourcePath.ChangeExtension("pdf"); } }


        public TexCompiler()
        {
            var fs = new PhysicalFileSystem();
            _TempDir = new TemporaryDirectory();
            var tmpDir = fs.ConvertPathFromInternal(_TempDir);
            WorkingDirectory = fs.GetOrCreateSubFileSystem(tmpDir);
        }

        public void Dispose()
        {
            _TempDir?.Dispose();
        }


        public bool TryToCompile(int nRuns, bool verbose)
        {
            int exitCode = -1;
            _CancelCompilation = false;
            Console.CancelKeyPress += Console_CancelKeyPress;
            for (var i = 0; i < nRuns && !_CancelCompilation; i++)
            {
                exitCode = RunPdfLatex(verbose);
                if (verbose) Console.WriteLine(String.Format("Exit code of PdfLatex: {0}", exitCode));
            }
            Console.CancelKeyPress -= Console_CancelKeyPress;

            return exitCode == 0;
        }

        public void CopyWorkingDirectory(IFileSystem fs, UPath targetDir)
        {
            if (fs.DirectoryExists(targetDir))
                fs.DeleteDirectory(targetDir, true);
            foreach (var srcPath in WorkingDirectory.EnumerateFiles("/", "*", SearchOption.AllDirectories))
            {
                var targetPath = UPath.Combine(targetDir, srcPath.ToRelative());
                fs.CreateDirectory(targetPath.GetDirectory());
                WorkingDirectory.CopyFileCross(srcPath, fs, targetPath, false);
            }
        }

        private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            // If _CancelCompilation is set to true already, shut down application.
            e.Cancel = !_CancelCompilation;
            _CancelCompilation = true;
        }

        private int RunPdfLatex(bool verbose)
        {
            var animation = new string[] { "\\", "|", "/", "-" };
            var cmd = "pdflatex.exe";
            var args = new List<string>();
            args.Add("--interaction=nonstopmode");
            args.Add("--shell-escape");
            args.Add("\"" + SourcePath.ToRelative() + "\"");

            var outputQueue = new ConcurrentQueue<string>();
            var errorQueue = new ConcurrentQueue<string>();


            var proc = new Process();
            proc.StartInfo.FileName = cmd;
            proc.StartInfo.Arguments = String.Join(" ", args);
            proc.StartInfo.WorkingDirectory = WorkingDirectory.ConvertPathToInternal("/");
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.OutputDataReceived += delegate (object s, DataReceivedEventArgs e)
            {
                outputQueue.Enqueue(e.Data);
            };
            proc.ErrorDataReceived += delegate (object s, DataReceivedEventArgs e)
            {
                errorQueue.Enqueue(e.Data);
            };

            if (verbose) Console.WriteLine("########## START pdflatex ##########");

            proc.Start();
            // Without reading a redirected stream, the process blocks.
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            long tick = 0;
            while (!proc.WaitForExit(100))
            {
                // Clear previous animation
                try
                {
                    var cursorPosX = Console.CursorLeft;
                    var cursorPosY = Console.CursorTop;
                    Console.Write(new string(' ', Console.WindowWidth));
                    Console.SetCursorPosition(cursorPosX, cursorPosY);
                }
                catch (IOException)
                {
                    // Silently accept that Console animation is not working.
                }

                // Write redirected process output to console
                string buf;
                if (verbose)
                {
                    while (outputQueue.TryDequeue(out buf)) Console.Out.WriteLine(buf);
                    while (errorQueue.TryDequeue(out buf)) Console.Error.WriteLine(buf);
                }
                else
                {
                    while (outputQueue.TryDequeue(out buf)) ;
                    while (errorQueue.TryDequeue(out buf)) ;
                }

                // Render animation
                try
                {
                    var cursorPosX = Console.CursorLeft;
                    var cursorPosY = Console.CursorTop;
                    var frame = animation[tick % animation.Length];
                    Console.Write(frame + " Running PdfLatex " + frame);
                    Console.SetCursorPosition(cursorPosX, cursorPosY);
                }
                catch (IOException)
                {
                    // Silently accept that Console animation is not working.
                }

                if (_CancelCompilation) proc.Kill();
                tick++;

            }

            // Clear animation
            try
            {
                var cursorPosX = Console.CursorLeft;
                var cursorPosY = Console.CursorTop;
                Console.Write(new string(' ', Console.WindowWidth));
                Console.SetCursorPosition(cursorPosX, cursorPosY);
            }
            catch (IOException)
            {
                // Silently accept that Console animation is not working.
            }

            if (verbose)
            {
                string buf;
                while (outputQueue.TryDequeue(out buf)) Console.Out.WriteLine(buf);
                while (errorQueue.TryDequeue(out buf)) Console.Error.WriteLine(buf);
                Console.WriteLine();
                Console.WriteLine("########### END pdflatex ###########");
            }
            return proc.ExitCode;
        }
    }
}

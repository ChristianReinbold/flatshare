using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.creinbold.FlatShare
{
    public class CommandParser
    {
        private static string ExtractLine(ref IEnumerator<string> entries, int maxLength, string separator)
        {
            var builder = new StringBuilder();
            builder.Append(entries.Current);
            while (entries.MoveNext())
            {
                if (builder.Length + separator.Length + entries.Current.Length > maxLength) return builder.ToString();
                builder.Append(separator);
                builder.Append(entries.Current);

            }
            entries = null;
            return builder.ToString();
        }

        private static IEnumerable<string> ExtractLines(string s, string indent, bool indentFirstLine = true)
        {
            var builder = new StringBuilder();
            int w = Console.BufferWidth - 1;
            var entries = s.Split(' ').AsEnumerable().GetEnumerator();

            if (!entries.MoveNext()) yield break;
            if (!indentFirstLine) yield return ExtractLine(ref entries, w, " ");

            while (entries != null)
            {
                yield return indent + ExtractLine(ref entries, w - indent.Length, " ");
            }
        }

        private Dictionary<string, Func<IEnumerable<string>, string>> _CmdIdToFunction;
        private Dictionary<string, string> _CmdIdToHelp;
        private Dictionary<string, string> _CmdIdToOptions;

        public void AddComand(string cmd_id, Func<IEnumerable<string>, string> func, string optionsString, string helpMsg)
        {

            _CmdIdToFunction.Add(cmd_id, func);
            _CmdIdToOptions.Add(cmd_id, optionsString);
            _CmdIdToHelp.Add(cmd_id, helpMsg);
        }

        private string GetHelpString(string cmd)
        {
            var usageFormat = "{0} {1}";
            var usage = String.Format(usageFormat, cmd, _CmdIdToOptions[cmd]);
            var usageLines = ExtractLines(usage, new string(' ', cmd.Length + 1), false);
            var helpLines = ExtractLines(_CmdIdToHelp[cmd], new string(' ', 4));
            return String.Join("\n", usageLines.Concat(helpLines));
        }

        private string OnHelp(IEnumerable<string> args)
        {
            var orderedCommands = _CmdIdToHelp.Keys.OrderBy(p => p);
            var helpStrings = orderedCommands.Select(cmd => GetHelpString(cmd));
            return String.Join("\n", helpStrings);
        }

        private string OnVersion(IEnumerable<string> args)
        {
            return "Version: " + ProductVersion.ToString(ProductVersion.Get());
        }

        private string Exit(IEnumerable<string> args)
        {
            Environment.Exit(0);
            return null;
        }

        public CommandParser()
        {
            this._CmdIdToFunction = new Dictionary<string, Func<IEnumerable<string>, string>>();
            this._CmdIdToOptions = new Dictionary<string, string>();
            this._CmdIdToHelp = new Dictionary<string, string>();
            this.AddComand("help", this.OnHelp, "", "Prints the help text for all available commands.");
            this.AddComand("version", this.OnVersion, "", "Prints the current product version.");
            this.AddComand("exit", this.Exit, "", "Exit the application.");
        }

        public string Execute(string cmd)
        {
            var words = new List<string>();
            bool escapeSpace = false;
            foreach (var part in cmd.Split('\"'))
            {
                if (escapeSpace) words.Add(part);
                else words.AddRange(part.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                escapeSpace = !escapeSpace;
            }
            if (words.Count == 0) return "";
            var cmd_id = words[0];
            var args = words.Skip(1);
            Func<IEnumerable<string>, string> execute_fn = null;
            try
            {
                execute_fn = _CmdIdToFunction[cmd_id];
            }
            catch (KeyNotFoundException)
            {
                return String.Format("Unknown command: {0}\n Type help for a list of all available commands.", cmd_id);
            }
            return execute_fn(args);
        }
    }
}

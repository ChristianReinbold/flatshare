using de.creinbold.FlatShare.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Zio;
using Zio.FileSystems;

namespace de.creinbold.FlatShare
{
    public class WorkingFilesystemProvider
    {
        public event EventHandler<string> Changed;

        public WorkingFilesystemProvider()
        {
            bool directorySet = false;
            if (!String.IsNullOrEmpty(Settings.Default.WorkingDirectory))
            {
                try
                {
                    Get();
                    directorySet = true;
                }
                catch (IOException) { }
            }
            if (!directorySet)
            {
                Console.WriteLine("Please select a working directory where to store all data.");
                if (!SetByUser())
                {
                    Console.WriteLine("No working directory selected.\n\nPress key to exit application...");
                    Console.ReadKey();
                    Environment.Exit(0);
                }
            }
        }

        public void RegisterCommands(CommandParser cp)
        {
            cp.AddComand("pwd", this.ExecuteCommand, "[set]",
                "Returns the working directory of the application. " +
                "Supply \"set\" in order to set the working directory.");
        }

        private string ExecuteCommand(IEnumerable<string> args)
        {
            var args_list = args.ToList();
            if (args_list.Count > 1 || (args_list.Count == 1 && !"set".Equals(args_list[0])))
            {
                return "Usage: pwd [set]";
            }

            if (args_list.Count == 1)
            {
                return SetByUser() ? "Working directory set." : "No working directory has been set.";
            }
            else
            {
                return Settings.Default.WorkingDirectory;
            }
        }

        public IFileSystem Get()
        {
            var filesystem = new PhysicalFileSystem();
            var path = filesystem.ConvertPathFromInternal(Settings.Default.WorkingDirectory);
            return filesystem.GetOrCreateSubFileSystem(path);
        }

        public bool SetByUser()
        {
            var dialog = new FolderBrowserDialog();
            dialog.Description = "Please select a working directory where to store all data.";
            if (!String.IsNullOrEmpty(Settings.Default.WorkingDirectory))
                dialog.SelectedPath = Settings.Default.WorkingDirectory;
            dialog.ShowNewFolderButton = true;

            if (dialog.ShowDialog() != DialogResult.OK) return false;
            var pwd = dialog.SelectedPath;
            if (String.IsNullOrEmpty(pwd)) return false;
            Settings.Default.WorkingDirectory = pwd;
            Settings.Default.Save();
            Changed?.Invoke(this, pwd);
            return true;
        }


    }
}

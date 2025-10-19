using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Zio;

namespace de.creinbold.FlatShare
{
    public static class IO
    {
        public static bool AskUser(ConsoleKey yesKey = ConsoleKey.Y, ConsoleKey noKey = ConsoleKey.N)
        {
            for (var info = Console.ReadKey(true); ; info = Console.ReadKey(true))
            {
                if (info.Key == noKey) return false;
                if (info.Key == yesKey) return true;
            }
        }

        public static ConsoleKey AskForKey(params ConsoleKey[] allowedKeys)
        {
            for (var info = Console.ReadKey(true); ; info = Console.ReadKey(true))
            {
                if (allowedKeys.Contains(info.Key)) return info.Key;
            }
        }

        public static byte[] ReadPassword(params ConsoleKey[] throwKeys)
        {
            var throwKeysSet = new HashSet<ConsoleKey>(throwKeys);
            string password = "";
            ConsoleKeyInfo info = Console.ReadKey(true);
            while (info.Key != ConsoleKey.Enter)
            {
                if (throwKeysSet.Contains(info.Key))
                {
                    Console.WriteLine();
                    throw new OperationCanceledException("The user has canceled inputting a password.");
                }
                if (info.Key != ConsoleKey.Backspace)
                {
                    Console.Write("*");
                    password += info.KeyChar;
                }

                else if (info.Key == ConsoleKey.Backspace)
                {
                    if (!string.IsNullOrEmpty(password))
                    {
                        password = password.Substring(0, password.Length - 1);
                        int pos = Console.CursorLeft;
                        Console.SetCursorPosition(pos - 1, Console.CursorTop);
                        Console.Write(" ");
                        Console.SetCursorPosition(pos - 1, Console.CursorTop);
                    }
                }
                info = Console.ReadKey(true);
            }
            Console.WriteLine();
            return Encoding.UTF8.GetBytes(password);
        }

        public static UPath AddNumberIfExists(IFileSystem fs, UPath path)
        {
            var modifiedPath = path;
            int tryCount = 0;
            var extension = path.GetExtensionWithDot();
            var fileName = path.GetNameWithoutExtension();
            var directory = path.GetDirectory();
            while (fs.FileExists(modifiedPath) || fs.DirectoryExists(modifiedPath))
            {
                tryCount++;
                modifiedPath = UPath.Combine(directory, fileName + "_" + tryCount + extension);
            }
            return modifiedPath;
        }

        public static void ExtractToFilesystem(this ZipArchive archive, IFileSystem dest)
        {
            foreach (var entry in archive.Entries)
            {
                UPath path = entry.FullName;
                path = path.ToAbsolute();
                if (!path.GetDirectory().IsRoot())
                    dest.CreateDirectory(path.GetDirectory());
                using (Stream entryStream = entry.Open(),
                              fileStream = dest.CreateFile(path))
                {
                    entryStream.CopyTo(fileStream);
                }
                dest.SetLastWriteTime(path, entry.LastWriteTime.UtcDateTime);
            }
        }

        public static void PopulateFromFilesystem(this ZipArchive archive, IFileSystem src)
        {
            foreach (var path in src.EnumerateFiles("/", "*", SearchOption.AllDirectories))
            {
                var relPath = path.ToRelative();
                var entry = archive.CreateEntry(relPath.ToString());
                entry.LastWriteTime = src.GetLastWriteTime(path);
                using (Stream entryStream = entry.Open(),
                              fileStream = src.OpenFile(path, FileMode.Open, FileAccess.Read))
                {
                    fileStream.CopyTo(entryStream);
                }
            }
        }

        public static bool IsRoot(this UPath path)
        {
            return path.GetDirectory().IsNull;
        }
    }
}

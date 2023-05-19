using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using System.Diagnostics;
using Microsoft.Win32;
using System.Windows;

namespace PokeCounter
{
    public static class Paths
    {
        public static string Executable => System.Reflection.Assembly.GetEntryAssembly().Location;
        public static string ExecutableDirectory => Ensure(Path.GetDirectoryName(Executable));

        public static string RelativeTempDirectory => "PokeCounter\\";

        public static string CacheDirectory => Ensure(Path.Combine(ExecutableDirectory, "Cache"));
        public static string ImageCacheDirectory => Ensure(Path.Combine(CacheDirectory, "PokemonImages"));
        public static string TextFileCacheDirectory => Ensure(Path.Combine(CacheDirectory, "TextFiles"));

        public static string Ensure(string directory)
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            return directory;
        }
    }
    public class FileAssociation
    {
        public string Extension { get; set; }
        public string ProgId { get; set; }
        public string FileTypeDescription { get; set; }
        public string ExecutableFilePath { get; set; }
        public string FileIcon { get; set; }
    }

    public class FileAssociations
    {
        // needed so that Explorer windows get refreshed after the registry is updated
        [System.Runtime.InteropServices.DllImport("Shell32.dll")]
        private static extern int SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);

        private const int SHCNE_ASSOCCHANGED = 0x8000000;
        private const int SHCNF_FLUSH = 0x1000;

        public static void EnsureAssociationsSet(string iconPath)
        {
            var filePath = Process.GetCurrentProcess().MainModule.FileName;
            EnsureAssociationsSet(
                new FileAssociation
                {
                    Extension = ".counter",
                    ProgId = "PokeCounter.Counter",
                    FileTypeDescription = "PokeCounter Counter",
                    ExecutableFilePath = filePath,
                    FileIcon = iconPath
                });
        }

        public static void EnsureAssociationsSet(params FileAssociation[] associations)
        {
            bool madeChanges = false;
            foreach (var association in associations)
            {
                madeChanges |= SetAssociation(
                    association.Extension,
                    association.ProgId,
                    association.FileTypeDescription,
                    association.ExecutableFilePath,
                    association.FileIcon);
            }

            if (madeChanges)
            {
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero);
            }
        }

        public static bool SetAssociation(string extension, string progId, string fileTypeDescription, string applicationFilePath, string iconPath)
        {
            bool madeChanges = false;
            madeChanges |= SetKeyDefaultValue(@"Software\Classes\" + extension, progId);
            madeChanges |= SetKeyDefaultValue(@"Software\Classes\" + progId, fileTypeDescription);
            madeChanges |= SetKeyDefaultValue($@"Software\Classes\{progId}\shell\open\command", "\"" + applicationFilePath + "\" \"%1\"");
            madeChanges |= SetRootKeyDefaultValue($@"{progId}", fileTypeDescription);
            madeChanges |= SetRootKeyDefaultValue($@"{progId}\DefaultIcon", iconPath);

            return madeChanges;
        }

        private static bool SetKeyDefaultValue(string keyPath, string value)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(keyPath);
                if (key.GetValue(null) as string != value)
                {
                    key.SetValue(null, value);
                    return true;
                }
            }
            catch (Exception)
            {
            }


            return false;
        }

        private static bool SetRootKeyDefaultValue(string keyPath, string value)
        {
            try
            {
                using var key = Registry.ClassesRoot.CreateSubKey(keyPath);
                if (key.GetValue(null) as string != value)
                {
                    key.SetValue(null, value);
                    return true;
                }
            }
            catch (Exception)
            {
            }

            return false;
        }
    }

    public enum SourceFolderType
    {
        Execution,
        Documents
    }

    class SettingsFile<T> where T : new()
    {

        public SettingsFile(string relativePath, SourceFolderType sourceFolder = SourceFolderType.Execution)
        {
            this.relativePath = relativePath;
            this.sourceFolder = sourceFolder;

            if (!Directory.Exists(Path.GetDirectoryName(FilePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            }
            if (!File.Exists(FilePath))
            {
                File.Create(FilePath).Close();
            }

            Load();

            if (data == null) data = new();
        }

        public string FilePath
        {
            get
            {
                string path = "";

                switch (sourceFolder)
                {
                    case SourceFolderType.Execution:
                        path += Paths.ExecutableDirectory;
                        break;
                    case SourceFolderType.Documents:
                        path += Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        break;
                    default:
                        break;
                }

                path += "\\" + relativePath;

                return path;
            }
        }

        public void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(data));
            }
            catch (Exception)
            {
                System.Windows.MessageBox.Show($"Could not save file {FilePath}.\n Is it being used by another process?", "Save failed!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void Load()
        {
            string text = File.ReadAllText(FilePath);
            data = JsonConvert.DeserializeObject<T>(text);
        }

        readonly string relativePath;
        readonly SourceFolderType sourceFolder;

        public T data;
    }
}

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PokeCounter
{
    static class DownloadManager
    {
        private static DateTime lastPing;
        private static bool? isOnline;
        public static bool IsOnline()
        {
            if (!isOnline.HasValue || (lastPing - DateTime.Now).TotalMinutes > 10)
            {
                lastPing = DateTime.Now;
                try
                {
                    Ping myPing = new Ping();
                    string host = "google.com";
                    byte[] buffer = new byte[32];
                    int timeout = 1000;
                    PingOptions pingOptions = new PingOptions();
                    PingReply reply = myPing.Send(host, timeout, buffer, pingOptions);
                    isOnline = (reply.Status == IPStatus.Success);
                }
                catch (Exception)
                {
                    isOnline = false;
                }
            }
            return isOnline.Value;
        }

        class TextCache
        {
            public Dictionary<string, string> urlToTextHash = new();
        }

        static SettingsFile<TextCache> textFileCache;

        static DownloadManager()
        {
            textFileCache = new SettingsFile<TextCache>(Path.Combine(Paths.RelativeTempDirectory, "downloadCache.json"), SourceFolderType.Documents);
        }

        public static void ClearCache()
        {
            List<string> skippedFiles = new List<string>();
            DeleteFolderRecursive(Paths.CacheDirectory, skippedFiles);

            List<string> entriesToRemove = new List<string>();
            foreach (var key in textFileCache.data.urlToTextHash)
            {
                if (!skippedFiles.Contains(key.Value))
                {
                    entriesToRemove.Add(key.Key);
                }
            }
            foreach (var key in entriesToRemove)
            {
                textFileCache.data.urlToTextHash.Remove(key);
            }
            textFileCache.Save();
        }

        static void DeleteFolderRecursive(string directory, List<string> skippedFiles)
        {
            DirectoryInfo dinfo = new DirectoryInfo(directory);
            foreach (var cd in dinfo.EnumerateDirectories())
            {
                DeleteFolderRecursive(cd.FullName, skippedFiles);
            }

            foreach (var file in dinfo.EnumerateFiles())
            {
                try
                {
                    file.Delete();
                }
                catch (Exception)
                {
                    skippedFiles.Add(file.FullName);
                }
            }
        }

        public static T DownloadObject<T>(string url, WebClient c = null)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(DownloadText(url, c));
            }
            catch (Exception)
            {
                return default(T);
            }
        }

        public static string DownloadText(string url, WebClient c = null)
        {
            if (c == null) c = new WebClient();
            if (textFileCache.data.urlToTextHash.ContainsKey(url))
            {
                var filename = textFileCache.data.urlToTextHash[url];
                if (File.Exists(filename))
                {
                    return File.ReadAllText(filename);
                }
                else
                {
                    textFileCache.data.urlToTextHash.Remove(url);
                    textFileCache.Save();
                }
            }

            byte[] raw;
            string text;

            try
            {
                raw = c.DownloadData(url);
                text = Encoding.UTF8.GetString(raw);
            }
            catch (Exception)
            {
                MessageBox.Show($"Failed to download {url}!\nAre you connected to the internet?", "Download error!", MessageBoxButton.OK, MessageBoxImage.Error);
                return "";
            }

            string cacheFileLocation = Path.Combine(Paths.TextFileCacheDirectory, Guid.NewGuid().ToString() + ".txt");

            try
            {
                File.WriteAllText(cacheFileLocation, text);
                textFileCache.data.urlToTextHash.Add(url, cacheFileLocation);
                textFileCache.Save();
            }
            catch (Exception)
            {
                MessageBox.Show($"Failed to write to file {cacheFileLocation}!\nDo you lack access to the folder {Paths.TextFileCacheDirectory}?", "Write error!", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return text;
        }

        public static void DownloadImage(string url, string targetFile, WebClient c = null)
        {
            if (c == null) c = new WebClient();
            if (File.Exists(targetFile))
            {
                return;
            }

            bool validLink = false;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Timeout = 15000;
            request.Method = "HEAD"; // As per Lasse's comment
            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    validLink = response.StatusCode == HttpStatusCode.OK;
                }
            }
            catch (Exception) { validLink = false; }
            if (validLink)
            {
                c.DownloadFile(new Uri(url), targetFile);
            }
        }
    }
}

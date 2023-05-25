using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace PokeCounter
{
    public struct WindowWrapper
    {
        public IntPtr windowHandle;
    }

    public struct QueryHandle
    {
        private bool isValid;
        public bool IsValid()
        {
            return isValid;
        }

        public void Invalidate()
        {
            isValid = false;
        }

        public void GenerateNewGuid()
        {
            guid = Guid.NewGuid();
            isValid = true;
        }

        public Guid guid;
        public IntPtr targetWindowHandle;

        public string FileName => guid + ".file";
    }

    public class RemoteControlManager
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        readonly static string RemoteControlImmediateFile = "PokeCounterDataFile.file";

        class Sender : IDisposable
        {
            MemoryMappedFile mmf;

            public void PostData<T>(T data, int structSize, QueryHandle handle) where T : struct
            {
                mmf = MemoryMappedFile.CreateNew(handle.FileName, IntPtr.Size, MemoryMappedFileAccess.ReadWrite);
                using (var accessor = mmf.CreateViewAccessor(0, structSize, MemoryMappedFileAccess.ReadWrite))
                {
                    accessor.Write(0, ref data);
                }
            }
            public void PostData<T>(T data, int structSize) where T : struct
            {
                mmf = MemoryMappedFile.CreateNew(RemoteControlImmediateFile, IntPtr.Size, MemoryMappedFileAccess.ReadWrite);
                using (var accessor = mmf.CreateViewAccessor(0, structSize, MemoryMappedFileAccess.ReadWrite))
                {
                    accessor.Write(0, ref data);
                }
            }

            public void Dispose()
            {
                if (mmf != null)
                {
                    mmf.Dispose();
                    mmf = null;
                }
            }

            ~Sender()
            {
                Dispose();
            }
        }

        struct SenderCache
        {
            public static readonly float TimeToDispose = 5;
            public Sender sender;
            public DateTime time;
        }
        public RemoteControlManager()
        {
            GatherWindows();
        }
        public RemoteControlManager(Window owningWindow)
        {
            this.owningWindow = owningWindow;
            thisWindow = new WindowWrapper()
            {
                windowHandle = new WindowInteropHelper(owningWindow).Handle
            };
            GatherWindows();

            foreach (var window in otherWindows)
            {
                SendMessage(window, Message.Handshake, thisWindow.windowHandle.ToInt32());
            }

            new DispatcherTimer(
                TimeSpan.FromMilliseconds(1000), DispatcherPriority.Background,
                delegate
                {
                    List<Guid> sendersToDispose = new List<Guid>();
                    foreach (var sender in cachedSenders)
                    {
                        if ((sender.Value.time - DateTime.Now).TotalSeconds > SenderCache.TimeToDispose)
                        {
                            sendersToDispose.Add(sender.Key);
                        }

                    }

                    foreach (var guid in sendersToDispose)
                    {
                        cachedSenders[guid].sender.Dispose();
                        cachedSenders.Remove(guid);
                    }
                },
                owningWindow.Dispatcher);
        }

        public List<WindowWrapper> otherWindows = new List<WindowWrapper>();
        public List<WindowWrapper> AllWindows
        {
            get
            {
                List<WindowWrapper> allWindows = new List<WindowWrapper>();
                allWindows.Add(thisWindow);
                allWindows.AddRange(otherWindows);
                return allWindows;
            }
        }
        private Dictionary<Guid, SenderCache> cachedSenders = new Dictionary<Guid, SenderCache>();

        Window owningWindow;
        public WindowWrapper thisWindow;
        QueryHandle reservedQueryHandle;

        public void GatherWindows()
        {
            otherWindows.Clear();
            foreach (var process in Process.GetProcesses())
            {
                if (process.MainWindowTitle.Contains(MainWindow.WindowTitle) && process.MainWindowHandle != thisWindow.windowHandle)
                {
                    AddWindow(process.MainWindowHandle);
                }
            }
        }

        public void AddWindow(IntPtr hwnd)
        {
            otherWindows.Add(new WindowWrapper()
            {
                windowHandle = hwnd,
            });
        }

        public void RemoveWindow(IntPtr hwnd)
        {
            for (int i = 0; i < otherWindows.Count; i++)
            {
                if (otherWindows[i].windowHandle.Equals(hwnd))
                {
                    otherWindows.RemoveAt(i);
                    return;
                }
            }
        }

        private void CacheSender(Sender s, QueryHandle handle)
        {
            cachedSenders.Add(handle.guid, new SenderCache()
            {
                sender = s,
                time = DateTime.Now
            });
        }


        public IntPtr SendMessage(WindowWrapper window, Message message, int wParam = 0, int lParam = 0)
            => SendMessage(window, (int)message, wParam, lParam);
        public IntPtr SendMessage(WindowWrapper window, int message, int wParam = 0, int lParam = 0)
            => SendMessage(window.windowHandle, message, new IntPtr(wParam), new IntPtr(lParam));
        public IntPtr SendMessage(IntPtr window, Message message, int wParam = 0, int lParam = 0)
            => SendMessage(window, (int)message, new IntPtr(wParam), new IntPtr(lParam));

        public List<IntPtr> BroadcastMessage(Message message, int wParam = 0, int lParam = 0, bool all = false)
        {
            List<IntPtr> results = new List<IntPtr>();
            foreach (var window in (all ? AllWindows : otherWindows))
            {
                results.Add(SendMessage(window, message, wParam, lParam));
            }
            return results;
        }

        /// <summary>
        /// Sends a post as a raw message. This is useful if you want to manage the posting and disposing of the data struct yourself
        /// </summary>
        public IntPtr PostDirect(WindowWrapper window, Post postType)
        {
            return PostDirect(window, postType, 0, 0);
        }

        public IntPtr PostDirect(WindowWrapper window, Post postType, int wParam = 0, int lParam = 0)
        {
            return SendMessage(window, (int)postType, wParam, lParam);
        }

        public IntPtr Post<T>(WindowWrapper window, Post messageType, T dataStruct) where T : struct
            => Post(window, messageType, dataStruct, Marshal.SizeOf<T>());
        public IntPtr Post<T>(WindowWrapper window, Post messageType, T dataStruct, int structSize) where T : struct
        {
            using (Sender sender = new Sender())
            {
                sender.PostData(dataStruct, structSize);
                return SendMessage(window, (Message)messageType);
            }
        }

        /// <summary>
        /// Must be disposed of before another post is made
        /// This is only useful for managing your own posts, if you want multiple recievers to read a single post
        /// </summary>
        public IDisposable PostData<T>(T dataStruct) where T : struct
        {
            return PostData(dataStruct, Marshal.SizeOf<T>());
        }

        /// <summary>
        /// Must be disposed of before another post is made
        /// This is only useful for managing your own posts, if you want multiple recievers to read a single post
        /// </summary>
        public IDisposable PostData<T>(T dataStruct, int structSize) where T : struct
        {
            Sender sender = new Sender();
            sender.PostData(dataStruct, structSize);
            return sender;
        }

        public bool RecieveData<T>(out T result) where T : struct => RecieveData(Marshal.SizeOf<T>(), out result);
        public bool RecieveData<T>(int structSize, out T result) where T : struct
            => RecieveDataInternal(RemoteControlImmediateFile, structSize, out result);
        public bool RecieveDataFromQuery<T>(QueryHandle handle, out T result) where T : struct
            => RecieveDataFromQuery(handle, Marshal.SizeOf<T>(), out result);
        public bool RecieveDataFromQuery<T>(QueryHandle handle, int structSize, out T result) where T : struct
        {
            if (!handle.IsValid())
            {
                throw new Exception("Query handle is invalid");
            }

            handle.Invalidate();
            return RecieveDataInternal(handle.FileName, structSize, out result);
        }

        bool RecieveDataInternal<T>(string fileName, int structSize, out T result) where T : struct
        {
            try
            {
                using (var mmf = MemoryMappedFile.OpenExisting(fileName, MemoryMappedFileRights.Read))
                using (var accessor = mmf.CreateViewAccessor(0, structSize, MemoryMappedFileAccess.Read))
                {
                    accessor.Read(0, out result);
                }
            }
            catch (Exception)
            {
                result = new();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Posts a query and returns the resulting struct in a simplified way
        /// </summary>
        /// <typeparam name="T">The struct you want to query. May not contain references or members of variable size</typeparam>
        /// <param name="window">A target window to send the query to</param>
        /// <param name="query"></param>
        /// <returns></returns>
        public T Query<T>(WindowWrapper window, Query query) where T : struct
        {
            var result = PostQuery(window, query, out QueryHandle handle);
            if (result.Equals(-1)) return new();

            var dataResult = RecieveDataFromQuery(handle, out T data);
            if (!dataResult) return new();

            result = SendMessage(handle.targetWindowHandle, Message.DisposeQueryHandle);
            if (result.Equals(-1)) return new();
            return data;
        }

        public IntPtr PostQuery(WindowWrapper window, Query query, out QueryHandle handle)
        {
            handle = new QueryHandle();
            handle.GenerateNewGuid();
            handle.targetWindowHandle = window.windowHandle;

            using (Sender sender = new Sender())
            {
                sender.PostData(handle, Marshal.SizeOf(handle));

                var result = SendMessage(window, Message.ReserveQueryHandle);

                if (result.Equals(-1))
                {
                    return result;
                }
            }

            return SendMessage(window, (Message)query);
        }

        public void PostQueryData<T>(T dataStruct) where T : struct
        {
            if (!reservedQueryHandle.IsValid())
            {
                throw new Exception("Query handle is invalid");
            }

            Sender sender = new Sender();
            sender.PostData(dataStruct, Marshal.SizeOf<T>(), reservedQueryHandle);

            CacheSender(sender, reservedQueryHandle);
        }

        public bool TryReserveQueryHandle()
        {
            if (reservedQueryHandle.IsValid())
            {
                throw new Exception("Previous handle was not closed before opening new handle!");
                //return false;
            }

            return RecieveData(out reservedQueryHandle);
        }

        public bool TryDisposeQueryHandle()
        {
            if (!reservedQueryHandle.IsValid())
            {
                throw new Exception("No handle to invalidate!");
                //return false;
            }

            if (cachedSenders.ContainsKey(reservedQueryHandle.guid))
            {
                cachedSenders[reservedQueryHandle.guid].sender.Dispose();
                cachedSenders.Remove(reservedQueryHandle.guid);
            }

            reservedQueryHandle.Invalidate();
            return true;
        }

    }
}

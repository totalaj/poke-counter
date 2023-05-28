using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace PokeCounter
{
    public partial class App : Application
    {
        // Delegate to filter which windows to include 
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            string startupFile = null;

            for (int i = 0; i < e.Args.Length; i++)
            {
                string arg = e.Args[i];
                if (File.Exists(arg))
                {
                    startupFile = arg;
                }
            }

            if (Path.GetExtension(startupFile) == CounterProfile.GroupFileExtension)
            {
                var rcm = new RemoteControlManager();
                int groupIndex = PokeCounter.MainWindow.GetNextAvailableGroupIndex(rcm);
                CounterGroup group = JsonConvert.DeserializeObject<CounterGroup>(File.ReadAllText(startupFile));
                List<Process> processes = new List<Process>();
                foreach (var counter in group.counters)
                {
                    var process = Process.Start(Paths.Executable, $"\"{counter.profilePath}\" -g {groupIndex} -h -skipReload " +
                        $"-y {counter.layout.WindowTop} -x {counter.layout.WindowLeft}");
                    processes.Add(process);
                }

                bool cont = true;
                const int maxAttempts = 60;

                for (int i = 0; i < maxAttempts; i++)
                {
                    System.Threading.Thread.Sleep(1000);
                    cont = false;
                    try
                    {
                        for (int processIndex = processes.Count - 1; processIndex >= 0; processIndex--)
                        {
                            var process = processes[processIndex];
                            bool enumeratedWindows = false;
                            EnumWindows(delegate (IntPtr wnd, IntPtr param)
                            {
                                GetWindowThreadProcessId(wnd, out uint id);

                                if (id != process.Id)
                                {
                                    return true;
                                }

                                enumeratedWindows = true;

                                var result = rcm.SendMessage(wnd, Message.Ping).ToInt32();
                                if (result != 1)
                                {
                                    cont = true;
                                }
                                else
                                {
                                    processes.RemoveAt(processIndex);
                                }
                                return true;
                            }, IntPtr.Zero);

                            if (!enumeratedWindows) cont = true;
                        }
                        if (!cont)
                        {
                            break;
                        }
                    }
                    catch (Exception exception)
                    {
                        MessageBox.Show(exception.Message);
                    }
                }

                if (cont)
                {
                    var result = MessageBox.Show($"Couldn't successfully open {processes.Count} counters! Continue anyway?", "Failure!", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (result == MessageBoxResult.No)
                    {
                        rcm.GatherWindows();
                        rcm.BroadcastMessage(Message.Close);
                        foreach (var process in processes)
                        {
                            process.Kill();
                        }

                        Shutdown();
                        return;
                    }
                }

                rcm.GatherWindows();
                rcm.BroadcastMessage(Message.RefreshOtherWindows);
                rcm.BroadcastMessage(Message.Show);

                foreach (var process in processes)
                {
                    process.Kill();
                }

                Shutdown();
                return;
            }

            MainWindow wnd = new MainWindow(e.Args);

            wnd.Show();
            MainWindow = wnd;
        }
    }
}
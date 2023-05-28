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

                    rcm.GatherWindows();
                    if (rcm.otherWindows.Count != processes.Count) cont = true;
                    foreach (var window in rcm.otherWindows)
                    {
                        if (rcm.SendMessage(window, Message.Ping).ToInt32() != 1)
                        {
                            cont = true;
                        }
                    }

                    for (int processIndex = processes.Count - 1; processIndex >= 0; processIndex--)
                    {
                        if (processes[processIndex].HasExited)
                        {
                            processes.RemoveAt(processIndex);
                        }
                    }

                    if (!cont)
                    {
                        break;
                    }
                }

                if (cont)
                {
                    var result = MessageBox.Show($"Couldn't successfully open all counters! Continue anyway?", "Failure!", MessageBoxButton.YesNo, MessageBoxImage.Warning);
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

                Shutdown();
                return;
            }

            MainWindow wnd = new MainWindow(e.Args);
            wnd.Show();
        }
    }
}
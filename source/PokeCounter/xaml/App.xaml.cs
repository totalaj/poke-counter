using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
                int highestCurrentGroup = PokeCounter.MainWindow.GetNextAvailableGroupIndex(rcm);
                CounterGroup group = JsonConvert.DeserializeObject<CounterGroup>(File.ReadAllText(startupFile));
                List<Process> processes = new List<Process>();
                foreach (var counter in group.counters)
                {
                    var process = Process.Start(Paths.Executable, $"\"{counter.profilePath}\" -g {highestCurrentGroup + 1} -h -skipReload " +
                        $"-y {counter.layout.WindowTop} -x {counter.layout.WindowLeft}");
                    processes.Add(process);
                }

                bool cont = true;

                while (cont)
                {
                    cont = false;
                    foreach (var process in processes)
                    {
                        if (rcm.SendMessage(process.MainWindowHandle, Message.Ping) == IntPtr.Zero)
                        {
                            cont = true;
                        }
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
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace PokeCounter
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            string startupFile = null;
            bool markAsNew = false;

            foreach (var arg in e.Args)
            {
                if (File.Exists(arg))
                {
                    startupFile = arg;
                }
                if (arg.StartsWith("-n"))
                {
                    markAsNew = true;
                }
            }
            MainWindow wnd = new MainWindow(startupFile, markAsNew);

            wnd.Show();
        }
    }
}
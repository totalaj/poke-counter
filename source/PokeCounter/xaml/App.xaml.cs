using System;
using System.Collections.Generic;
using System.Windows;

namespace PokeCounter
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            string startupFile = null;
            if (e.Args.Length == 1)
                startupFile = e.Args[0];
            MainWindow wnd = new MainWindow(startupFile);

            wnd.Show();
        }
    }
}
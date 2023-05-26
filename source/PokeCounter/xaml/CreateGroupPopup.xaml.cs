using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PokeCounter
{
    /// <summary>
    /// Interaction logic for CreateGroupPopup.xaml
    /// </summary>
    public partial class CreateGroupPopup : Window
    {
        public delegate void Finished(bool success);
        RemoteControlManager rcm;
        MainWindow owningWindow;
        public CreateGroupPopup(int groupIndex, MainWindow owningWindow, RemoteControlManager rcm)
        {
            InitializeComponent();
            this.owningWindow = owningWindow;
            this.rcm = rcm;

            owningWindow.SetGroupEligibilityMode(true);
            owningWindow.SetEligibleForGroup(true);

            foreach (var window in rcm.AllWindows)
            {
                rcm.SendMessage(window, Message.EnterGroupElegibilityMode);
                if (rcm.SendMessage(window, Message.GetGroup).ToInt32() == groupIndex || window.windowHandle == rcm.thisWindow.windowHandle)
                {
                    rcm.SendMessage(window, Message.SetEligibleForGroup, 1);
                }
            }
        }

        bool init = false;
        bool success = false;
        public Finished onFinished;

        public void Complete()
        {
            int groupCount = 0;
            foreach (var window in rcm.AllWindows)
            {
                if (rcm.SendMessage(window, Message.GetEligibleForGroup).ToInt32() == 1)
                {
                    groupCount++;
                }
            }

            success = groupCount > 1;
            Close();
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            Complete();
        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!init)
            {
                init = true;
            }
        }

        private void PopupWindow_Closed(object sender, EventArgs e)
        {
            onFinished?.Invoke(success);

            rcm.BroadcastMessage(Message.ExitGroupElegibilityMode, all: true);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void PopupWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed) Close();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PokeCounter
{
    /// <summary>
    /// Interaction logic for PressAnyButtonPopup.xaml
    /// </summary>
    public partial class PressAnyButtonPopup : Window
    {
        public List<GlobalHotkey> occupiedKeys;
        bool init = false;
        public PressAnyButtonPopup(string valueName, List<GlobalHotkey> occupiedKeys)
        {
            InitializeComponent();
            this.occupiedKeys = occupiedKeys;
            PopupWindow.Title = "Set " + valueName;
            SetAcceptingInput(true);
            InvalidText.Visibility = Visibility.Collapsed;
        }

        public GlobalHotkey value;
        public bool acceptingInput, validInput;

        public void Complete()
        {
            bool result = true;

            DialogResult = result;
            Close();
        }
        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            Complete();
        }

        void UpdateKeyText()
        {
            KeyText.Content = value.ToString();
        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!init)
            {
                init = true;
            }
        }

        List<Keys> disallowedKeys = new List<Keys>()
        {
            Keys.ShiftKey, Keys.LShiftKey, Keys.RShiftKey,
            Keys.Control, Keys.ControlKey, Keys.LControlKey, Keys.RControlKey,
            Keys.Alt,
            Keys.RWin, Keys.LWin
        };

        private void PopupWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (!acceptingInput) return;
            e.Handled = true;
            var key = (Keys)KeyInterop.VirtualKeyFromKey(e.Key);

            if (disallowedKeys.Contains(key)) key = Keys.None;

            var hotkey = new GlobalHotkey(key, e.KeyboardDevice.Modifiers);

            validInput = !occupiedKeys.Contains(hotkey);

            InvalidText.Visibility = validInput ? Visibility.Collapsed : Visibility.Visible;

            value = hotkey;
            UpdateKeyText();
        }

        private void PopupWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed) Close();
            if (e.LeftButton == MouseButtonState.Pressed) SetAcceptingInput(true);
        }

        private void PopupWindow_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            SetAcceptingInput(false);
        }

        public void SetAcceptingInput(bool acceptingInput)
        {
            this.acceptingInput = acceptingInput;
            KeyText.Background = acceptingInput ? new SolidColorBrush() : new SolidColorBrush(Color.FromRgb(230, 230, 230));
            ClickLabel.Visibility = acceptingInput ? Visibility.Collapsed : Visibility.Visible;
            if (acceptingInput)
            {
                KeyText.Content = "Press any key...";
            }
        }
    }
}

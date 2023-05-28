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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PokeCounter
{
    /// <summary>
    /// Interaction logic for KeybindingInput.xaml
    /// </summary>
    public partial class KeybindingInput : System.Windows.Controls.UserControl
    {
        public KeybindingInput()
        {
            InitializeComponent();
        }

        public KeybindingInput(List<KeyCombination> occupiedKeys, KeybindingWrapper wrapper, Window owningWindow)
        {
            InitializeComponent();
            this.occupiedKeys = occupiedKeys;
            this.wrapper = wrapper;
            NameLabel.Content = wrapper.name;
            validInput = true;
            originalValue = new KeyCombination(wrapper.value);
            localValue = new KeyCombination(wrapper.value);

            SetAcceptingInput(false);
            UpdateKeyText();

            owningWindow.KeyDown += KeybindingInput_KeyDown;
            owningWindow.KeyUp += KeybindingInput_KeyUp;
        }

        readonly List<KeyCombination> occupiedKeys;

        KeyCombination originalValue;
        KeyCombination localValue;
        KeybindingWrapper wrapper;
        public Action focused;
        public bool acceptingInput, validInput;
        string invalidReason = "";

        void UpdateKeyText()
        {
            KeyText.Content = localValue.ToString();
        }

        static readonly List<Key> disallowedKeys = new List<Key>()
        {
            Key.LeftShift, Key.RightShift,
            Key.LeftCtrl, Key.RightCtrl,
            Key.LeftAlt, Key.RightAlt, Key.System,
            Key.RWin, Key.LWin
        };

        private void KeybindingInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (!acceptingInput || !IsFocused) return;
            e.Handled = true;

            var key = e.SystemKey;
            if (key == Key.None) key = e.Key;

            validInput = true;

            if (disallowedKeys.Contains(key))
            {
                validInput = false;
                key = Key.None;
            }

            KeyCombination hotkey;

            if (e.Key == Key.Escape)
            {
                hotkey = new KeyCombination(originalValue);
            }
            else
            {
                hotkey = new KeyCombination(key, e.KeyboardDevice.Modifiers);
            }
            TrySetHotkey(hotkey);
        }

        void TrySetHotkey(KeyCombination hotkey)
        {
            validInput = wrapper.IsValid(hotkey, out invalidReason);

            if (hotkey != wrapper.value && validInput)
            {
                wrapper.onUpdated?.Invoke(wrapper.value, hotkey);
                wrapper.value = new KeyCombination(hotkey);
            }

            localValue = new KeyCombination(hotkey);

            UpdateKeyText();
        }

        private void KeybindingInput_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (!IsFocused) return;
            SetAcceptingInput(false);
        }

        private void KeybindingInput_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                originalValue = wrapper.value;
                SetAcceptingInput(true);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            TrySetHotkey(wrapper.defaultValue);
            SetAcceptingInput(false);
        }

        public void SetAcceptingInput(bool acceptingInput)
        {
            this.acceptingInput = acceptingInput;
            KeyText.Background = acceptingInput ? new SolidColorBrush(Color.FromRgb(230, 230, 230)) : new SolidColorBrush();
            SecondaryLabel.Visibility = acceptingInput ? Visibility.Collapsed : Visibility.Visible;

            if (validInput)
            {
                SecondaryLabel.Content = "Click to rebind";
                SecondaryLabel.Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 0));
            }
            else
            {
                SecondaryLabel.Content = invalidReason;
                SecondaryLabel.Foreground = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            }

            if (acceptingInput)
            {
                Focus();
                focused?.Invoke();
                KeyText.Content = "Press any key...";
            }
            else
            {
                if (localValue.keys == Key.None) KeyText.Content = "No input!";
                else
                {
                    UpdateKeyText();
                }
            }
        }
    }
}

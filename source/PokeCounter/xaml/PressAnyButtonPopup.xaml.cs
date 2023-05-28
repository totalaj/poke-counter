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
    public delegate bool KeybindingValidator(KeyCombination keyCombination, out string invalidReason);
    public class KeybindingWrapper
    {
        public delegate void BindingFinished(KeyCombination keyCombination);
        public delegate void BindingUpdated(KeyCombination oldKeyCombination, KeyCombination newKeyCombination);

        public KeybindingWrapper()
        {

        }
        public KeybindingWrapper(string name, KeyCombination value, KeyCombination defaultValue, BindingFinished onFinished, KeybindingValidator validator = null)
        {
            this.name = name;
            this.value = value;
            this.defaultValue = defaultValue;
            this.onFinished = onFinished;
            if (validator != null)
                validators.Add(validator);
        }

        public bool IsValid(out string reason)
        {
            return IsValid(value, out reason);
        }

        public bool IsValid(KeyCombination value, out string reason)
        {
            foreach (var validator in validators)
            {
                if (!validator(value, out reason))
                {
                    return false;
                }
            }
            reason = "Valid";
            return true;
        }

        public string name;
        public List<KeybindingValidator> validators = new List<KeybindingValidator>();
        public BindingFinished onFinished = null;
        public BindingUpdated onUpdated = null;
        public KeyCombination value, defaultValue;
        public bool isSeparator;
        public void Finish()
        {
            if (IsValid(out string ignore) && onFinished != null)
                onFinished(value);
        }
    }

    /// <summary>
    /// Interaction logic for PressAnyButtonPopup.xaml
    /// </summary>
    public partial class PressAnyButtonPopup : Window
    {
        List<KeybindingWrapper> keybindingWrappers = new List<KeybindingWrapper>();
        List<KeybindingInput> keybindingInputs = new List<KeybindingInput>();
        public List<KeyCombination> occupiedKeys = new List<KeyCombination>();
        bool init = false;
        public PressAnyButtonPopup(string windowTitle, List<KeyCombination> aOccupiedKeys, List<object> listEntries)
        {
            InitializeComponent();
            occupiedKeys.AddRange(aOccupiedKeys);
            PopupWindow.Title = windowTitle;

            foreach (var entry in listEntries)
            {
                if (entry is KeybindingWrapper keybinding)
                {
                    keybinding.validators.Add(
                        (KeyCombination kc, out string reason) =>
                        {
                            foreach (var key in occupiedKeys)
                            {
                                if (key == kc && key != keybinding.value)
                                {
                                    reason = "Already bound!";
                                    return false;
                                }
                            }
                            reason = "Valid";
                            return true;
                        });

                    KeybindingInput keybindingInput = new KeybindingInput(occupiedKeys, keybinding, this)
                    {
                    };
                    keybindingInput.focused += () =>
                    {
                        foreach (var keybinding in keybindingInputs)
                        {
                            if (!keybinding.IsFocused) keybinding.SetAcceptingInput(false);
                        }
                    };
                    keybinding.onUpdated += (prev, next) =>
                    {
                        occupiedKeys.Remove(prev);
                        occupiedKeys.Add(next);
                    };
                    keybindingWrappers.Add(keybinding);
                    keybindingInputs.Add(keybindingInput);
                    KeybindingStack.Children.Add(keybindingInput);
                }
                else if (entry is UIElement uiElement)
                {
                    KeybindingStack.Children.Add(uiElement);
                }
            }
        }

        public void Complete()
        {
            bool result = true;

            foreach (var wrapper in keybindingWrappers)
            {
                wrapper.Finish();
            }

            DialogResult = result;
            Close();
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            Complete();
        }

        private void PopupWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed) Close();
        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!init)
            {
                init = true;
            }
        }
    }
}

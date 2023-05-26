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
        readonly Func<Keys, bool> validator;
        bool init = false;
        public PressAnyButtonPopup(string valueName, Func<Keys, bool> validator = null)
        {
            InitializeComponent();
            this.validator = validator;
            PopupWindow.Title = "Set " + valueName;
        }

        public Keys value;

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

        private void PopupWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var key = (Keys)KeyInterop.VirtualKeyFromKey(e.Key);

            if (validator != null)
            {
                if (validator(key)) value = key;
            }
            else
            {
                value = key;
            }

            UpdateKeyText();
        }

        private void PopupWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed) Close();
        }
    }
}

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
    /// Interaction logic for SingleValuePopup.xaml
    /// </summary>
    public partial class SingleValuePopup : Window
    {
        readonly Func<int, bool> validator;
        public SingleValuePopup(string valueName, Func<int, bool> validator = null)
        {
            InitializeComponent();
            this.validator = validator;
            PopupWindow.Title = "Set " + valueName;
            valueProperty.Text = "";
            invalidValueText.Visibility = Visibility.Hidden;
        }

        public int value;

        public void Complete()
        {
            bool result = true;

            if (!int.TryParse(valueProperty.Text, out value))
            {
                result = false;
            }

            DialogResult = result;
            Close();
        }

        private void ValueProperty_GotStylusCapture(object sender, StylusEventArgs e)
        {
            valueProperty.SelectAll();
        }

        private void ValueProperty_GotFocus(object sender, RoutedEventArgs e)
        {
            valueProperty.SelectAll();
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            Complete();
        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (valueProperty.Text == "")
            {
                valueProperty.Text = value.ToString();
                valueProperty.Focus();
            }
        }

        private void ValueProperty_TextChanged(object sender, TextChangedEventArgs e)
        {
            bool result = int.TryParse(valueProperty.Text, out value);

            if (result && validator != null) result = validator(value);

            invalidValueText.Visibility = result ? Visibility.Hidden : Visibility.Visible;
        }

        private void PopupWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed) Close();
        }
    }
}

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
    /// Interaction logic for DualValuePopup.xaml
    /// </summary>
    public partial class DualValuePopup : Window
    {
        public DualValuePopup(string titleValueName, string value1Name, string value2Name, Func<int, bool> validator1 = null, Func<int,bool> validator2 = null)
        {
            InitializeComponent();
            this.validator1 = validator1;
            this.validator2 = validator2;

            PopupWindow.Title = "Set " + titleValueName;
            Value1Label.Content = value1Name;
            Value2Label.Content = value2Name;

            Value1Property.Text = "";
            Value2Property.Text = "";

            invalidValueText1.Visibility = Visibility.Hidden;
            invalidValueText2.Visibility = Visibility.Hidden;
        }

        readonly Func<int, bool> validator1;
        readonly Func<int, bool> validator2;

        public int value1;
        public int value2;

        public void Complete()
        {
            bool result = true;

            if (!int.TryParse(Value1Property.Text, out value1))
            {
                result = false;
            }
            if (!int.TryParse(Value2Property.Text, out value2))
            {
                result = false;
            }

            DialogResult = result;
            Close();
        }

        private void Value1Property_GotStylusCapture(object sender, StylusEventArgs e)
        {
            Value1Property.SelectAll();
        }

        private void Value1Property_GotFocus(object sender, RoutedEventArgs e)
        {
            Value1Property.SelectAll();
        }

        private void Value2Property_GotFocus(object sender, RoutedEventArgs e)
        {
            Value2Property.SelectAll();
        }

        private void Value2Property_GotStylusCapture(object sender, StylusEventArgs e)
        {
            Value2Property.SelectAll();
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            Complete();
        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Value1Property.Text == "" && Value2Property.Text == "")
            {
                Value1Property.Text = value1.ToString();
                Value2Property.Text = value2.ToString();
                Value1Property.Focus();
            }
        }

        private void Value1Property_TextChanged(object sender, TextChangedEventArgs e)
        {
            bool result = int.TryParse(Value1Property.Text, out value1);

            if (result && validator1 != null) result = validator1(value1);

            invalidValueText1.Visibility = result ? Visibility.Hidden : Visibility.Visible;
        }

        private void Value2Property_TextChanged(object sender, TextChangedEventArgs e)
        {
            bool result = int.TryParse(Value2Property.Text, out value2);

            if (result && validator2 != null) result = validator2(value2);

            invalidValueText2.Visibility = result ? Visibility.Hidden : Visibility.Visible;
        }
    }
}

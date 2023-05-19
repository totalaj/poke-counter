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
    /// Interaction logic for ColorPickerPopup.xaml
    /// </summary>
    public partial class ColorPickerPopup : Window
    {
        public delegate void ValueUpdated(Color c);
        public delegate void Finished(Color c, bool valueChanged);

        public static readonly RoutedCommand CompleteCommand = new RoutedCommand
    (
        "Complete",
        typeof(ColorPickerPopup),
        new InputGestureCollection()
        {
            new KeyGesture(Key.Enter, ModifierKeys.Control),
            new KeyGesture(Key.Enter, ModifierKeys.Control | ModifierKeys.Shift)
        }
    );

        public ValueUpdated onValueUpdated;
        public Finished onFinished;

        bool init = false;
        public ColorPickerPopup(string valueName, Color value)
        {
            InitializeComponent();
            this.value = value;
            PopupWindow.Title = "Set " + valueName;
        }

        Color value;
        bool valueChanged = false;

        public void Complete()
        {
            value = valuePropertyA.SelectedColor;
            valueChanged = true;

            Close();
        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!init)
            {
                init = true;
                valuePropertyA.SelectedColor = value;
                valuePropertyB.SelectedColor = value;
            }
        }

        private void SquarePicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            valuePropertyB.SelectedColor = valuePropertyA.SelectedColor;
            onValueUpdated?.Invoke(valuePropertyA.SelectedColor);
        }

        private void HexColorTextBox_ColorChanged(object sender, RoutedEventArgs e)
        {
            valuePropertyA.SelectedColor = valuePropertyB.SelectedColor;
            onValueUpdated?.Invoke(valuePropertyA.SelectedColor);
        }

        private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void CommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Complete();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void PopupWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        private void PopupWindow_Closed(object sender, EventArgs e)
        {
            onFinished?.Invoke(value, valueChanged);
        }

        private void PopupWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed) Close();
        }
    }
}

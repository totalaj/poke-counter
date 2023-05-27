using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PokeCounter.Commands
{
    public static class CustomCommands
    {
        public static readonly RoutedUICommand Save = new RoutedUICommand
    (
        "Save",
        "Save",
        typeof(CustomCommands),
        new InputGestureCollection()
        {
         new KeyGesture(Key.S, ModifierKeys.Control),
        }
    );

        public static readonly RoutedUICommand SaveAs = new RoutedUICommand
    (
        "Save as...",
        "Save as...",
        typeof(CustomCommands),
        new InputGestureCollection()
        {
            new KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Shift)
        }
    );

        public static readonly RoutedUICommand Load = new RoutedUICommand
    (
        "Load profile",
        "Load profile",
        typeof(CustomCommands),
        new InputGestureCollection()
        {
            new KeyGesture(Key.O, ModifierKeys.Control)
        }
    );
       public static readonly RoutedUICommand Undo = new RoutedUICommand
    (
        "Undo",
        "Undo",
        typeof(CustomCommands),
        new InputGestureCollection()
        {
            new KeyGesture(Key.Z, ModifierKeys.Control)
        }
    );
       public static readonly RoutedUICommand Redo = new RoutedUICommand
    (
        "Redo",
        "Redo",
        typeof(CustomCommands),
        new InputGestureCollection()
        {
            new KeyGesture(Key.Y, ModifierKeys.Control),
            new KeyGesture(Key.Z, ModifierKeys.Control | ModifierKeys.Shift)
        }
    );
       public static readonly RoutedUICommand New = new RoutedUICommand
    (
        "New profile",
        "New profile",
        typeof(CustomCommands),
        new InputGestureCollection()
        {
            new KeyGesture(Key.N, ModifierKeys.Control)
        }
    );

        public static readonly RoutedUICommand Close = new RoutedUICommand
    (
        "Close",
        "Close",
        typeof(CustomCommands),
        new InputGestureCollection()
        {
            new KeyGesture(Key.W, ModifierKeys.Control)
        }
    );

        public static readonly RoutedUICommand SelectPokemon = new RoutedUICommand
    (
        "Select pokemon",
        "Select pokemon",
        typeof(CustomCommands),
        new InputGestureCollection()
        {
            new KeyGesture(Key.P, ModifierKeys.Control)
        }
    );


        public static readonly RoutedUICommand Duplicate = new RoutedUICommand
    (
        "Duplicate window",
        "Duplicate window",
        typeof(CustomCommands),
        new InputGestureCollection()
        {
            new KeyGesture(Key.D, ModifierKeys.Control)
        }
    );

        public static readonly RoutedUICommand Group = new RoutedUICommand
    (
        "Group",
        "Group",
        typeof(CustomCommands),
        new InputGestureCollection()
        {
            new KeyGesture(Key.G, ModifierKeys.Control)
        }
    );
        public static readonly RoutedUICommand SaveGroup = new RoutedUICommand
    (
        "Save Group",
        "Save Group",
        typeof(CustomCommands),
        new InputGestureCollection()
        {
            new KeyGesture(Key.G, ModifierKeys.Control | ModifierKeys.Shift)
        }
    );

        public static readonly RoutedUICommand ShowOdds = new RoutedUICommand
    (
        "Show Odds",
        "Show Odds",
        typeof(CustomCommands),
        new InputGestureCollection()
        {
            new KeyGesture(Key.O, ModifierKeys.Alt)
        }
    );

        public static readonly RoutedUICommand LockSize = new RoutedUICommand
    (
        "Lock Size",
        "Lock Size",
        typeof(CustomCommands),
        new InputGestureCollection()
        {
            new KeyGesture(Key.L, ModifierKeys.Alt)
        }
    );

        public static readonly RoutedUICommand AlwaysOnTop = new RoutedUICommand
    (
        "Always on top",
        "Always on top",
        typeof(CustomCommands),
        new InputGestureCollection()
        {
            new KeyGesture(Key.T, ModifierKeys.Alt)
        }
    );

    }
}

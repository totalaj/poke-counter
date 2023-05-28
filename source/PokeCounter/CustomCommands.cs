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
        public static List<RoutedUICommand> GetAllCommands()
        {
            List<RoutedUICommand> commands = new List<RoutedUICommand>();
            var fields = typeof(CustomCommands).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

            foreach (var command in fields)
            {
                if (command.GetValue(null) is RoutedUICommand uiCommand)
                {
                    commands.Add(uiCommand);

                }
            }
            return commands;
        }

        public static Dictionary<string, RoutedUICommand> GetAllCommandsMapped()
        {
            var commands = GetAllCommands();
            Dictionary<string, RoutedUICommand> mappedCommands = new Dictionary<string, RoutedUICommand>();

            foreach (var command in commands)
            {
                mappedCommands.Add(command.Name, command);
            }
            return mappedCommands;
        }

        public static RoutedUICommand Save = new RoutedUICommand
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
        "saveAsCommand",
        typeof(CustomCommands),
        new InputGestureCollection()
        {
            new KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Shift)
        }
    );

        public static readonly RoutedUICommand Load = new RoutedUICommand
    (
        "Load profile",
        "loadCommand",
        typeof(CustomCommands),
        new InputGestureCollection()
        {
            new KeyGesture(Key.O, ModifierKeys.Control)
        }
    );
       public static readonly RoutedUICommand Undo = new RoutedUICommand
    (
        "Undo",
        "undoCommand",
        typeof(CustomCommands),
        new InputGestureCollection()
        {
            new KeyGesture(Key.Z, ModifierKeys.Control)
        }
    );
       public static readonly RoutedUICommand Redo = new RoutedUICommand
    (
        "Redo",
        "redoCommand",
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
        "newCommand",
        typeof(CustomCommands),
        new InputGestureCollection()
        {
            new KeyGesture(Key.N, ModifierKeys.Control)
        }
    );

        public static readonly RoutedUICommand Close = new RoutedUICommand
    (
        "Close",
        "closeCommand",
        typeof(CustomCommands),
        new InputGestureCollection()
        {
            new KeyGesture(Key.W, ModifierKeys.Control)
        }
    );

        public static readonly RoutedUICommand SelectPokemon = new RoutedUICommand
    (
        "Select pokemon",
        "selectPokemonCommand",
        typeof(CustomCommands),
        new InputGestureCollection()
        {
            new KeyGesture(Key.P, ModifierKeys.Control)
        }
    );


        public static readonly RoutedUICommand Duplicate = new RoutedUICommand
    (
        "Duplicate window",
        "duplicateCommand",
        typeof(CustomCommands),
        new InputGestureCollection()
        {
            new KeyGesture(Key.D, ModifierKeys.Control)
        }
    );

        public static readonly RoutedUICommand Group = new RoutedUICommand
    (
        "Group",
        "groupCommand",
        typeof(CustomCommands),
        new InputGestureCollection()
        {
            new KeyGesture(Key.G, ModifierKeys.Control)
        }
    );
        public static readonly RoutedUICommand SaveGroup = new RoutedUICommand
    (
        "Save Group",
        "saveGroupCommand",
        typeof(CustomCommands),
        new InputGestureCollection()
        {
            new KeyGesture(Key.G, ModifierKeys.Control | ModifierKeys.Shift)
        }
    );

        public static readonly RoutedUICommand ShowOdds = new RoutedUICommand
    (
        "Show Odds",
        "showOddsCommand",
        typeof(CustomCommands),
        new InputGestureCollection()
        {
            new KeyGesture(Key.O, ModifierKeys.Alt)
        }
    );

        public static readonly RoutedUICommand LockSize = new RoutedUICommand
    (
        "Lock Size",
        "lockSizeCommand",
        typeof(CustomCommands),
        new InputGestureCollection()
        {
            new KeyGesture(Key.L, ModifierKeys.Alt)
        }
    );

        public static readonly RoutedUICommand AlwaysOnTop = new RoutedUICommand
    (
        "Always on top",
        "alwaysOnTopCommand",
        typeof(CustomCommands),
        new InputGestureCollection()
        {
            new KeyGesture(Key.T, ModifierKeys.Alt)
        }
    );

        public static readonly RoutedUICommand Resize = new RoutedUICommand
    (
        "Resize",
        "resizeCommand",
        typeof(CustomCommands),
        new InputGestureCollection()
        {
            new KeyGesture(Key.R, ModifierKeys.Control)
        }
    );

    }
}

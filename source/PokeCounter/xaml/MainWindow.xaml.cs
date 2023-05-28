using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfAnimatedGif;

namespace PokeCounter
{
    public class MetaSettings
    {
        public string settingsPath = "settings.json";
        public string lastProfilePath = "";
        public HashSet<string> recentProfiles = new HashSet<string>();
        public PokemonInfoList pokemonDatas = null;
        public bool topmost = true;
        public bool autosave = true;
        public Dictionary<string, KeyCombination> customKeybinds = new Dictionary<string, KeyCombination>();

        public Key incrementKey = Key.Up, decrementKey = Key.Down;

        public bool Verify()
        {
            return recentProfiles.RemoveWhere(s => !File.Exists(s)) > 0;
        }
    }
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Variables
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        // Const variables
        readonly FloatToStringMap oddsMap = new FloatToStringMap(new List<KeyValuePair<float, string>>
        {
            new KeyValuePair<float, string>(0, "You've got a long ways to go..."),
            new KeyValuePair<float, string>(.05f, "Keep going"),
            new KeyValuePair<float, string>(.2f, "Getting somewhere"),
            new KeyValuePair<float, string>(.5f, "Halfway there!"),
            new KeyValuePair<float, string>(.75f, "Approaching odds"),
            new KeyValuePair<float, string>(.90f, "So close!"),
            new KeyValuePair<float, string>(1f, "At odds"),
            new KeyValuePair<float, string>(1.1f, "Over odds..."),
            new KeyValuePair<float, string>(1.5f, "Way over odds..."),
            new KeyValuePair<float, string>(2f, "Double odds >:("),
            new KeyValuePair<float, string>(3f, "Plain unlucky"),
            new KeyValuePair<float, string>(5f, "Maybe you're doing something wrong?"),
            new KeyValuePair<float, string>(10f, "Whatever you do, don't give up"),
            new KeyValuePair<float, string>(20f, "You can still do this!"),
            new KeyValuePair<float, string>(30f, "Patience is a virtue..."),
            new KeyValuePair<float, string>(40f, "\"Den som väntar på nåt gott...\""),
            new KeyValuePair<float, string>(50f, "Try not to go insane"),
            new KeyValuePair<float, string>(100f, "100 times odds..."),
            new KeyValuePair<float, string>(1000f, "Maybe by now you should give up"),
            new KeyValuePair<float, string>(10000f, "Heat death of the universe type beat")
        });
        public const int VK_LBUTTON = 0x01;

        public const double DefaultWidth = 180, DefaultHeight = 240;
        public const double MinimumWidth = 100, MinimumHeight = 100;

        string CommonGroupFile => Path.Combine(Paths.CacheDirectory, "CurrentGroupFile.txt");

        // Variables
        KeyboardHook incrementHook = new KeyboardHook();
        KeyboardHook decrementHook = new KeyboardHook();

        CounterProfile currentProfile = null;
        readonly SettingsFile<MetaSettings> metaSettings = new SettingsFile<MetaSettings>("metaSettings.json", SourceFolderType.Documents);
        readonly UndoList<CounterProfile> undoList = new UndoList<CounterProfile>();

        public static string WindowTitle = "PokeCounter Window";

        bool MouseIsDown => GetAsyncKeyState(VK_LBUTTON) < 0;
        bool communicable = false;

        #endregion

        #region Main Window

        public MainWindow(string[] args)
        {
            InitializeComponent();
            Title = WindowTitle;

            var commands = Commands.CustomCommands.GetAllCommandsMapped();

            foreach (var keyValuePair in commands)
            {
                List<KeyCombination> keyCombinations = new List<KeyCombination>();

                foreach (var keyCombination in keyValuePair.Value.InputGestures)
                {
                    if (keyCombination is KeyGesture kg)
                        keyCombinations.Add(new KeyCombination(kg.Key, kg.Modifiers));
                }

                originalKeybindings.Add(keyValuePair.Key, keyCombinations);
            }

            startupArguments = new MainWindowArguments(args);

            MinHeight = MinimumHeight;
            MinWidth = MinimumWidth;

            if (!startupArguments.skipReload)
            {
                RefreshPokemonDatas();
                EnsureFileAssociation();
                if (metaSettings.data.Verify())
                {
                    metaSettings.Save();
                }
            }

            AutosaveOption.IsChecked = metaSettings.data.autosave;

            if (startupArguments.forceDefault)
            {
                InitializeFromProfile(CounterProfile.CreateDefault(), true);
            }

            if (File.Exists(startupArguments.startupFile) && currentProfile == null)
            {
                OpenFile(startupArguments.startupFile);
            }
            if (File.Exists(metaSettings.data.lastProfilePath) && currentProfile == null)
            {
                OpenFile(metaSettings.data.lastProfilePath);
            }

            if (currentProfile == null)
            {
                InitializeFromProfile(CounterProfile.CreateDefault(), true);
            }

            undoList.PushChange(currentProfile);

            new DispatcherTimer(
                TimeSpan.FromMilliseconds(10), DispatcherPriority.Background,
                delegate
                {
                    if (resizeDirtyFlag && !MouseIsDown)
                    {
                        ResizingText.Visibility = Visibility.Collapsed;
                        resizeDirtyFlag = false;
                        currentProfile.windowWidth = Width;
                        currentProfile.windowHeight = Height;

                        PushChange();
                    }

                    UpdateCanResize();
                },
                Dispatcher);

            new DispatcherTimer(
                TimeSpan.FromMilliseconds(1000 * 60 * 5), DispatcherPriority.Background,
                delegate
                {
                    if (metaSettings.data.autosave)
                    {
                        Commands.CustomCommands.Save.Execute(true, this);
                    }
                },
                Dispatcher);

            Dispatcher.Invoke(EscapeTracker);
            RefreshAll();
            SetAlwaysOnTopOption(metaSettings.data.topmost, true);
            SetEdgeHighlights(0);

            metaSettings.onSaved += () =>
            {
                rcm.BroadcastMessage(Message.RefreshMetaSettings);
            };

            if (startupArguments.groupIndex != -1)
            {
                groupIndex = startupArguments.groupIndex;
                groupMode = true;
                groupShutdown = true;
            }

            if (startupArguments.markAsNew)
            {
                currentProfile.path = null;
                currentProfile.SetIsDirty(true);
            }

            if (startupArguments.xPosition.exists && startupArguments.yPosition.exists)
            {
                Left = startupArguments.xPosition;
                Top = startupArguments.yPosition;
                currentProfile.windowTop = Top;
                currentProfile.windowLeft = Left;
                currentProfile.Save();
            }

            var mappedCommands = Commands.CustomCommands.GetAllCommandsMapped();
            foreach (var keybind in metaSettings.data.customKeybinds)
            {
                if (mappedCommands.TryGetValue(keybind.Key, out RoutedUICommand command))
                {
                    command.InputGestures.Clear();
                    command.InputGestures.Add(keybind.Value.Gesture);
                }
            }

            KeybindingsUpdated();

            System.Windows.Application.Current.MainWindow = this;
            communicable = true;

            if (startupArguments.startHidden)
            {
                Topmost = false;
            }
        }

        public class MainWindowArguments : ArgumentParser
        {
            public MainWindowArguments(string[] args) : base(args) { }

            public FileArgument startupFile = new FileArgument();
            public IntArgument groupIndex = new IntArgument("-g", -1);
            public BoolArgument markAsNew = new BoolArgument("-n");
            public BoolArgument startHidden = new BoolArgument("-h");
            public BoolArgument skipReload = new BoolArgument("-skipReload");
            public BoolArgument forceDefault = new BoolArgument("-default");
            public DoubleArgument xPosition = new DoubleArgument("-x", double.NaN);
            public DoubleArgument yPosition = new DoubleArgument("-y", double.NaN);
        }

        MainWindowArguments startupArguments;

        private void CounterWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (groupEligibilityMode)
                {
                    SetEligibleForGroup(!eligibleForGroup);
                }
                else
                {
                    UpdateOtherLayouts();
                    DragMove();
                    FinishDragging();
                }
            }
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                Commands.CustomCommands.Close.Execute(null, this);
            }
        }

        private void CounterWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == metaSettings.data.incrementKey || e.Key == Key.Enter)
            {
                IncrementCounter(currentProfile.incrementAmount);
            }
            if (e.Key == metaSettings.data.decrementKey)
            {
                IncrementCounter(-currentProfile.incrementAmount);
            }

            switch (e.Key)
            {
                case Key.Escape:
                    escapeHeld = true;
                    break;
                default:
                    break;
            }
        }

        private void CounterWindow_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    escapeHeld = false;
                    break;
                default:
                    break;
            }
        }

        List<Window> AllPopups => new List<Window>()
            {
                backgroundColorPickerPopup,
                createGroupPopup,
                statTextColorPickerPopup,
                textColorPickerPopup
            };

        bool CanClose()
        {
            bool canClose = true;
            AllPopups.ForEach((Window w) =>
            {
                if (w != null)
                {
                    if (w.IsLoaded)
                    {
                        canClose = false;
                    }
                }
            });

            if (groupEligibilityMode) canClose = false;

            return canClose;
        }

        bool forceClosing = false;
        private void CounterWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (currentProfile.GetIsDirty())
            {
                MessageBoxResult result = MessageBoxResult.Yes;

                if (!metaSettings.data.autosave)
                {
                    result = System.Windows.MessageBox.Show("You have unsaved changes!\nWould you like to save?", currentProfile.Name, MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                }

                if (result == MessageBoxResult.Yes)
                {
                    bool saveSucceeded = false;
                    if (currentProfile.path == null)
                    {
                        if (!metaSettings.data.autosave)
                            saveSucceeded = currentProfile.SaveAs();
                        else // Basically ignore "save as" if autosave is on
                            saveSucceeded = true;
                    }
                    else
                    {
                        saveSucceeded = currentProfile.Save();
                    }

                    if (!saveSucceeded)
                    {
                        e.Cancel = true;
                        return;
                    }
                }
                if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }
            rcm.BroadcastMessage(Message.Disconnect, rcm.thisWindow.windowHandle.ToInt32());

            if (!forceClosing && groupShutdown)
            {
                foreach (var window in rcm.otherWindows)
                {
                    if (rcm.SendMessage(window, Message.GetGroup).ToInt32() == groupIndex
                        && rcm.SendMessage(window, Message.GetGroupShutdown).ToInt32() == 1)
                    {
                        rcm.SendMessage(window, Message.Close, 1);
                    }
                }
            }

            foreach (var popup in AllPopups)
            {
                if (popup != null)
                {
                    popup.Close();
                }
            }
        }

        private void CounterWindow_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (groupEligibilityMode)
            {
                e.Handled = true;
                return;
            }

            while (RecentProfilesOption.Items.Count > 3)
            {
                RecentProfilesOption.Items.RemoveAt(3);
            }

            if (metaSettings.data.recentProfiles.Count == 0)
            {
                RecentProfilesOption.IsEnabled = false;
            }
            else
            {
                RecentProfilesOption.IsEnabled = true;
            }

            bool dirty = (currentProfile as IDirtyable).GetIsDirty();
            CurrentFileFullNameLabel.Header = currentProfile.path + (dirty ? "*" : "");
            CurrentFileNameLabel.Header = System.IO.Path.GetFileName(currentProfile.path) + (dirty ? "*" : "");

            UngroupOption.Visibility = groupMode ? Visibility.Visible : Visibility.Collapsed;
            MakeGroupOption.IsEnabled = !groupMode;

            foreach (var profile in metaSettings.data.recentProfiles.Reverse())
            {
                if (profile == currentProfile.path) continue;
                var mi = new MenuItem();
                mi.Header = profile;
                mi.Click += RecentProfile_Click;
                CounterProfile profileObject = JsonConvert.DeserializeObject<CounterProfile>(File.ReadAllText(profile));

                string iconPath = "";

                if (profileObject.cachedPokemonIndex != -1)
                {
                    if (metaSettings.data.pokemonDatas.pokemon.Count > profileObject.cachedPokemonIndex)
                    {
                        var pokemonInfo = metaSettings.data.pokemonDatas.pokemon[profileObject.cachedPokemonIndex];
                        string iconUrl = SelectPokemonPopup.GetIconURLFromPokemon(pokemonInfo);
                        if (iconUrl != null)
                        {
                            iconPath = DownloadManager.DownloadPokemonImage(iconUrl);
                        }
                    }
                }
                else if (File.Exists(profileObject.backgroundImagePath))
                {
                    iconPath = profileObject.backgroundImagePath;
                }

                if (File.Exists(iconPath))
                {
                    BitmapImage bmp = new BitmapImage(new Uri(iconPath));

                    Image image = new Image();

                    FileInfo finfo = new FileInfo(iconPath);

                    if (finfo.Extension == ".gif")
                    {
                        ImageBehavior.SetAnimatedSource(image, bmp);
                        image.Source = null;
                    }
                    else
                    {
                        ImageBehavior.SetAnimatedSource(image, null);
                        image.Source = bmp;
                    }

                    image.Stretch = Stretch.Uniform;
                    RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

                    mi.Icon = image;
                }

                RecentProfilesOption.Items.Add(mi);
            }

            RebindCommandsOption.Items.Clear();

            SelectPokemonOption.IsEnabled = DownloadManager.IsOnline();
            RefreshContextMenuTickboxes();
        }

        private void CounterWindow_DragLeave(object sender, System.Windows.DragEventArgs e)
        {
            FileDropOverlay.Visibility = Visibility.Collapsed;
        }

        private void CounterWindow_DragEnter(object sender, System.Windows.DragEventArgs e)

        {
            FileDropOverlay.Visibility = IsValidDrop(e) ? Visibility.Visible : Visibility.Collapsed;
            AllowDrop = IsValidDrop(e);
        }

        static bool IsValidDrop(System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                if (System.IO.Path.GetExtension(files[0]) == CounterProfile.DefaultExtension)
                {
                    return true;
                }
            }
            return false;
        }

        private void CounterWindow_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                OpenFile(files[0]);
            }
            FileDropOverlay.Visibility = Visibility.Collapsed;
        }

        bool resizeDirtyFlag;

        private void CounterWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!MouseIsDown) return;
            resizeDirtyFlag = true;
            ResizingText.Visibility = Visibility.Visible;
            UpdateResizeText();
        }

        private void CounterWindow_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            UpdateCanResize();
            if (!groupEligibilityMode)
            {
                GroupTag.Visibility = groupIndex != -1 ? Visibility.Visible : Visibility.Collapsed;
                GroupTagLabel.Content = groupIndex.ToString();
            }
        }

        private void CounterWindow_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!groupEligibilityMode)
            {
                GroupTag.Visibility = Visibility.Collapsed;
            }
        }

        void UpdateCanResize()
        {
            const double ExtraCheckSize = 10;
            var screenRect = new Rect(Left - ExtraCheckSize, Top - ExtraCheckSize, Width + ExtraCheckSize * 2, Height + ExtraCheckSize * 2);
            bool insideBounds = screenRect.Contains(System.Windows.Forms.Cursor.Position.X, System.Windows.Forms.Cursor.Position.Y);
            bool handlingThisWindow = (MouseIsDown && IsActive);
            if (!currentProfile.sizeLocked && ((insideBounds && !MouseIsDown) || handlingThisWindow))
            {
                if (CounterWindow.ResizeMode != ResizeMode.CanResizeWithGrip)
                    CounterWindow.ResizeMode = ResizeMode.CanResizeWithGrip;
            }
            else
            {
                if (CounterWindow.ResizeMode != ResizeMode.NoResize)
                    CounterWindow.ResizeMode = ResizeMode.NoResize;
            }
        }

        #endregion

        #region Remote Control Interface

        RemoteControlManager rcm;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
            if (startupArguments.startHidden)
            {
                Hide();
            }
            source.AddHook(WndProc);
            rcm = new RemoteControlManager(this);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch ((Message)msg)
            {
                case Message.Ping:
                    {
                        if (communicable)
                        {
                            handled = true;
                            return new IntPtr(1);
                        }
                        break;
                    }
                case Message.Handshake:
                    {
                        rcm.AddWindow(wParam);
                        return rcm.thisWindow.windowHandle;
                    }
                case Message.Disconnect:
                    {
                        rcm.RemoveWindow(wParam);
                        break;
                    }
                case Message.ReserveQueryHandle:
                    {
                        handled = true;
                        var result = rcm.TryReserveQueryHandle();
                        if (!result)
                        {
                            return new IntPtr(-1);
                        }
                        break;
                    }
                case Message.DisposeQueryHandle:
                    {
                        rcm.TryDisposeQueryHandle();
                        break;
                    }
                case Message.Increment:
                    {
                        handled = true;
                        IncrementCounter(currentProfile.incrementAmount);
                        break;
                    }
                case Message.Decrement:
                    {
                        handled = true;
                        IncrementCounter(-currentProfile.incrementAmount);
                        break;
                    }
                case Message.SetValue:
                    {
                        handled = true;
                        SetCounterValue(wParam.ToInt32());
                        break;
                    }
                case Message.Save:
                    {
                        currentProfile.Save();
                        break;
                    }
                case Message.SetEdgeHighlight:
                    {
                        handled = true;
                        SetEdgeHighlights((EdgeHighlight)wParam.ToInt32());
                        break;
                    }
                case Message.WriteProfilePathToGroupFile:
                    {
                        handled = true;
                        List<string> lines = File.ReadAllLines(CommonGroupFile).ToList();
                        lines.Add(currentProfile.path);
                        File.WriteAllLines(CommonGroupFile, lines);
                        break;
                    }
                case Message.SetGroup:
                    {
                        handled = true;
                        groupShutdown = false;
                        SetGroup(wParam.ToInt32());
                        break;
                    }
                case Message.GetGroup:
                    {
                        handled = true;
                        return new IntPtr(groupIndex);
                    }
                case Message.EnterGroupElegibilityMode:
                    {
                        handled = true;
                        SetGroupEligibilityMode(true);
                        break;
                    }
                case Message.ExitGroupElegibilityMode:
                    {
                        handled = true;
                        SetGroupEligibilityMode(false);
                        break;
                    }
                case Message.SetEligibleForGroup:
                    {
                        handled = true;
                        SetEligibleForGroup(wParam.ToInt32() == 1);
                        break;
                    }
                case Message.GetEligibleForGroup:
                    {
                        handled = true;
                        return new IntPtr(eligibleForGroup ? 1 : 0);
                    }
                case Message.Show:
                    {
                        handled = true;
                        Topmost = metaSettings.data.topmost;
                        Show();
                        break;
                    }
                case Message.Hide:
                    {
                        handled = true;
                        Hide();
                        break;
                    }
                case Message.RefreshOtherWindows:
                    {
                        handled = true;
                        rcm.GatherWindows();
                        break;
                    }
                case Message.RefreshMetaSettings:
                    {
                        handled = true;
                        metaSettings.Load();
                        break;
                    }
                case Message.Close:
                    {
                        handled = true;
                        forceClosing = wParam.ToInt32() == 1;
                        Close();
                        break;
                    }
                case Message.GetGroupShutdown:
                    {
                        handled = true;
                        return new IntPtr(groupShutdown ? 1 : 0);
                    }
                case Message.SetGroupShutdown:
                    {
                        handled = true;
                        groupShutdown = wParam.ToInt32() == 1;
                        break;
                    }
                default:
                    break;
            }

            switch ((Post)msg)
            {
                case Post.LayoutData:
                    {
                        handled = true;
                        if (rcm.RecieveData(out LayoutData ld))
                        {
                            SetLayout(ld);
                        }
                        break;
                    }
                case Post.DeltaMovement:
                    {
                        int inGroup = wParam.ToInt32();

                        if (inGroup == -1 || (inGroup == groupIndex && groupMode))
                        {
                            rcm.RecieveData(out Vector delta);
                            Left += delta.X;
                            Top += delta.Y;
                            currentProfile.windowTop = Top;
                            currentProfile.windowLeft = Left;
                            SetDirty();
                        }
                        break;
                    }
                default:
                    break;
            }

            switch ((Query)msg)
            {
                case Query.LayoutData:
                    {
                        handled = true;
                        rcm.PostQueryData(GetLayout());
                        break;
                    }
                case Query.GetIncrementKey:
                    {
                        handled = true;
                        rcm.PostQueryData(currentProfile.incrementHotkey);
                        break;
                    }
                case Query.GetDecrementKey:
                    {
                        handled = true;
                        rcm.PostQueryData(currentProfile.decrementHotkey);
                        break;
                    }
                default:
                    break;
            }

            return IntPtr.Zero;
        }

        #endregion

        #region Layout Control

        struct WindowLayoutWrapper
        {
            public WindowWrapper window;
            public LayoutData layout;
        }

        public struct LayoutData
        {
            static double ColliderSize = 40, CornerSize = 20;
            public double WindowWidth, WindowHeight, WindowLeft, WindowTop;

            public Vector Center
                => new Vector(WindowTop + WindowHeight / 2, WindowLeft + WindowWidth / 2);
            public Rect AsRect => new Rect(WindowLeft, WindowTop, WindowWidth, WindowHeight);

            // Axies irrelevant to the edge will be 0
            public Vector GetLocation(EdgeHighlight edge)
            {
                switch (edge)
                {
                    case EdgeHighlight.Top:
                        return new Vector(0, WindowTop);
                    case EdgeHighlight.Left:
                        return new Vector(WindowLeft, 0);
                    case EdgeHighlight.Right:
                        return new Vector(WindowLeft + WindowWidth, 0);
                    case EdgeHighlight.Down:
                        return new Vector(0, WindowTop + WindowHeight);
                    case EdgeHighlight.TopLeft:
                        return new Vector(WindowLeft, WindowTop);
                    case EdgeHighlight.TopRight:
                        return new Vector(WindowLeft + WindowWidth, WindowTop);
                    case EdgeHighlight.DownLeft:
                        return new Vector(WindowLeft, WindowTop + WindowHeight);
                    case EdgeHighlight.DownRight:
                        return new Vector(WindowLeft + WindowWidth, WindowTop + WindowHeight);
                    default:
                        return new Vector();
                }
            }

            public Rect GetEdge(EdgeHighlight edge)
            {
                switch (edge)
                {

                    case EdgeHighlight.Top:
                        return new Rect(WindowLeft, WindowTop - (ColliderSize / 2), WindowWidth, ColliderSize);
                    case EdgeHighlight.Left:
                        return new Rect(WindowLeft - (ColliderSize / 2), WindowTop, ColliderSize, WindowHeight);
                    case EdgeHighlight.Right:
                        return new Rect((WindowLeft + WindowWidth) - (ColliderSize / 2), WindowTop, ColliderSize, WindowHeight);
                    case EdgeHighlight.Down:
                        return new Rect(WindowLeft, (WindowTop + WindowHeight) - (ColliderSize / 2), WindowWidth, ColliderSize);
                    case EdgeHighlight.TopLeft:
                        return new Rect(WindowLeft, WindowTop, CornerSize, CornerSize);
                    case EdgeHighlight.TopRight:
                        return new Rect(WindowLeft + WindowWidth, WindowTop, CornerSize, CornerSize);
                    case EdgeHighlight.DownLeft:
                        return new Rect(WindowLeft, WindowTop + WindowHeight, CornerSize, CornerSize);
                    case EdgeHighlight.DownRight:
                        return new Rect(WindowLeft + WindowWidth, WindowTop + WindowHeight, CornerSize, CornerSize);
                }

                return new Rect();
            }

        }

        [Flags]
        public enum EdgeHighlight
        {
            Top = 1,
            Left = 2,
            Right = 4,
            Down = 8,
            TopLeft = 16,
            TopRight = 32,
            DownLeft = 64,
            DownRight = 128
        }

        struct SnapInstruction
        {
            public WindowLayoutWrapper targetWindow;
            public EdgeHighlight targetEdge;
            public EdgeHighlight sourceEdge;
        }

        List<WindowLayoutWrapper> otherLayouts = new List<WindowLayoutWrapper>();
        SnapInstruction bestSnapInstruction;

        void UpdateOtherLayouts()
        {
            otherLayouts.Clear();

            if (groupMode) return;

            foreach (var window in rcm.otherWindows)
            {
                otherLayouts.Add(
                    new WindowLayoutWrapper()
                    {
                        window = window,
                        layout = rcm.Query<LayoutData>(window, Query.LayoutData)
                    });
            }
        }

        void FinishDragging()
        {
            foreach (var window in otherLayouts)
            {
                rcm.SendMessage(window.window, Message.SetEdgeHighlight, 0);
            }

            SetEdgeHighlights(0);

            if (otherLayouts.Count > 0)
            {
                otherLayouts.Clear();
            }
            else
            {
                return;
            }

            LayoutData thisLayout = GetLayout();
            LayoutData targetLayout = bestSnapInstruction.targetWindow.layout;
            Vector delta = targetLayout.GetLocation(bestSnapInstruction.targetEdge) - thisLayout.GetLocation(bestSnapInstruction.sourceEdge);
            Left += delta.X;
            Top += delta.Y;
        }

        void SetEdgeHighlights(EdgeHighlight highlight)
        {
            WindowTopEdge.Visibility = highlight.HasFlag(EdgeHighlight.Top) ? Visibility.Visible : Visibility.Collapsed;
            WindowLeftEdge.Visibility = highlight.HasFlag(EdgeHighlight.Left) ? Visibility.Visible : Visibility.Collapsed;
            WindowRightEdge.Visibility = highlight.HasFlag(EdgeHighlight.Right) ? Visibility.Visible : Visibility.Collapsed;
            WindowDownEdge.Visibility = highlight.HasFlag(EdgeHighlight.Down) ? Visibility.Visible : Visibility.Collapsed;
            WindowTopLeftCorner.Visibility = highlight.HasFlag(EdgeHighlight.TopLeft) ? Visibility.Visible : Visibility.Collapsed;
            WindowTopRightCorner.Visibility = highlight.HasFlag(EdgeHighlight.TopRight) ? Visibility.Visible : Visibility.Collapsed;
            WindowDownLeftCorner.Visibility = highlight.HasFlag(EdgeHighlight.DownLeft) ? Visibility.Visible : Visibility.Collapsed;
            WindowDownRightCorner.Visibility = highlight.HasFlag(EdgeHighlight.DownRight) ? Visibility.Visible : Visibility.Collapsed;
        }

        void CompareEdges(LayoutData a, LayoutData b, EdgeHighlight edgeA, EdgeHighlight edgeB, ref EdgeHighlight targetEdge, ref EdgeHighlight sourceEdge, ref EdgeHighlight highlight, ref EdgeHighlight thisHighlight, ref double heuristic, double bias = 0)
        {
            if (a.GetEdge(edgeA).IntersectsWith(b.GetEdge(edgeB)))
            {
                double distance = (a.GetLocation(edgeA) - b.GetLocation(edgeB)).Length - bias;
                if (distance < heuristic)
                {
                    heuristic = distance;
                    targetEdge = edgeB;
                    sourceEdge = edgeA;
                    highlight |= edgeB;
                    thisHighlight |= edgeA;
                }
            }
        }

        Vector prevLocation;
        private void CounterWindow_LocationChanged(object sender, EventArgs e)
        {
            currentProfile.windowTop = Top;
            currentProfile.windowLeft = Left;
            Vector curLocation = new Vector(Left, Top);
            Vector delta = curLocation - prevLocation;
            prevLocation = new Vector(Left, Top);

            if (groupMode && IsActive)
            {
                MoveGroup(delta);
            }

            if (MouseIsDown && IsActive)
            {
                currentProfile.SetIsDirty(true);
            }

            LayoutData thisLayout = GetLayout();
            EdgeHighlight thisHighlight = 0;
            bestSnapInstruction.targetEdge = 0;
            bestSnapInstruction.sourceEdge = 0;

            double heuristic = double.MaxValue;

            foreach (var window in otherLayouts)
            {
                const double cornerBias = 10;

                EdgeHighlight highlight = 0;
                EdgeHighlight target = 0, source = 0;

                CompareEdges(thisLayout, window.layout, EdgeHighlight.Top, EdgeHighlight.Down, ref target, ref source, ref highlight, ref thisHighlight, ref heuristic);
                CompareEdges(thisLayout, window.layout, EdgeHighlight.Down, EdgeHighlight.Top, ref target, ref source, ref highlight, ref thisHighlight, ref heuristic);
                CompareEdges(thisLayout, window.layout, EdgeHighlight.Left, EdgeHighlight.Right, ref target, ref source, ref highlight, ref thisHighlight, ref heuristic);
                CompareEdges(thisLayout, window.layout, EdgeHighlight.Right, EdgeHighlight.Left, ref target, ref source, ref highlight, ref thisHighlight, ref heuristic);
                CompareEdges(thisLayout, window.layout, EdgeHighlight.TopLeft, EdgeHighlight.TopRight, ref target, ref source, ref highlight, ref thisHighlight, ref heuristic, cornerBias);
                CompareEdges(thisLayout, window.layout, EdgeHighlight.TopRight, EdgeHighlight.TopLeft, ref target, ref source, ref highlight, ref thisHighlight, ref heuristic, cornerBias);
                CompareEdges(thisLayout, window.layout, EdgeHighlight.DownLeft, EdgeHighlight.DownRight, ref target, ref source, ref highlight, ref thisHighlight, ref heuristic, cornerBias);
                CompareEdges(thisLayout, window.layout, EdgeHighlight.DownRight, EdgeHighlight.DownLeft, ref target, ref source, ref highlight, ref thisHighlight, ref heuristic, cornerBias);
                CompareEdges(thisLayout, window.layout, EdgeHighlight.TopRight, EdgeHighlight.DownRight, ref target, ref source, ref highlight, ref thisHighlight, ref heuristic, cornerBias);
                CompareEdges(thisLayout, window.layout, EdgeHighlight.TopLeft, EdgeHighlight.DownLeft, ref target, ref source, ref highlight, ref thisHighlight, ref heuristic, cornerBias);
                CompareEdges(thisLayout, window.layout, EdgeHighlight.DownRight, EdgeHighlight.TopRight, ref target, ref source, ref highlight, ref thisHighlight, ref heuristic, cornerBias);
                CompareEdges(thisLayout, window.layout, EdgeHighlight.DownLeft, EdgeHighlight.TopLeft, ref target, ref source, ref highlight, ref thisHighlight, ref heuristic, cornerBias);

                rcm.SendMessage(window.window, Message.SetEdgeHighlight, (int)highlight);
                if (highlight != 0)
                {
                    bestSnapInstruction.targetWindow = window;
                    bestSnapInstruction.targetEdge = target;
                    bestSnapInstruction.sourceEdge = source;
                }
            }
            SetEdgeHighlights(thisHighlight);
        }

        LayoutData GetLayout() => new LayoutData()
        {
            WindowWidth = Width,
            WindowHeight = Height,
            WindowLeft = Left,
            WindowTop = Top
        };

        private void SetLayout(LayoutData layout)
        {
            Width = layout.WindowWidth;
            Height = layout.WindowHeight;
            Left = layout.WindowLeft;
            Top = layout.WindowTop;
        }

        #region Group

        bool groupMode = false, groupShutdown = false;
        bool groupEligibilityMode = false, eligibleForGroup = false;
        int groupIndex = -1;

        void SetGroup(int groupIndex)
        {
            this.groupIndex = groupIndex;
            groupMode = groupIndex != -1;
            GroupTagLabel.Content = groupIndex.ToString();
        }

        public void SetGroupEligibilityMode(bool eligible)
        {
            groupEligibilityMode = eligible;
            GroupTag.Visibility = (eligible && groupIndex != -1) ? Visibility.Visible : Visibility.Collapsed;
            if (!eligible)
            {
                SetEligibleForGroup(false);
            }
        }

        public void SetEligibleForGroup(bool eligible)
        {
            GroupOverlay.Visibility = eligible ? Visibility.Visible : Visibility.Collapsed;
            eligibleForGroup = eligible;
        }

        void MoveGroup(Vector delta)
        {
            using (var post = rcm.PostData(delta))
            {
                foreach (var window in rcm.otherWindows)
                {
                    rcm.PostDirect(window, Post.DeltaMovement, groupIndex);
                }
            }
        }

        #endregion

        #endregion

        #region Save/File Management


        private void InitializeFromProfile(CounterProfile profile, bool alwaysLoadImage = false)
        {
            var previousProfile = currentProfile;
            currentProfile = profile;
            Height = profile.windowHeight;
            Width = profile.windowWidth;
            BackgroundImage.Stretch = profile.stretch;
            BackgroundImage.VerticalAlignment = profile.verticalAlignment;
            BackgroundImage.HorizontalAlignment = profile.horizontalAlignment;
            MainCounterText.Foreground = new SolidColorBrush(profile.textColor);
            OddsText.Foreground = new SolidColorBrush(profile.statTextColor);
            OddsTextMotivation.Foreground = new SolidColorBrush(profile.statTextColor);
            OddsText.Visibility = profile.showOdds ? Visibility.Visible : Visibility.Collapsed;
            OddsTextMotivation.Visibility = profile.showOdds ? Visibility.Visible : Visibility.Collapsed;
            OddsTextMotivationBox.Visibility = profile.showOdds ? Visibility.Visible : Visibility.Collapsed;
            Background = new SolidColorBrush(profile.backgroundColor);

            if (!currentProfile.windowTop.Equals(double.NaN) && !currentProfile.windowLeft.Equals(double.NaN))
            {
                Top = currentProfile.windowTop;
                Left = currentProfile.windowLeft;
            }

            SetSizeLocked(profile.sizeLocked, true);
            SetShowOdds(profile.showOdds, true);
            SetFiltering(profile.bitmapScalingMode);

            if (profile.cachedImageURL != null)
                if (!File.Exists(profile.backgroundImagePath) && profile.cachedImageURL.Length > 0)
                {
                    profile.backgroundImagePath = DownloadManager.DownloadPokemonImage(profile.cachedImageURL);
                    if (profile.path != null) profile.Save();
                }

            if (File.Exists(profile.backgroundImagePath))
            {
                bool wantToLoad = false;
                if (previousProfile == null)
                {
                    wantToLoad = true;
                }
                else
                {
                    wantToLoad = profile.backgroundImagePath != previousProfile.backgroundImagePath;
                }

                if (wantToLoad || alwaysLoadImage)
                    LoadImageFromFile(profile.backgroundImagePath);
            }
            else
            {
                System.Windows.MessageBox.Show($"Failed to load background image {profile.backgroundImagePath}", "Failed to load image", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            BackgroundImage.Width = profile.imageWidth;
            BackgroundImage.Height = profile.imageHeight;

            RegisterHotkeys();

            if (profile.path != null)
            {
                metaSettings.data.recentProfiles.Add(profile.path);
                metaSettings.Save();
            }

            RefreshAll();
        }

        void SetDirty()
        {
            currentProfile.SetIsDirty(true);
        }

        void PushChange()
        {
            undoList.PushChange(currentProfile);
            SetDirty();
        }

        void OpenFile(string path)
        {
            if (!File.Exists(path))
            {
                System.Windows.MessageBox.Show($"Couldn't find file {path}!", "Open file failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (currentProfile != null)
                if (currentProfile.GetIsDirty())
                {
                    var result = System.Windows.MessageBox.Show("Do you want to save your current profile before loading the new one?", currentProfile.Name, MessageBoxButton.YesNo);

                    if (result == MessageBoxResult.Yes)
                        Commands.CustomCommands.Save.Execute(null, this);
                }

            var profile = CounterProfile.LoadFrom(path);
            if (profile != null)
            {
                InitializeFromProfile(profile);
                undoList.Clear();
                metaSettings.data.recentProfiles.Add(path);
                metaSettings.data.lastProfilePath = path;
                metaSettings.Save();
            }
        }

        #endregion

        #region Commands

        // Save as
        private void SaveAsCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void SaveAsCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            currentProfile.SaveAs();
        }

        // Save
        private void SaveCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            bool isAutosaveAction = (e.Parameter is bool) ? ((bool)e.Parameter) : false;

            if (currentProfile.path == null)
            {
                if (!(metaSettings.data.autosave && isAutosaveAction))
                {
                    if (currentProfile.SaveAs())
                    {
                        Dispatcher.Invoke(ShowSavedText);
                    }

                }
            }
            else
            {
                currentProfile.Save();
                Dispatcher.Invoke(ShowSavedText);
            }
        }

        // Load
        private void LoadProfileCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void LoadProfileCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.DefaultExt = CounterProfile.DefaultExtension;
            openFileDialog.Filter = $"Counter file (*{CounterProfile.DefaultExtension})|*{CounterProfile.DefaultExtension}";
            openFileDialog.Title = "Open Counter Profile";
            openFileDialog.InitialDirectory = System.IO.Path.GetDirectoryName(currentProfile.path);

            var result = openFileDialog.ShowDialog();
            if (result != System.Windows.Forms.DialogResult.OK) return;
            if (!File.Exists(openFileDialog.FileName)) return;

            OpenFile(openFileDialog.FileName);
        }

        // New
        private void NewProfileCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void NewProfileCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Process.Start(Paths.Executable, "-default -n -skipReload");
        }

        // Undo
        private void UndoCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = undoList.CanUndo;
        }

        private void UndoCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            CounterProfile undoProfile = new CounterProfile();
            if (undoList.Undo(ref undoProfile))
            {
                InitializeFromProfile(undoProfile);
            }
        }

        // Redo
        private void RedoCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = undoList.CanRedo;
        }

        private void RedoCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            CounterProfile redoProfile = new CounterProfile();
            if (undoList.Redo(ref redoProfile))
            {
                InitializeFromProfile(redoProfile);
            }
        }

        // Close
        private void CloseCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CanClose();
        }

        private void CloseCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Close();
        }

        // Select pokemon
        private void SelectPokemonCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = DownloadManager.IsOnline();
        }

        private void SelectPokemonCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SelectPokemonPopup popup = new SelectPokemonPopup("pokemon", metaSettings.data.pokemonDatas.pokemon, currentProfile.bitmapScalingMode);

            popup.cachedGame = currentProfile.cachedPokemonGame;
            popup.cachedOptions = currentProfile.cachedPokemonOptions;
            popup.cachedPokemon = currentProfile.cachedPokemonIndex;

            if (popup.ShowDialog().GetValueOrDefault(false))
            {
                currentProfile.cachedPokemonGame = popup.cachedGame;
                currentProfile.cachedPokemonOptions = popup.cachedOptions;
                currentProfile.cachedPokemonIndex = popup.cachedPokemon;
                currentProfile.cachedImageURL = popup.imageURL;

                LoadImageFromFile(popup.imagePath);
                RefreshAll();
                PushChange();
            }
        }

        // Duplicate
        private void DuplicateCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void DuplicateCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Process.Start(Paths.Executable, "\"" + currentProfile.path + "\" -n -skipReload");
        }

        // Group
        private void GroupCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void GroupCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (groupMode)
            {
                SetGroup(-1);
                groupShutdown = false;
            }
            else
            {
                MakeGroup();
            }
        }

        private void SaveGroupCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void SaveGroupCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (createGroupPopup?.IsLoaded == true) return;

            createGroupPopup = new CreateGroupPopup("Create group file", groupIndex, this, rcm);
            createGroupPopup.Show();

            createGroupPopup.onFinished += (success) =>
            {
                if (success)
                {
                    SaveFileDialog saveFileDialog = new SaveFileDialog();
                    saveFileDialog.DefaultExt = CounterProfile.GroupFileExtension;
                    saveFileDialog.Filter = $"Counter group file (*{CounterProfile.GroupFileExtension})|*{CounterProfile.GroupFileExtension}";
                    saveFileDialog.AddExtension = true;
                    saveFileDialog.Title = "Save Counter Group";
                    if (currentProfile.path != null)
                        saveFileDialog.InitialDirectory = System.IO.Path.GetDirectoryName(currentProfile.path);
                    else
                        saveFileDialog.InitialDirectory = Paths.ExecutableDirectory;

                    var result = saveFileDialog.ShowDialog();

                    if (result != System.Windows.Forms.DialogResult.OK) return;

                    List<LayoutData> profileLayouts = new List<LayoutData>();
                    File.WriteAllText(CommonGroupFile, "");
                    int nextAvailableGroup = GetNextAvailableGroupIndex();
                    foreach (var window in rcm.AllWindows)
                    {
                        if (rcm.SendMessage(window, Message.GetEligibleForGroup).ToInt32() == 1)
                        {
                            rcm.SendMessage(window, Message.WriteProfilePathToGroupFile);
                            rcm.SendMessage(window, Message.SetGroup, nextAvailableGroup);
                            profileLayouts.Add(rcm.Query<LayoutData>(window, Query.LayoutData));
                        }
                    }

                    string[] profilePaths = File.ReadAllLines(CommonGroupFile);

                    CounterGroup counterGroup = new CounterGroup();

                    MessageBoxResult saveResult = MessageBoxResult.Yes;

                    if (profileLayouts.Count != profilePaths.Length)
                    {
                        saveResult = System.Windows.MessageBox.Show("Failed to save counter file! There was a problem gathering the profile information. Do you want to try to make a profile group anyway?", "Oops!", MessageBoxButton.YesNo, MessageBoxImage.Warning); ;
                    }

                    if (saveResult != MessageBoxResult.Yes) return;

                    for (int i = 0; i < profilePaths.Length && i < profileLayouts.Count; i++)
                    {
                        counterGroup.counters.Add(new CounterGroup.CounterInfo()
                        {
                            profilePath = profilePaths[i],
                            layout = profileLayouts[i]
                        });
                    }

                    File.WriteAllText(saveFileDialog.FileName, JsonConvert.SerializeObject(counterGroup));
                }
            };
        }

        // Show Odds
        private void ShowOddsCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void ShowOddsCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SetShowOdds(!currentProfile.showOdds);
            PushChange();
        }

        void SetShowOdds(bool showOdds, bool updateCheckmark = false)
        {
            currentProfile.showOdds = showOdds;
            OddsText.Visibility = showOdds ? Visibility.Visible : Visibility.Collapsed;
            OddsTextMotivation.Visibility = showOdds ? Visibility.Visible : Visibility.Collapsed;
            OddsTextMotivationBox.Visibility = showOdds ? Visibility.Visible : Visibility.Collapsed;

            SetTargetOddsOption.IsEnabled = showOdds;

            if (updateCheckmark)
            {
                ShowOdds.IsChecked = showOdds;
            }
            RefreshAll();
        }


        // Lock Size
        private void LockSizeCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void LockSizeCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SetSizeLocked(!currentProfile.sizeLocked);
            PushChange();
        }

        void SetSizeLocked(bool aLockedSize, bool updateCheckmark = false)
        {
            currentProfile.sizeLocked = aLockedSize;

            if (updateCheckmark)
            {
                LockSizeOption.IsChecked = aLockedSize;
            }
        }

        // Always On Top
        private void AlwaysOnTopCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void AlwaysOnTopCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SetAlwaysOnTopOption(!metaSettings.data.topmost);
            PushChange();
        }

        void SetAlwaysOnTopOption(bool alwaysOnTop, bool updateCheckmark = false)
        {
            metaSettings.data.topmost = alwaysOnTop;
            CounterWindow.Topmost = alwaysOnTop;

            if (updateCheckmark)
            {
                AlwaysOnTopOption.IsChecked = alwaysOnTop;
            }
        }

        // Set size
        private void ResizeCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void ResizeCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            DualValuePopup setSizePopup = new DualValuePopup("Size", "x", "y"
                , (int i) => { return i >= MinimumWidth; }
                , (int i) => { return i >= MinimumHeight; });
            setSizePopup.value1 = (int)Width;
            setSizePopup.value2 = (int)Height;

            if (setSizePopup.ShowDialog().GetValueOrDefault(false))
            {
                Width = setSizePopup.value1;
                Height = setSizePopup.value2;

                currentProfile.windowWidth = setSizePopup.value1;
                currentProfile.windowHeight = setSizePopup.value2;
                PushChange();
            }
        }


        #endregion

        #region Hotkeys

        public void RegisterHotkeys()
        {
            TryRegisterHotkey(ref incrementHook, currentProfile.incrementHotkey, IncrementHook_KeyPressed);
            TryRegisterHotkey(ref decrementHook, currentProfile.decrementHotkey, DecrementHook_KeyPressed);
        }

        public void DeregisterHotkeys()
        {
            incrementHook.KeyPressed -= IncrementHook_KeyPressed;
            incrementHook.Dispose();
            decrementHook.KeyPressed -= DecrementHook_KeyPressed;
            decrementHook.Dispose();
        }

        static bool TryRegisterHotkey(ref KeyboardHook refHook, KeyCombination hotkey, EventHandler<KeyPressedEventArgs> function)
        {
            if (refHook != null)
            {
                refHook.KeyPressed -= function;
                refHook.Dispose();
            }
            refHook = new KeyboardHook();
            var result = refHook.RegisterHotKey(hotkey.modifierKeys, hotkey.keys);
            if (!result) return false;
            refHook.KeyPressed += function;
            return true;
        }

        private void IncrementHook_KeyPressed(object sender, KeyPressedEventArgs e)
        {
            IncrementCounter(currentProfile.incrementAmount);
        }

        private void DecrementHook_KeyPressed(object sender, KeyPressedEventArgs e)
        {
            IncrementCounter(-currentProfile.incrementAmount);
        }

        List<KeyCombination> OccupiedKeys
        {
            get
            {
                List<KeyCombination> keys = new List<KeyCombination>();

                keys.Add(new KeyCombination(metaSettings.data.incrementKey, 0));
                keys.Add(new KeyCombination(metaSettings.data.decrementKey, 0));
                // Hard coded key combinations from elsewhere
                keys.Add(new KeyCombination(Key.Enter, 0));
                keys.Add(new KeyCombination(Key.Escape, 0));

                foreach (var window in rcm.AllWindows)
                {
                    keys.Add(rcm.Query<KeyCombination>(window, Query.GetIncrementKey));
                    keys.Add(rcm.Query<KeyCombination>(window, Query.GetDecrementKey));
                }

                var commands = Commands.CustomCommands.GetAllCommands();

                foreach (var command in commands)
                {
                    foreach (var gesture in command.InputGestures)
                    {
                        if (gesture is KeyGesture keyGesture)
                        {
                            keys.Add(new KeyCombination(keyGesture.Key, keyGesture.Modifiers));
                        }
                    }
                }

                return keys;
            }
        }

        #endregion

        #region Update Functions

        private void RefreshAll()
        {
            RefreshCounterText();
        }

        private void RefreshCounterText()
        {
            MainCounterText.Content = currentProfile.count.ToString();
            if (currentProfile.showOdds)
            {
                float percent = 1f - MathF.Pow(1f - ((float)currentProfile.targetOddsShinyRolls / currentProfile.targetOdds), currentProfile.count);
                if (currentProfile.count == 0) percent = 0;
                float oddsFloat = (float)currentProfile.count / ((float)currentProfile.targetOdds / currentProfile.targetOddsShinyRolls);
                string content = $"{(int)(percent * 10000) / 100f}%";

                if (currentProfile.targetOdds % currentProfile.targetOddsShinyRolls == 0)
                {
                    content += $" ({currentProfile.targetOddsShinyRolls * currentProfile.count}/{currentProfile.targetOdds})";
                }


                OddsTextMotivation.Content = $"\n{oddsMap.GetString(oddsFloat)}";
                OddsText.Content = content;
            }
        }

        void UpdateResizeText()
        {
            ResizingText.Text = $"{(int)Width}x{(int)Height}";
        }

        #endregion

        #region Helper Functions

        bool escapeHeld;
        public async void EscapeTracker()
        {
            const int delay = 10;
            float escapeTimer = 0;
            const float escapeTime = .5f, fadeinTime = .3f;
            while (true)
            {
                await Task.Delay(delay);
                float dt = (float)delay / 1000f;
                if (escapeHeld)
                {
                    HoldEscapeText.Visibility = Visibility.Visible;
                    escapeTimer += dt;
                    double time = Math.Clamp(escapeTimer / fadeinTime, 0, 1);
                    try
                    {
                        HoldEscapeText.Opacity = time;
                    }
                    catch (Exception)
                    {
                    }

                    if (escapeTimer > escapeTime)
                    {
                        Close();
                    }
                }
                else
                {
                    HoldEscapeText.Visibility = Visibility.Collapsed;
                    escapeTimer = 0;
                }
            }
        }

        int saveInstance = 0;
        async void ShowSavedText()
        {
            int thisInstance = ++saveInstance;

            SavedText.Content = "saved " + System.IO.Path.GetFileName(currentProfile.path);

            SavedText.Visibility = Visibility.Visible;

            float duration = 1, fadeoutTime = .5f;
            const int delay = 10;
            while (duration > 0)
            {
                await Task.Delay(delay);

                float dt = delay / 1000f;
                duration -= dt;
                double time = 1f - Math.Clamp((1f - duration) / fadeoutTime, 0, 1);

                if (thisInstance == saveInstance)
                    SavedText.Opacity = time;

            }

            if (thisInstance == saveInstance)
                SavedText.Visibility = Visibility.Hidden;
        }

        private void ResetImageScale()
        {
            BackgroundImage.Width = double.NaN;
            BackgroundImage.Height = double.NaN;
            currentProfile.imageHeight = double.NaN;
            currentProfile.imageWidth = double.NaN;
        }

        private void RefreshPokemonDatas()
        {
            if (!DownloadManager.IsOnline()) return;

            const string baseLink = "https://pokeapi.co/api/v2/pokemon/?limit=2000";
            var lastTopmost = Topmost;
            Topmost = false;

            var pokemonDatas = DownloadManager.DownloadObject<PokemonInfoList>(baseLink);
            pokemonDatas.pokemon.RemoveAll(p =>
            {
                int end = p.url.LastIndexOf('/');
                int start = p.url.LastIndexOf('/', end - 1);
                string substr = p.url.Substring(start + 1, end - start - 1);
                if (int.TryParse(substr, out int index))
                {
                    return index > 10000;
                }
                else
                {
                    return false;
                }
            });

            metaSettings.data.pokemonDatas = pokemonDatas;
            metaSettings.Save();

            Topmost = lastTopmost;
        }

        private void RefreshContextMenuTickboxes()
        {
            bool exactSize = !BackgroundImage.Width.Equals(double.NaN);
            ImageAlignmentCenterOption.IsChecked = currentProfile.stretch == Stretch.None && !exactSize;
            ImageAlignmentStretchOption.IsChecked = currentProfile.stretch == Stretch.Fill && !exactSize;
            ImageAlignmentFitOption.IsChecked = currentProfile.stretch == Stretch.Uniform && !exactSize;
            ImageAlignmentSizeToFillOption.IsChecked = currentProfile.stretch == Stretch.UniformToFill && !exactSize;
            ImageAlignmentExactOption.IsChecked = exactSize;

            ImageVerticalAlignmentTop.IsChecked = currentProfile.verticalAlignment == VerticalAlignment.Top;
            ImageVerticalAlignmentCenter.IsChecked = currentProfile.verticalAlignment == VerticalAlignment.Center;
            ImageVerticalAlignmentBottom.IsChecked = currentProfile.verticalAlignment == VerticalAlignment.Bottom;

            ImageHorizontalAlignmentLeft.IsChecked = currentProfile.horizontalAlignment == System.Windows.HorizontalAlignment.Left;
            ImageHorizontalAlignmentCenter.IsChecked = currentProfile.horizontalAlignment == System.Windows.HorizontalAlignment.Center;
            ImageHorizontalAlignmentRight.IsChecked = currentProfile.horizontalAlignment == System.Windows.HorizontalAlignment.Right;

            ImageFilteringNearestNeighbor.IsChecked = currentProfile.bitmapScalingMode == BitmapScalingMode.NearestNeighbor;
            ImageFilteringLinear.IsChecked = currentProfile.bitmapScalingMode == BitmapScalingMode.Linear;
            ImageFilteringHighQuality.IsChecked = currentProfile.bitmapScalingMode == BitmapScalingMode.HighQuality;

            TextColorOption.IsEnabled = textColorPickerPopup?.IsLoaded != true;
            StatTextColorOption.IsEnabled = statTextColorPickerPopup?.IsLoaded != true;
            BackgroundColorOption.IsEnabled = backgroundColorPickerPopup?.IsLoaded != true;
        }

        public static int GetNextAvailableGroupIndex(RemoteControlManager optionalRCM = null)
        {
            if (optionalRCM == null) optionalRCM = new RemoteControlManager();
            HashSet<int> occupiedGroups = new HashSet<int>();
            foreach (var window in optionalRCM.otherWindows)
            {
                if (optionalRCM.SendMessage(window, Message.Ping).ToInt32() == 1)
                {
                    int group = optionalRCM.SendMessage(window, Message.GetGroup).ToInt32();
                    occupiedGroups.Add(group);
                }
            }

            int nextAvailableGroup = 0;

            while (occupiedGroups.Contains(nextAvailableGroup))
            {
                nextAvailableGroup++;
            }

            return nextAvailableGroup;
        }

        Dictionary<string, List<KeyCombination>> originalKeybindings = new Dictionary<string, List<KeyCombination>>();

        void KeybindingsUpdated()
        {
            foreach (var item in ContextMenu.Items)
            {
                if (item is MenuItem mItem)
                {
                    if (mItem.Command != null)
                    { // Refresh commands like wtf why do we have to do this
                        var command = mItem.Command;
                        mItem.Command = null;
                        mItem.Command = command;
                    }
                }

            }

            CounterContextMenu.CommandBindings.Clear();
            CounterContextMenu.CommandBindings.AddRange(CounterWindow.CommandBindings);
        }

        #endregion

        #region Counter + Counter text

        private void ResetCounter()
        {
            // Done twice because we don't record regular increments
            PushChange();
            currentProfile.count = 0;
            RefreshCounterText();
            PushChange();
        }

        private void IncrementCounter(int delta)
        {
            currentProfile.count = Math.Max(0, currentProfile.count + delta);
            SetDirty();
            RefreshCounterText();
        }

        #endregion

        #region Context Menu

        #region Groups

        private void UngroupOption_Click(object sender, RoutedEventArgs e)
        {
            groupIndex = -1;
            groupMode = false;
            groupShutdown = false;
        }

        CreateGroupPopup createGroupPopup;

        private void MakeGroupOption_Clicked(object sender, RoutedEventArgs e)
        {
            MakeGroup();
        }

        void MakeGroup()
        {
            if (createGroupPopup?.IsLoaded == true) return;

            createGroupPopup = new CreateGroupPopup("Create a group", groupIndex, this, rcm);
            createGroupPopup.Show();

            createGroupPopup.onFinished += (success) =>
            {
                if (success)
                {
                    int nextAvailableGroup = GetNextAvailableGroupIndex();
                    foreach (var window in rcm.AllWindows)
                    {
                        if (rcm.SendMessage(window, Message.GetEligibleForGroup).ToInt32() == 1)
                        {
                            rcm.SendMessage(window, Message.SetGroup, nextAvailableGroup);
                        }
                    }
                }
            };
        }

        private void GroupAllOpenWindows_Clicked(object sender, RoutedEventArgs e)
        {
            rcm.BroadcastMessage(Message.SetGroup, GetNextAvailableGroupIndex(), all: true);
        }

        #endregion

        #region Window Size

        private void ResetSizeOption_Click(object sender, RoutedEventArgs e)
        {
            Width = DefaultWidth;
            Height = DefaultHeight;
            currentProfile.windowWidth = Width;
            currentProfile.windowHeight = Height;
            PushChange();
        }

        private void LockSizeOption_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            SetSizeLocked(true);
        }

        private void LockSizeOption_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            SetSizeLocked(false);
        }

        private void LockSizeOption_Click(object sender, RoutedEventArgs e)
        {
            // This runs after checked and unchecked, which is why it can work

            PushChange();
        }

        #endregion

        #region Image

        // Setting
        private void SetImageOption_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = @"All Image Files|*.BMP;*.bmp;*.JPG;*.JPEG*.jpg;*.jpeg;*.PNG;*.png;*.GIF;*.gif;*.tif;*.tiff;*.ico;*.ICO"
                                  + "|PNG|*.PNG;*.png"
                                  + "|JPEG|*.JPG;*.JPEG*.jpg;*.jpeg"
                                  + "|Bitmap|*.BMP;*.bmp"
                                  + "|GIF|*.GIF;*.gif"
                                  + "|TIF|*.tif;*.tiff"
                                  + "|ICO|*.ico;*.ICO";
            openFileDialog.InitialDirectory = System.IO.Path.GetDirectoryName(currentProfile.backgroundImagePath);
            openFileDialog.AddExtension = true;

            var result = openFileDialog.ShowDialog();
            if (result != System.Windows.Forms.DialogResult.OK) return;
            if (!File.Exists(openFileDialog.FileName)) return;

            currentProfile.cachedImageURL = "";

            LoadImageFromFile(openFileDialog.FileName);
            PushChange();
        }

        private void LoadImageFromFile(string path)
        {
            if (!File.Exists(path)) return;

            FileInfo finfo = new FileInfo(path);

            BitmapImage bmp = new BitmapImage(new Uri(path));

            if (bmp == null) return;

            if (finfo.Extension == ".gif")
            {
                ImageBehavior.SetAnimatedSource(BackgroundImage, bmp);
                BackgroundImage.Source = null;
            }
            else
            {
                ImageBehavior.SetAnimatedSource(BackgroundImage, null);
                BackgroundImage.Source = bmp;
            }

            currentProfile.backgroundImagePath = path;
        }

        // Size
        private void SetImageAlignmentCenterOption_Click(object sender, RoutedEventArgs e)
        {
            ResetImageScale();
            SetImageSizeOption(Stretch.None);
        }

        private void SetImageAlignmentStretchOption_Click(object sender, RoutedEventArgs e)
        {
            ResetImageScale();
            SetImageSizeOption(Stretch.Fill);
        }

        private void SetImageAlignmentFitOption_Click(object sender, RoutedEventArgs e)
        {
            ResetImageScale();
            SetImageSizeOption(Stretch.Uniform);
        }

        private void SetImageAlignmentSizeToFillOption_Click(object sender, RoutedEventArgs e)
        {
            ResetImageScale();
            SetImageSizeOption(Stretch.UniformToFill);
        }

        void SetImageSizeOption(Stretch aStretch)
        {
            currentProfile.stretch = aStretch;
            BackgroundImage.Stretch = aStretch;
            PushChange();
            RefreshContextMenuTickboxes();
        }

        // Vertical Alignment
        private void SetImageVerticalAlignmentTop_Click(object sender, RoutedEventArgs e)
        {
            SetImageVerticalAlignmentOption(VerticalAlignment.Top);
        }

        private void SetImageVerticalAlignmentCenter_Click(object sender, RoutedEventArgs e)
        {
            SetImageVerticalAlignmentOption(VerticalAlignment.Center);
        }

        private void SetImageVerticalAlignmentBottom_Click(object sender, RoutedEventArgs e)
        {
            SetImageVerticalAlignmentOption(VerticalAlignment.Bottom);
        }

        private void SetImageAlignmentExactOption_Click(object sender, RoutedEventArgs e)
        {
            DualValuePopup setSizePopup = new DualValuePopup("image size", "x", "y"
                    , (int i) => { return i >= 0; }
                    , (int i) => { return i >= 0; });
            setSizePopup.value1 = BackgroundImage.Width.Equals(double.NaN) ? 100 : (int)BackgroundImage.Width;
            setSizePopup.value2 = BackgroundImage.Height.Equals(double.NaN) ? 100 : (int)BackgroundImage.Height;

            if (setSizePopup.ShowDialog().GetValueOrDefault(false))
            {
                BackgroundImage.Width = setSizePopup.value1;
                BackgroundImage.Height = setSizePopup.value2;
                currentProfile.imageWidth = setSizePopup.value1;
                currentProfile.imageHeight = setSizePopup.value2;
                // Is set dirty and undo by imagesizeoptions
                SetImageSizeOption(Stretch.Fill);
            }
        }

        void SetImageVerticalAlignmentOption(VerticalAlignment verticalAlignment)
        {
            BackgroundImage.VerticalAlignment = verticalAlignment;
            currentProfile.verticalAlignment = verticalAlignment;
            PushChange();
            RefreshContextMenuTickboxes();
        }

        // Horizontal Alignment
        private void SetImageHorizontalAlignmentLeft_Click(object sender, RoutedEventArgs e)
        {
            SetImageHorizontalAlignmentOption(System.Windows.HorizontalAlignment.Left);
        }

        private void SetImageHorizontalAlignmentCenter_Click(object sender, RoutedEventArgs e)
        {
            SetImageHorizontalAlignmentOption(System.Windows.HorizontalAlignment.Center);
        }

        private void SetImageHorizontalAlignmentRight_Click(object sender, RoutedEventArgs e)
        {
            SetImageHorizontalAlignmentOption(System.Windows.HorizontalAlignment.Right);
        }

        void SetImageHorizontalAlignmentOption(System.Windows.HorizontalAlignment horizontalAlignment)
        {
            BackgroundImage.HorizontalAlignment = horizontalAlignment;
            currentProfile.horizontalAlignment = horizontalAlignment;
            PushChange();
            RefreshContextMenuTickboxes();
        }

        // Filtering
        private void SetImageFilteringNearestNeighbor_Click(object sender, RoutedEventArgs e)
        {
            SetFiltering(BitmapScalingMode.NearestNeighbor);
            PushChange();
        }

        private void SetImageFilteringLinear_Click(object sender, RoutedEventArgs e)
        {
            SetFiltering(BitmapScalingMode.Linear);
            PushChange();
        }

        private void SetImageFilteringHighQuality_Click(object sender, RoutedEventArgs e)
        {
            SetFiltering(BitmapScalingMode.HighQuality);
            PushChange();
        }

        public void SetFiltering(BitmapScalingMode bitmapScaling)
        {
            currentProfile.bitmapScalingMode = bitmapScaling;
            RenderOptions.SetBitmapScalingMode(BackgroundImage, bitmapScaling);
            RefreshContextMenuTickboxes();
        }

        #endregion

        #region Value

        private void SetCounterValue(int value)
        {
            currentProfile.count = value;
            PushChange();
            RefreshAll();
        }

        private void SetValueOption_Click(object sender, RoutedEventArgs e)
        {
            SingleValuePopup popup = new SingleValuePopup("counter value", (int i) => { return i >= 0; });
            popup.value = currentProfile.count;

            if (popup.ShowDialog().GetValueOrDefault(false))
            {
                SetCounterValue(popup.value);
            }
        }

        private void ResetValueOption_Click(object sender, RoutedEventArgs e)
        {
            ResetCounter();
        }

        private void SetIncrementValueOption_Click(object sender, RoutedEventArgs e)
        {
            SingleValuePopup popup = new SingleValuePopup("increment", (int i) => { return i >= 0; });
            popup.value = currentProfile.incrementAmount;

            if (popup.ShowDialog().GetValueOrDefault(false))
            {
                currentProfile.incrementAmount = popup.value;
                SetDirty();
                RefreshAll();
            }
        }

        #endregion

        #region Color

        ColorPickerPopup textColorPickerPopup;
        private void SetTextColorOption_Click(object sender, RoutedEventArgs e)
        {
            if (textColorPickerPopup?.IsLoaded == true) return;

            textColorPickerPopup = new ColorPickerPopup("text color", currentProfile.textColor);

            textColorPickerPopup.onValueUpdated += c =>
            {
                MainCounterText.Foreground = new SolidColorBrush(c);
            };

            textColorPickerPopup.onFinished += (value, changed) =>
            {
                MainCounterText.Foreground = new SolidColorBrush(value);
                if (changed)
                {
                    currentProfile.textColor = value;
                    PushChange();
                }
            };

            textColorPickerPopup.Show();
        }

        ColorPickerPopup backgroundColorPickerPopup;
        private void SetBackgroundColorOption_Click(object sender, RoutedEventArgs e)
        {
            if (backgroundColorPickerPopup?.IsLoaded == true) return;

            backgroundColorPickerPopup = new ColorPickerPopup("background color", currentProfile.backgroundColor);

            backgroundColorPickerPopup.onValueUpdated += c =>
            {
                Background = new SolidColorBrush(c);
            };

            backgroundColorPickerPopup.onFinished += (value, changed) =>
            {
                Background = new SolidColorBrush(value);
                if (changed)
                {
                    currentProfile.backgroundColor = value;
                    PushChange();
                }
            };

            backgroundColorPickerPopup.Show();
        }

        ColorPickerPopup statTextColorPickerPopup;
        private void SetStatTextColorOption_Click(object sender, RoutedEventArgs e)
        {
            if (statTextColorPickerPopup?.IsLoaded == true) return;

            statTextColorPickerPopup = new ColorPickerPopup("text color", currentProfile.statTextColor);

            statTextColorPickerPopup.onValueUpdated += c =>
            {
                OddsText.Foreground = new SolidColorBrush(c);
                OddsTextMotivation.Foreground = new SolidColorBrush(c);
            };

            statTextColorPickerPopup.onFinished += (value, changed) =>
            {
                OddsText.Foreground = new SolidColorBrush(value);
                OddsTextMotivation.Foreground = new SolidColorBrush(value);
                if (changed)
                {
                    currentProfile.statTextColor = value;
                    PushChange();
                }
            };

            statTextColorPickerPopup.Show();
        }

        #endregion

        #region Odds

        private void SetTargetOddsDirectOption_Click(object sender, RoutedEventArgs e)
        {
            var mi = (e.Source as MenuItem);
            if (mi != null)
            {
                if (int.TryParse(mi.Header as string, out int odds))
                {
                    currentProfile.targetOdds = odds;
                    currentProfile.targetOddsShinyRolls = 1;
                    PushChange();
                    RefreshAll();
                }
            }
        }

        private void SetTargetOddsRatioOption_Click(object sender, RoutedEventArgs e)
        {
            DualValuePopup setRatio = new DualValuePopup("target odds", "", "/"
                    , (int i) => { return i > 0; }
                    , (int i) => { return i > 0; });
            setRatio.value1 = currentProfile.targetOddsShinyRolls;
            setRatio.value2 = currentProfile.targetOdds;

            if (setRatio.ShowDialog().GetValueOrDefault(false))
            {
                currentProfile.targetOddsShinyRolls = setRatio.value1;
                currentProfile.targetOdds = setRatio.value2;
                PushChange();
                RefreshAll();
            }
        }

        private void SetTargetOddsOption_Click(object sender, RoutedEventArgs e)
        {
            SingleValuePopup popup = new SingleValuePopup("target odds", (int i) => { return i > 0; });
            popup.value = currentProfile.targetOdds;

            if (popup.ShowDialog().GetValueOrDefault(false))
            {
                currentProfile.targetOdds = popup.value;
                currentProfile.targetOddsShinyRolls = 1;
                SetDirty();
                RefreshAll();
            }
        }

        #endregion

        #region Options
        private void AutosaveOption_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            SetAutosave(true);
        }

        private void AutosaveOption_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            SetAutosave(false);
        }

        void SetAutosave(bool autosave)
        {
            metaSettings.data.autosave = autosave;
            metaSettings.Save();
        }

        private void RebindControl_Click(object sender, RoutedEventArgs e)
        {
            DeregisterHotkeys();

            var defaultProfile = CounterProfile.CreateDefault();

            List<object> listEntries = new List<object>();

            var label = new System.Windows.Controls.Label();
            label.Content = "Global hotkeys (counter specific)";
            listEntries.Add(label);

            listEntries.Add(new KeybindingWrapper(
                "Increment",
                currentProfile.incrementHotkey, defaultProfile.incrementHotkey,
                (kc) => { currentProfile.incrementHotkey = kc; }
                ));
            listEntries.Add(new KeybindingWrapper(
                "Decrement",
                currentProfile.decrementHotkey, defaultProfile.decrementHotkey,
                (kc) => { currentProfile.decrementHotkey = kc; }
                ));

            listEntries.Add(new Separator());

            label = new System.Windows.Controls.Label();
            label.Content = "Commands";
            listEntries.Add(label);

            listEntries.Add(new KeybindingWrapper(
                "Increment key",
                new KeyCombination(metaSettings.data.incrementKey, 0), new KeyCombination(Key.Up, 0),
                (kc) => { metaSettings.data.incrementKey = kc.keys; },
                (KeyCombination kc, out string reason) =>
                {
                    if (kc.modifierKeys != 0)
                    {
                        reason = "Can't have modifiers!";
                        return false;
                    }

                    reason = "Valid";
                    return true;
                }
                ));
            listEntries.Add(new KeybindingWrapper(
                "Decrement key",
                new KeyCombination(metaSettings.data.decrementKey, 0), new KeyCombination(Key.Down, 0),
                (kc) => { metaSettings.data.decrementKey = kc.keys; },
                (KeyCombination kc, out string reason) =>
                {
                    if (kc.modifierKeys != 0)
                    {
                        reason = "Can't have modifiers!";
                        return false;
                    }

                    reason = "Valid";
                    return true;
                }
                ));

            foreach (var command in Commands.CustomCommands.GetAllCommands())
            {
                listEntries.Add(new KeybindingWrapper(
                    command.Text,
                    new KeyCombination(command.InputGestures[0]), originalKeybindings[command.Name].FirstOrDefault(),
                    (kc) => // Finished
                    {
                        command.InputGestures.Clear();
                        command.InputGestures.Add(kc.Gesture);

                        metaSettings.data.customKeybinds[command.Name] = kc;
                        if (originalKeybindings.TryGetValue(command.Name, out List<KeyCombination> originalCombinations))
                        {
                            if (originalCombinations.FirstOrDefault() == kc)
                            {
                                metaSettings.data.customKeybinds.Remove(command.Name);
                                command.InputGestures.Clear();

                                foreach (var ogkc in originalCombinations)
                                {
                                    command.InputGestures.Add(ogkc.Gesture);
                                }
                            }
                        }

                    },

                    (KeyCombination kc, out string reason) =>
                    {
                        reason = "Command needs modifiers";
                        return kc.modifierKeys != 0;
                    }
                    ));
            }

            PressAnyButtonPopup popup = new PressAnyButtonPopup($"Rebind commands", OccupiedKeys, listEntries);

            if (popup.ShowDialog().GetValueOrDefault(false))
            {
                KeybindingsUpdated();
                metaSettings.Save();
            }

            RegisterHotkeys();
        }

        #endregion

        #region Other

        // Recent profiles
        private void RecentProfile_Click(object sender, RoutedEventArgs e)
        {
            if (e.Source is MenuItem mi)
            {
                OpenFile(mi.Header as string);
            }
        }

        private void ClearRecentProfiles_Click(object sender, RoutedEventArgs e)
        {
            metaSettings.data.recentProfiles.Clear();
            metaSettings.Save();
        }

        // File association
        private void SetupFileAssociationOption_Click(object sender, RoutedEventArgs e)
        {
            if (!IsAdministrator)
            {
                System.Windows.MessageBox.Show("You won't be able to fully setup file associations unless you run as administrator", "File association", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            EnsureFileAssociation();
            System.Windows.MessageBox.Show(".counter and .counterGroup files will now open with this executable", "Success!");
        }

        private void LeaveFeedbackOption_Click(object sender, RoutedEventArgs e)
        {
            var sInfo = new System.Diagnostics.ProcessStartInfo("https://github.com/totalaj/poke-counter/issues")
            {
                UseShellExecute = true,
            };
            System.Diagnostics.Process.Start(sInfo);
        }

        public static bool IsAdministrator =>
             new WindowsPrincipal(WindowsIdentity.GetCurrent())
                 .IsInRole(WindowsBuiltInRole.Administrator);

        static void EnsureFileAssociation()
        {
            FileAssociations.EnsureAssociationsSet(new FileAssociation
            {
                Extension = ".counter",
                ProgId = "PokeCounter.Counter",
                FileTypeDescription = "PokeCounter Counter",
                ExecutableFilePath = Paths.Executable,
                FileIcon = Path.Combine(Paths.ExecutableDirectory, "icons", "pc file.ico")
            },
            new FileAssociation
            {
                Extension = ".counterGroup",
                ProgId = "PokeCounter.Counter",
                FileTypeDescription = "PokeCounter Counter Group",
                ExecutableFilePath = Paths.Executable,
                FileIcon = Path.Combine(Paths.ExecutableDirectory, "icons", "pc file.ico")
            }
            );
        }

        // Refresh data
        private void GetNewPokemonData_Click(object sender, RoutedEventArgs e)
        {
            RefreshPokemonDatas();
        }
        // Clear cache
        private void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            DownloadManager.ClearCache();
        }

        #endregion

        #endregion
    }
}

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
        public GlobalHotkey incrementHotkey = new GlobalHotkey(Keys.Add, 0);
        public GlobalHotkey decrementHotkey = new GlobalHotkey(Keys.Subtract, 0);
        public bool topmost = true;

        public void Verify()
        {
            recentProfiles.RemoveWhere(s => !File.Exists(s));
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

        // Variables
        KeyboardHook incrementHook = new KeyboardHook();
        KeyboardHook decrementHook = new KeyboardHook();

        CounterProfile currentProfile = null;
        readonly SettingsFile<MetaSettings> metaSettings = new SettingsFile<MetaSettings>("metaSettings.json", SourceFolderType.Documents);
        readonly UndoList<CounterProfile> undoList = new UndoList<CounterProfile>();

        #endregion

        #region Main Window

        public MainWindow(string startupFile = null)
        {
            // @todo
            // Grouping windows together so they move at once together
            // Exporting and opening a group file
            // Set filter to smooth
            // Better keybinds, and keybind normal buttons too
            // More text customization
            // Cache window location
            // Autosave (different save, is applied on ctrl+s)
            // Could have save recovery in cache?
            // Better error handling aswell please
            // Add your own shaders???
            // Sound for increment/decrement
            // Set dirty on new file loaded from startup
            // Deal with closing multiple programs simultaneously
            // Ctrl+w to close
            // Ctrl+p to open pokemon menu
            // Ctrl+d to duplicate current profile

            InitializeComponent();

            RefreshPokemonDatas();
            EnsureFileAssociation();

            metaSettings.data.Verify();
            metaSettings.Save();

            CounterContextMenu.CommandBindings.AddRange(CounterWindow.CommandBindings);

            if (File.Exists(startupFile))
            {
                OpenFile(startupFile);
            }
            if (File.Exists(metaSettings.data.lastProfilePath) && currentProfile == null)
            {
                OpenFile(metaSettings.data.lastProfilePath);
            }

            if (currentProfile == null)
            {
                InitializeFromProfile(CounterProfile.CreateDefault(), true);
                metaSettings.data.lastProfilePath = currentProfile.path;
                metaSettings.Save();
            }

            undoList.PushChange(currentProfile);

            new DispatcherTimer(
                TimeSpan.FromMilliseconds(100), DispatcherPriority.Background,
                delegate
                {
                    bool mouseIsDown = GetAsyncKeyState(VK_LBUTTON) < 0;

                    if (resizeDirtyFlag && !mouseIsDown)
                    {
                        ResizingText.Visibility = Visibility.Collapsed;
                        resizeDirtyFlag = false;
                        currentProfile.windowWidth = Width;
                        currentProfile.windowHeight = Height;

                        SetDirty();
                        undoList.PushChange(currentProfile);
                    }
                },
                Dispatcher);

            Dispatcher.BeginInvoke(StartEscapeTracker);
            RefreshAll();
            SetAlwaysOnTopOption(metaSettings.data.topmost, true);
            SetEdgeHighlights(0);
        }

        private void CounterWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                UpdateOtherLayouts();
                DragMove();
                FinishDragging();
            }
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                Close();
            }
        }

        private void CounterWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Up:
                case Key.Enter:
                    IncrementCounter(currentProfile.incrementAmount);
                    break;
                case Key.Down:
                    IncrementCounter(-currentProfile.incrementAmount);
                    break;
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

        private void CloseOption_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CounterWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            metaSettings.data.lastProfilePath = currentProfile.path;
            metaSettings.data.recentProfiles.Add(currentProfile.path);
            metaSettings.Save();
            if (currentProfile.GetIsDirty())
            {
                var result = System.Windows.MessageBox.Show("You have unsaved changes!\nWould you like to save?", "Wait!", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    bool saveSucceeded = false;
                    if (currentProfile.path == null)
                    {
                        saveSucceeded = currentProfile.SaveAs();
                    }
                    else
                    {
                        saveSucceeded = currentProfile.Save();
                    }

                    if (!saveSucceeded)
                    {
                        e.Cancel = true;
                    }
                }
                if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                }
            }
            rcm.BroadcastMessage(Message.Disconnect, rcm.thisWindow.windowHandle.ToInt32());
        }

        private void CounterWindow_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
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

            foreach (var profile in metaSettings.data.recentProfiles.Reverse())
            {
                if (profile == currentProfile.path) continue;
                var mi = new MenuItem();
                mi.Header = profile;
                mi.Click += RecentProfile_Click;
                RecentProfilesOption.Items.Add(mi);
            }
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
            bool mouseIsDown = GetAsyncKeyState(VK_LBUTTON) < 0;
            if (!mouseIsDown) return;
            resizeDirtyFlag = true;
            ResizingText.Visibility = Visibility.Visible;
            UpdateResizeText();
        }

        #endregion

        #region Remote Control Interface

        RemoteControlManager rcm;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
            source.AddHook(WndProc);
            rcm = new RemoteControlManager(this);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch ((Message)msg)
            {
                case Message.Ping:
                    {
                        handled = true;
                        return new IntPtr(1);
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

        struct LayoutData
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
        enum EdgeHighlight
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

        private void CounterWindow_LocationChanged(object sender, EventArgs e)
        {
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
            SetSizeLocked(profile.sizeLocked, true);
            SetShowOdds(profile.showOdds, true);
            SetFiltering(profile.bitmapScalingMode);
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

            TryRegisterHotkey(ref incrementHook, metaSettings.data.incrementHotkey, IncrementHook_KeyPressed);
            TryRegisterHotkey(ref decrementHook, metaSettings.data.decrementHotkey, DecrementHook_KeyPressed);

            RefreshAll();
        }

        void SetDirty()
        {
            currentProfile.SetIsDirty(true);
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
                    var result = System.Windows.MessageBox.Show("Do you want to save your current profile before loading the new one?", "Wait", MessageBoxButton.YesNo);

                    if (result == MessageBoxResult.Yes)
                        Commands.CustomCommands.Save.Execute(null, this);
                }

            var profile = CounterProfile.LoadFrom(path);
            if (profile != null)
            {
                InitializeFromProfile(profile);
                undoList.Clear();
                metaSettings.data.recentProfiles.Add(path);
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
            e.CanExecute = SaveOption.IsEnabled = (currentProfile as IDirtyable).GetIsDirty();
        }

        private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (currentProfile.path == null)
            {
                currentProfile.SaveAs();
            }
            else
            {
                if (currentProfile.Save())
                {
                    Dispatcher.BeginInvoke(ShowSavedText);
                }
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
            var result = System.Windows.MessageBox.Show("Are you sure you want to discard your current profile and create a new one?", "Wait", MessageBoxButton.YesNo);

            if (result == MessageBoxResult.Yes)
            {
                InitializeFromProfile(CounterProfile.CreateDefault());
                undoList.Clear();
                SetDirty();
            }
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

        #endregion

        #region Hotkeys

        static bool TryRegisterHotkey(ref KeyboardHook refHook, GlobalHotkey hotkey, EventHandler<KeyPressedEventArgs> function)
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


        private void BindGlobalIncrementKey_Click(object sender, RoutedEventArgs e)
        {
            incrementHook.KeyPressed -= IncrementHook_KeyPressed;
            incrementHook.Dispose();
            PressAnyButtonPopup popup = new PressAnyButtonPopup("global increment key");
            popup.value = metaSettings.data.incrementHotkey.keys;

            if (popup.ShowDialog().GetValueOrDefault(false))
            {
                metaSettings.data.incrementHotkey.keys = popup.value;
            }
            TryRegisterHotkey(ref incrementHook, metaSettings.data.incrementHotkey, IncrementHook_KeyPressed);
        }

        private void BindGlobalDecrementKey_Click(object sender, RoutedEventArgs e)
        {
            decrementHook.KeyPressed -= DecrementHook_KeyPressed;
            decrementHook.Dispose();
            PressAnyButtonPopup popup = new PressAnyButtonPopup("global decrement key");
            popup.value = metaSettings.data.decrementHotkey.keys;

            if (popup.ShowDialog().GetValueOrDefault(false))
            {
                metaSettings.data.decrementHotkey.keys = popup.value;
            }
            TryRegisterHotkey(ref decrementHook, metaSettings.data.decrementHotkey, DecrementHook_KeyPressed);
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
        public async void StartEscapeTracker()
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


        #endregion

        #region Counter + Counter text

        private void ResetCounter()
        {
            // Done twice because we don't record regular increments
            undoList.PushChange(currentProfile);
            currentProfile.count = 0;
            RefreshCounterText();
            SetDirty();
            undoList.PushChange(currentProfile);
        }

        private void IncrementCounter(int delta)
        {
            currentProfile.count = Math.Max(0, currentProfile.count + delta);
            SetDirty();
            RefreshCounterText();
        }

        #endregion

        #region Option Functions

        void SetSizeLocked(bool aLockedSize, bool updateCheckmark = false)
        {
            currentProfile.sizeLocked = aLockedSize;

            if (aLockedSize)
            {
                CounterWindow.ResizeMode = ResizeMode.NoResize;
            }
            else
            {
                CounterWindow.ResizeMode = ResizeMode.CanResize;
                MinHeight = MinimumHeight;
                MinWidth = MinimumWidth;
            }

            if (updateCheckmark)
            {
                LockSizeOption.IsChecked = aLockedSize;
            }
        }

        #endregion

        #region Context Menu

        #region Save

        #endregion

        #region Window Size

        private void SetWindowSizeOption_Click(object sender, RoutedEventArgs e)
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
                SetDirty();
                undoList.PushChange(currentProfile);
            }
        }

        private void ResetSizeOption_Click(object sender, RoutedEventArgs e)
        {
            Width = DefaultWidth;
            Height = DefaultHeight;
            currentProfile.windowWidth = Width;
            currentProfile.windowHeight = Height;
            SetDirty();
            undoList.PushChange(currentProfile);
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
            undoList.PushChange(currentProfile);
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

            var result = openFileDialog.ShowDialog();
            if (result != System.Windows.Forms.DialogResult.OK) return;
            if (!File.Exists(openFileDialog.FileName)) return;

            LoadImageFromFile(openFileDialog.FileName);
            SetDirty();
            undoList.PushChange(currentProfile);
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

        private void SetImageFromPokemonOption_Click(object sender, RoutedEventArgs e)
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
                LoadImageFromFile(popup.imagePath);
                SetDirty();
                RefreshAll();
                undoList.PushChange(currentProfile);
            }
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
            SetDirty();
            undoList.PushChange(currentProfile);
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
            SetDirty();
            undoList.PushChange(currentProfile);
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
            SetDirty();
            undoList.PushChange(currentProfile);
            RefreshContextMenuTickboxes();
        }

        // Filtering
        private void SetImageFilteringNearestNeighbor_Click(object sender, RoutedEventArgs e)
        {
            SetFiltering(BitmapScalingMode.NearestNeighbor);
            undoList.PushChange(currentProfile);
        }

        private void SetImageFilteringLinear_Click(object sender, RoutedEventArgs e)
        {
            SetFiltering(BitmapScalingMode.Linear);
            undoList.PushChange(currentProfile);
        }

        private void SetImageFilteringHighQuality_Click(object sender, RoutedEventArgs e)
        {
            SetFiltering(BitmapScalingMode.HighQuality);
            undoList.PushChange(currentProfile);
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
            SetDirty();
            undoList.PushChange(currentProfile);
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
                    SetDirty();
                    undoList.PushChange(currentProfile);
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
                    SetDirty();
                    undoList.PushChange(currentProfile);
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
                    SetDirty();
                    undoList.PushChange(currentProfile);
                }
            };

            statTextColorPickerPopup.Show();
        }

        #endregion

        #region Odds

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

        private void ShowOddsOption_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            SetShowOdds(true);
        }

        private void ShowOddsOption_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            SetShowOdds(false);
        }

        private void ShowOddsOption_Click(object sender, RoutedEventArgs e)
        {
            undoList.PushChange(currentProfile);
        }

        private void SetTargetOddsDirectOption_Click(object sender, RoutedEventArgs e)
        {
            var mi = (e.Source as MenuItem);
            if (mi != null)
            {
                if (int.TryParse(mi.Header as string, out int odds))
                {
                    currentProfile.targetOdds = odds;
                    currentProfile.targetOddsShinyRolls = 1;
                    SetDirty();
                    undoList.PushChange(currentProfile);
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
                SetDirty();
                undoList.PushChange(currentProfile);
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

        #region Other

        // Always on top (topmost)
        private void AlwaysOnTopOption_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            SetAlwaysOnTopOption(false);
        }

        private void AlwaysOnTopOption_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            SetAlwaysOnTopOption(true);
        }

        private void AlwaysOnTopOption_Click(object sender, RoutedEventArgs e)
        {
            undoList.PushChange(currentProfile);
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
            System.Windows.MessageBox.Show(".counter files will now open with this executable", "Success!");
        }
        public static bool IsAdministrator =>
             new WindowsPrincipal(WindowsIdentity.GetCurrent())
                 .IsInRole(WindowsBuiltInRole.Administrator);

        static void EnsureFileAssociation()
        {
            FileAssociations.EnsureAssociationsSet(System.IO.Path.Combine(Paths.ExecutableDirectory, "icons", "pc file.ico"));
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

using Newtonsoft.Json;
using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;

namespace PokeCounter
{
    public delegate void IsDirtyChanged(bool newDirty);
    interface IDirtyable
    {
        public bool GetIsDirty();
        public void SetIsDirty(bool isDirty);
        public event IsDirtyChanged DirtyChanged;
    }

    public struct GlobalHotkey
    {
        public GlobalHotkey(Keys keys, HotkeyModifierKeys modifierKeys)
        {
            this.keys = keys;
            this.modifierKeys = modifierKeys;
        }

        public Keys keys;
        public HotkeyModifierKeys modifierKeys;
    }

    class CounterProfile : IUndoObject<CounterProfile>, IDirtyable
    {
        public string path = null;
        public int count = 0;
        public int incrementAmount = 1;
        public int targetOdds = 4096, targetOddsShinyRolls = 1;
        public string backgroundImagePath = System.IO.Path.Combine(Paths.ExecutableDirectory, "images", "0251Celebi.png");
        public double windowWidth = MainWindow.DefaultWidth, windowHeight = MainWindow.DefaultHeight;
        public double imageWidth = double.NaN, imageHeight = double.NaN;
        public bool sizeLocked = true;
        public Stretch stretch = Stretch.Uniform;
        public VerticalAlignment verticalAlignment = VerticalAlignment.Top;
        public System.Windows.HorizontalAlignment horizontalAlignment = System.Windows.HorizontalAlignment.Center;
        public Color textColor = Color.FromRgb(23, 102, 6);
        public Color statTextColor = Color.FromRgb(0, 0, 0);
        public Color backgroundColor = Color.FromRgb(255, 255, 255);
        public bool showOdds = true;
        public BitmapScalingMode bitmapScalingMode = BitmapScalingMode.NearestNeighbor;
        public int cachedPokemonIndex = 0;
        public Options cachedPokemonOptions = 0;
        public CategoryWrapper cachedPokemonGame = new CategoryWrapper()
        {
            uniqueIdentifier = "official"
        };

        [JsonIgnore]
        private bool isDirty;
        public event IsDirtyChanged DirtyChanged;

        public static string DefaultExtension => ".counter";

        public static CounterProfile LoadFrom(string path)
        {
            CounterProfile profile = null;

            if (!File.Exists(path))
            {
                System.Windows.MessageBox.Show("Invalid profile path!", "Oopsie!", MessageBoxButton.OK, MessageBoxImage.Error);
                return profile;
            }

            string text = File.ReadAllText(path);
            CounterProfile loadedProfile = JsonConvert.DeserializeObject<CounterProfile>(text);
            loadedProfile.path = path;
            if (loadedProfile == null)
            {
                System.Windows.MessageBox.Show("Profile failed to load!", "Oopsie!", MessageBoxButton.OK, MessageBoxImage.Error);
                return profile;
            }

            return loadedProfile;
        }

        public static CounterProfile CreateDefault()
        {
            CounterProfile defaultProfile = new CounterProfile();
            defaultProfile.path = null;
            defaultProfile.Save();
            return defaultProfile;
        }

        public bool SaveAs()
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.DefaultExt = DefaultExtension;
            saveFileDialog.AddExtension = true;
            saveFileDialog.Title = "Save Counter Profile";
            if (path != null)
                saveFileDialog.InitialDirectory = System.IO.Path.GetDirectoryName(path);
            else
                saveFileDialog.InitialDirectory = Paths.ExecutableDirectory;

            var result = saveFileDialog.ShowDialog();

            if (result != DialogResult.OK) return false;

            path = saveFileDialog.FileName;
            SetIsDirty(true);
            Save();
            return true;
        }

        public bool Save()
        {
            if (GetIsDirty())
            {
                try
                {
                    File.WriteAllText(path, JsonConvert.SerializeObject(this));
                    SetIsDirty(false);
                    return true;
                }
                catch (Exception)
                {
                    System.Windows.MessageBox.Show($"Could not save file {path}.\n Is it being used by another process?", "Save failed!", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            return false;
        }

        public CounterProfile GetCopy()
        {
            return (CounterProfile)MemberwiseClone();
        }

        public bool GetIsDirty()
        {
            return isDirty;
        }

        public void SetIsDirty(bool isDirty)
        {
            this.isDirty = isDirty;
            DirtyChanged?.Invoke(isDirty);
        }
    }
}

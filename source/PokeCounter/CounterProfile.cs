﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
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

    public struct KeyCombination
    {
        public static bool operator ==(KeyCombination a, KeyCombination b)
        {
            return a.keys == b.keys && a.modifierKeys == b.modifierKeys;
        }

        public static bool operator !=(KeyCombination a, KeyCombination b)
        {
            return a.keys != b.keys || a.modifierKeys != b.modifierKeys;
        }

        public override bool Equals(object obj)
        {
            if (obj is KeyCombination)
            {
                var kc = (KeyCombination)obj;
                return kc == this;
            }
            return false;
        }

        public KeyCombination(Key keys, ModifierKeys modifierKeys)
        {
            this.keys = keys;
            this.modifierKeys = modifierKeys;
        }

        public KeyCombination(KeyCombination other)
        {
            keys = other.keys;
            modifierKeys = other.modifierKeys;
        }

        public KeyCombination(object keyGesture)
        {
            if (keyGesture is KeyGesture inputGesture)
            {
                keys = inputGesture.Key;
                modifierKeys = inputGesture.Modifiers;
            }
            else
            {
                keys = 0; modifierKeys = 0;
            }
        }

        [JsonIgnore]
        public KeyGesture Gesture => new KeyGesture(keys, modifierKeys);

        public override string ToString()
        {
            string keyCombo = keys.ToString();
            if (modifierKeys != 0) keyCombo = (modifierKeys.ToString() + "+") + keyCombo;
            return keyCombo;
        }

        public Key keys;
        public ModifierKeys modifierKeys;
    }

    class CounterProfile : IUndoObject<CounterProfile>, IDirtyable
    {
        [JsonIgnore]
        public string Name => Path.GetFileName(path);
        public string path = null;
        public int count = 0;
        public int incrementAmount = 1;
        public int targetOdds = 4096, targetOddsShinyRolls = 1;
        public string backgroundImagePath = System.IO.Path.Combine(Paths.ExecutableDirectory, "images", "0251Celebi.png");
        public double windowWidth = MainWindow.DefaultWidth, windowHeight = MainWindow.DefaultHeight;
        public double windowTop = double.NaN, windowLeft = double.NaN;
        public double imageWidth = double.NaN, imageHeight = double.NaN;
        public bool sizeLocked = true;
        public Stretch stretch = Stretch.Uniform;
        public VerticalAlignment verticalAlignment = VerticalAlignment.Top;
        public System.Windows.HorizontalAlignment horizontalAlignment = System.Windows.HorizontalAlignment.Center;
        public Color textColor = Color.FromRgb(23, 102, 6);
        public Color statTextColor = Color.FromRgb(0, 0, 0);
        public Color backgroundColor = Color.FromRgb(255, 255, 255);
        public bool showOdds = true;
        public BitmapScalingMode bitmapScalingMode = BitmapScalingMode.HighQuality;
        public int cachedPokemonIndex = -1;
        public Options cachedPokemonOptions = 0;
        public CategoryWrapper cachedPokemonGame = new CategoryWrapper()
        {
            uniqueIdentifier = "official"
        };
        public string cachedImageURL = "";
        public string audioPath = System.IO.Path.Combine(Paths.ExecutableDirectory, "audio", "defaultSound.wav");

        public KeyCombination incrementHotkey = new KeyCombination(Key.Add, 0);
        public KeyCombination decrementHotkey = new KeyCombination(Key.Subtract, 0);

        [JsonIgnore]
        private bool isDirty;
        public event IsDirtyChanged DirtyChanged;

        public static string DefaultExtension => ".counter";
        public static string GroupFileExtension => ".counterGroup";


        public static CounterProfile LoadFrom(string path)
        {
            CounterProfile profile = null;

            if (!File.Exists(path))
            {
                System.Windows.MessageBox.Show($"Invalid profile path!\n{path}\nCould not be found", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                return profile;
            }

            string text = File.ReadAllText(path);
            CounterProfile loadedProfile = JsonConvert.DeserializeObject<CounterProfile>(text);
            if (loadedProfile == null)
            {
                System.Windows.MessageBox.Show($"Profile failed to load!\n{path}\nCould not be loaded", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                return profile;
            }

            loadedProfile.path = path;

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
            saveFileDialog.Filter = $"Counter file (*{DefaultExtension})|*{DefaultExtension}";
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
            if (GetIsDirty() && path != "" && path != null)
            {
                try
                {
                    File.WriteAllText(path, JsonConvert.SerializeObject(this));
                    SetIsDirty(false);
                    return true;
                }
                catch (Exception e)
                {
                    System.Windows.MessageBox.Show($"Could not save file {path}.\n Is it being used by another process?\n{e}", "Save failed!", MessageBoxButton.OK, MessageBoxImage.Error);
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

    class CounterGroup
    {
        public class CounterInfo
        {
            public string profilePath;
            public MainWindow.LayoutData layout;
        }

        public List<CounterInfo> counters = new List<CounterInfo>();
    }
}

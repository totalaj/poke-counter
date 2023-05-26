using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfAnimatedGif;

namespace PokeCounter
{
    /// <summary>
    /// Interaction logic for SelectPokemonPopup.xaml
    /// </summary>
    public partial class SelectPokemonPopup : Window
    {
        readonly List<PokemonInfo> pokemonDatas;

        public int cachedPokemon;
        public Options cachedOptions;
        public CategoryWrapper cachedGame;

        public SelectPokemonPopup(string valueName, List<PokemonInfo> pokemonDatas, BitmapScalingMode bitmapScalingMode)
        {
            InitializeComponent();
            this.pokemonDatas = pokemonDatas;
            PopupWindow.Title = "Set " + valueName;
            RenderOptions.SetBitmapScalingMode(PreviewImage, bitmapScalingMode);
            new DispatcherTimer(
                TimeSpan.FromMilliseconds(100), DispatcherPriority.Render,
                delegate
                {
                    if (requests.Count == 0) return;
                    ProcessRequest(requests.Dequeue());
                },
                Dispatcher);
        }

        void ProcessRequest(DownloadRequest downloadRequest)
        {
            string extension = "";
            if (downloadRequest.imageURL != null)
                extension = System.IO.Path.GetExtension(downloadRequest.imageURL);
            string relativeTargetPath = downloadRequest.categoryIdentifier
            + "_" + downloadRequest.pokemonName + "_" + SpriteCategory.OptionEnumToString(downloadRequest.options) + extension;
            string targetFile = System.IO.Path.Combine(Paths.ImageCacheDirectory, relativeTargetPath);

            if (downloadRequest.imageURL != null)
            {
                DownloadManager.DownloadImage(downloadRequest.imageURL, targetFile);
            }

            if (!File.Exists(targetFile) || downloadRequest.imageURL == null)
            {
                ImageBehavior.SetAnimatedSource(PreviewImage, null);
                PreviewImage.Source = InvalidImageHolder.Source;
                return;
            }
            try
            {
                imagePath = targetFile;
                relativeImagePath = relativeTargetPath;
                imageURL = downloadRequest.imageURL;

                FileInfo finfo = new FileInfo(imagePath);

                BitmapImage bmp = new BitmapImage(new Uri(imagePath));

                if (bmp == null) return;

                if (finfo.Extension == ".gif")
                {
                    ImageBehavior.SetAnimatedSource(PreviewImage, bmp);
                    PreviewImage.Source = null;
                }
                else
                {
                    ImageBehavior.SetAnimatedSource(PreviewImage, null);
                    PreviewImage.Source = bmp;
                }
            }
            catch
            {
                ImageBehavior.SetAnimatedSource(PreviewImage, null);
                PreviewImage.Source = InvalidImageHolder.Source;
            }
        }

        private void PokemonDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                InitializeNewPokemon(PokemonDropdown.SelectedItem as PokemonInfo);
            }
            catch (Exception exception)
            {
                MessageBox.Show($"Failed to download data for pokemon {pokemonDatas[PokemonDropdown.SelectedIndex].name}!\n{exception.Message}");
            }
        }

        private void InitializeCategoryListFromPokemon(Pokemon pokemon)
        {
            currentPokemon = pokemon;

            List<CategoryWrapper> games = new List<CategoryWrapper>();
            games.AddRange(GetSpriteCategoriesFromObject(pokemon.Sprites.versions.generation1));
            games.AddRange(GetSpriteCategoriesFromObject(pokemon.Sprites.versions.generation2));
            games.AddRange(GetSpriteCategoriesFromObject(pokemon.Sprites.versions.generation3));
            games.AddRange(GetSpriteCategoriesFromObject(pokemon.Sprites.versions.generation4));
            games.AddRange(GetSpriteCategoriesFromObject(pokemon.Sprites.versions.generation5));
            games.AddRange(GetSpriteCategoriesFromObject(pokemon.Sprites.versions.generation6));
            games.AddRange(GetSpriteCategoriesFromObject(pokemon.Sprites.versions.generation7));
            games.AddRange(GetSpriteCategoriesFromObject(pokemon.Sprites.versions.generation8));
            games.AddRange(GetSpriteCategoriesFromObject(pokemon.Sprites.generation9));
            games.AddRange(GetSpriteCategoriesFromObject(pokemon.Sprites.other));

            var prevSelectedGame = GameVersionDropdown.SelectedItem as CategoryWrapper;

            GameVersionDropdown.Items.Clear();
            foreach (var game in games)
            {
                if (game.category != null)
                    GameVersionDropdown.Items.Add(game);
            }

            bool foundGame = false;

            if (prevSelectedGame != null)
            {
                foreach (var item in GameVersionDropdown.Items)
                {
                    if ((item as CategoryWrapper).uniqueIdentifier == prevSelectedGame.uniqueIdentifier)
                    {
                        if ((item as CategoryWrapper).category != null)
                        {
                            GameVersionDropdown.SelectedItem = item;
                            foundGame = true;
                            break;
                        }
                    }
                }
            }

            if (!foundGame)
            {
                GameVersionDropdown.SelectedIndex = 0;
            }

            InitializeNewCategory(GameVersionDropdown.SelectedItem as CategoryWrapper);
        }

        void InitializeNewPokemon(PokemonInfo pokemonInfo)
        {
            Pokemon pokemon = DownloadManager.DownloadObject<Pokemon>(pokemonInfo.url);

            InitializeCategoryListFromPokemon(pokemon);

            var species = DownloadManager.DownloadObject<PokemonSpecies>(pokemon.species.url);

            int formCount = pokemon.FormInfos.Count + species.varieties.Count;

            FormDropdownSlot.Visibility = formCount > 2 ? Visibility.Visible : Visibility.Collapsed;

            if (formCount > 2)
            {
                FormDropdown.Items.Clear();

                List<object> forms = new List<object>();
                foreach (var variety in species.varieties)
                {
                    forms.Add(DownloadManager.DownloadObject<Pokemon>(variety.pokemon.url));
                }

                if (pokemon.FormInfos.Count > 1)
                    foreach (var formInfo in pokemon.FormInfos)
                    {
                        try
                        {
                            forms.AddRange(GetSpriteCategoriesFromObject(DownloadManager.DownloadObject<PokemonForm>(formInfo.url)));
                        }
                        catch (Exception)
                        {
                            throw;
                        }
                    }

                foreach (var form in forms)
                {
                    if (form is CategoryWrapper cw)
                    {
                        if (cw.category != null)
                        {
                            FormDropdown.Items.Add(form);
                        }
                    }
                    else
                    {
                        FormDropdown.Items.Add(form);
                    }
                }

                FormDropdown.SelectedIndex = 0;
            }

            UpdateImage();
        }

        void InitializeNewCategory(CategoryWrapper category)
        {
            var options = category.category.GetAvailableOptions();
            var currentOptions = GetCurrentOptions();

            currentCategory = category;

            FemaleProperty.Visibility = options.HasFlag(Options.female) ? Visibility.Visible : Visibility.Collapsed;
            TransparentProperty.Visibility = options.HasFlag(Options.transparent) ? Visibility.Visible : Visibility.Collapsed;
            GrayProperty.Visibility = options.HasFlag(Options.gray) ? Visibility.Visible : Visibility.Collapsed;
            BackProperty.Visibility = options.HasFlag(Options.back) ? Visibility.Visible : Visibility.Collapsed;
            ShinyProperty.Visibility = options.HasFlag(Options.shiny) ? Visibility.Visible : Visibility.Collapsed;
            AnimatedProperty.Visibility = options.HasFlag(Options.animated) ? Visibility.Visible : Visibility.Collapsed;
            IconProperty.Visibility = options.HasFlag(Options.icon) ? Visibility.Visible : Visibility.Collapsed;
            FemaleProperty.IsChecked = ((currentOptions & options).HasFlag(Options.female));
            TransparentProperty.IsChecked = ((currentOptions & options).HasFlag(Options.transparent));
            GrayProperty.IsChecked = ((currentOptions & options).HasFlag(Options.gray));
            BackProperty.IsChecked = ((currentOptions & options).HasFlag(Options.back));
            ShinyProperty.IsChecked = ((currentOptions & options).HasFlag(Options.shiny));
            AnimatedProperty.IsChecked = ((currentOptions & options).HasFlag(Options.animated));
            IconProperty.IsChecked = ((currentOptions & options).HasFlag(Options.icon));

            UpdateImage();
        }

        static List<CategoryWrapper> GetSpriteCategoriesFromObject(object owningObject)
        {
            List<CategoryWrapper> games = new List<CategoryWrapper>();
            Type t = owningObject.GetType();
            var properties = t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            foreach (var prop in properties)
            {
                if (prop.GetValue(owningObject) is SpriteCategory)
                {
                    bool shouldAdd = true;

                    bool allNull = true;
                    foreach (var spriteField in prop.PropertyType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                    {
                        if (spriteField.GetValue(prop.GetValue(owningObject)) != null && spriteField.FieldType == typeof(string))
                        {
                            allNull = false;
                            break;
                        }
                    }
                    if (allNull) shouldAdd = false;
                    if (owningObject == null) shouldAdd = false;

                    if (!shouldAdd) continue;

                    string name = "No name";
                    string uniqueIdentifier = "NONAME";

                    foreach (var attr in prop.GetCustomAttributes(true))
                    {
                        if (attr is DisplayNameAttribute)
                        {
                            name = (attr as DisplayNameAttribute).Name;
                            uniqueIdentifier = (attr as DisplayNameAttribute).UniqueIdentifier;
                        }
                    }

                    if (owningObject is IDisplayName dn)
                    {
                        name = dn.GetName();
                        uniqueIdentifier = dn.GetUniqueIdentifier();
                    }

                    games.Add(new CategoryWrapper()
                    {
                        name = name,
                        uniqueIdentifier = uniqueIdentifier,
                        category = prop.GetValue(owningObject) as SpriteCategory
                    });
                }
            }
            return games;
        }

        Options GetCurrentOptions()
        {
            return (FemaleProperty.IsChecked.GetValueOrDefault(false) ? Options.female : 0)
                 | (TransparentProperty.IsChecked.GetValueOrDefault(false) ? Options.transparent : 0)
                 | (GrayProperty.IsChecked.GetValueOrDefault(false) ? Options.gray : 0)
                 | (BackProperty.IsChecked.GetValueOrDefault(false) ? Options.back : 0)
                 | (ShinyProperty.IsChecked.GetValueOrDefault(false) ? Options.shiny : 0)
                 | (AnimatedProperty.IsChecked.GetValueOrDefault(false) ? Options.animated : 0)
                 | (IconProperty.IsChecked.GetValueOrDefault(false) ? Options.icon : 0);
        }

        void SetCurrentOptions(Options currentOptions)
        {
            FemaleProperty.IsChecked = currentOptions.HasFlag(Options.female);
            TransparentProperty.IsChecked = currentOptions.HasFlag(Options.transparent);
            GrayProperty.IsChecked = currentOptions.HasFlag(Options.gray);
            BackProperty.IsChecked = currentOptions.HasFlag(Options.back);
            ShinyProperty.IsChecked = currentOptions.HasFlag(Options.shiny);
            AnimatedProperty.IsChecked = currentOptions.HasFlag(Options.animated);
        }

        class DownloadRequest
        {
            public string imageURL;
            public string categoryIdentifier;
            public string pokemonName;
            public Options options;
        }

        CategoryWrapper currentCategory;
        Queue<DownloadRequest> requests = new Queue<DownloadRequest>();
        public string imagePath;
        public Pokemon currentPokemon;
        public string targetResolution;
        public string relativeDirectory;
        public string relativeImagePath, imageURL;
        bool init = false;

        void UpdateImage()
        {
            if (currentCategory == null) return;

            var options = GetCurrentOptions();

            string pokemonName = "", categoryIdentifier = "";
            if (PokemonDropdown.SelectedItem is PokemonInfo pInfo) pokemonName = pInfo.name;
            if (GameVersionDropdown.SelectedItem is CategoryWrapper cWrapper) categoryIdentifier = cWrapper.uniqueIdentifier;

            requests.Enqueue(new DownloadRequest()
            {
                imageURL = (currentCategory.category).GetSpriteFromOptions(options),
                options = options,
                categoryIdentifier = categoryIdentifier,
                pokemonName = pokemonName
            });

            FemaleProperty.IsEnabled = currentCategory.category.GetSpriteFromOptions(options | Options.female) != null;
            TransparentProperty.IsEnabled = currentCategory.category.GetSpriteFromOptions(options | Options.transparent) != null;
            GrayProperty.IsEnabled = currentCategory.category.GetSpriteFromOptions(options | Options.gray) != null;
            BackProperty.IsEnabled = currentCategory.category.GetSpriteFromOptions(options | Options.back) != null;
            ShinyProperty.IsEnabled = currentCategory.category.GetSpriteFromOptions(options | Options.shiny) != null;
            AnimatedProperty.IsEnabled = currentCategory.category.GetSpriteFromOptions(options | Options.animated) != null;
            IconProperty.IsEnabled = currentCategory.category.GetSpriteFromOptions(options | Options.icon) != null;
        }

        public void Complete()
        {
            bool result = true;

            cachedGame = currentCategory;
            cachedPokemon = PokemonDropdown.SelectedIndex;
            cachedOptions = GetCurrentOptions();

            if (requests.Count > 0) ProcessRequest(requests.Last());

            DialogResult = result;
            Close();
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            Complete();
        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!init)
            {
                init = true;
                for (int i = 0; i < pokemonDatas.Count; i++)
                {
                    PokemonDropdown.Items.Add(pokemonDatas[i]);
                }
                PokemonDropdown.SelectedIndex = cachedPokemon;

                if (cachedGame != null)
                {
                    foreach (CategoryWrapper game in GameVersionDropdown.Items)
                    {
                        if (game.uniqueIdentifier == cachedGame.uniqueIdentifier)
                        {
                            GameVersionDropdown.SelectedItem = game;
                            break;
                        }
                    }
                }

                SetCurrentOptions(cachedOptions);
                UpdateImage();
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            var destinationurl = e.Uri.ToString();
            var sInfo = new System.Diagnostics.ProcessStartInfo(destinationurl)
            {
                UseShellExecute = true,
            };
            System.Diagnostics.Process.Start(sInfo);
        }

        private void GameVersionDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var wrapper = GameVersionDropdown.SelectedItem as CategoryWrapper;
            if (wrapper == null || wrapper.category == null) return;

            InitializeNewCategory(wrapper);
        }

        private void Property_Click(object sender, RoutedEventArgs e)
        {
            UpdateImage();
        }

        private void FormDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FormDropdown.SelectedItem is CategoryWrapper cw)
            {
                if (cw.category is PokemonForm.FormSpriteCategory form)
                {
                    if (form.isBaseForm)
                    {
                        GameDropdownSlot.Visibility = Visibility.Visible;
                        if (GameVersionDropdown.SelectedItem is CategoryWrapper game)
                        {
                            InitializeNewCategory(game);
                        }
                        else if (GameVersionDropdown.Items.GetItemAt(0) is CategoryWrapper defaultGame)
                        {
                            InitializeNewCategory(defaultGame);
                        }
                    }
                    else
                    {
                        GameDropdownSlot.Visibility = Visibility.Collapsed;
                        InitializeNewCategory(cw);
                    }
                }
            }
            if (FormDropdown.SelectedItem is Pokemon pokemon)
            {
                InitializeCategoryListFromPokemon(pokemon);
            }
        }

        private void PopupWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed) Close();
        }
    }
}

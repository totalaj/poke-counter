using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PokeCounter
{

    [System.AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    sealed class DisplayNameAttribute : Attribute
    {
        readonly string name;
        readonly string uniqueIdentifier;

        public DisplayNameAttribute(string name, string uniqueIdentifier = "")
        {
            this.name = name;
            if (uniqueIdentifier == "")
                this.uniqueIdentifier = name.Trim().ToLower().Replace(" ", "");
            else
                this.uniqueIdentifier = uniqueIdentifier;
        }

        public string Name
        {
            get { return name; }
        }

        public string UniqueIdentifier
        {
            get { return uniqueIdentifier; }
        }

    }
    [System.AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class OptionQualifiersAttribute : Attribute
    {
        readonly Options options;

        public OptionQualifiersAttribute(Options options)
        {
            this.options = options;
        }

        public Options Options
        {
            get { return options; }
        }
    }

    interface IDisplayName
    {
        string GetName();
        string GetUniqueIdentifier();
    }

    public class CategoryWrapper
    {
        public override string ToString()
        {
            return name;
        }
        public string name;
        public string uniqueIdentifier;
        public SpriteCategory category;
    }

    public class SpriteCategory
    {
        public static string OptionEnumToString(Options option)
        {
            if (option == 0) return "default";

            return
                (option.HasFlag(Options.female) ? "female" : "") +
                (option.HasFlag(Options.transparent) ? "transparent" : "") +
                (option.HasFlag(Options.gray) ? "gray" : "") +
                (option.HasFlag(Options.back) ? "back" : "") +
                (option.HasFlag(Options.shiny) ? "shiny" : "") +
                (option.HasFlag(Options.animated) ? "animated" : "") +
                (option.HasFlag(Options.icon) ? "icon" : "");
        }

        public Options GetAvailableOptions()
        {
            return GetOptionsFromType(this, GetType());
        }

        Options GetOptionsFromType(object o, Type t)
        {
            Options options = 0;
            var fields = t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            foreach (var field in fields)
            {
                foreach (var attr in field.GetCustomAttributes(true))
                {
                    if (attr is OptionQualifiersAttribute && field.GetValue(o) != null)
                    {
                        var opt = (attr as OptionQualifiersAttribute);
                        options |= opt.Options;
                    }
                }
                options |= GetOptionsFromType(field.GetValue(o), field.FieldType);
            }
            return options;
        }

        public string GetDefaultSprite()
        {
            return GetSpriteFromOptions(0);
        }

        public string GetSpriteFromOptions(Options options)
        {
            return GetSpriteFromObjectRecursively(this, options);
        }

        string GetSpriteFromObjectRecursively(object o, Options options)
        {
            if (o == null) return null;
            Type t = o.GetType();
            var fields = t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            foreach (var field in fields)
            {
                foreach (var attr in field.GetCustomAttributes(true))
                {
                    if (attr is OptionQualifiersAttribute)
                    {
                        var opt = (attr as OptionQualifiersAttribute);
                        if (opt.Options.Equals(options))
                        {
                            return field.GetValue(o) as string;
                        }
                    }
                }

                string sprite = GetSpriteFromObjectRecursively(field.GetValue(o), options);
                if (sprite != null) return sprite;
            }
            return null;
        }
    };

    [Flags]
    public enum Options
    {
        none = 0,
        female = 1,
        transparent = 2,
        gray = 4,
        back = 8,
        shiny = 16,
        animated = 32,
        icon = 64
    }

    public class PokemonSprites
    {
        public class Versions
        {
            public class Generation1
            {
                public class Gen1Game : SpriteCategory
                {
                    [OptionQualifiers(Options.back)]
                    [JsonProperty("back_default")]
                    public string backDefault;
                    [OptionQualifiers(Options.back | Options.gray)]
                    [JsonProperty("back_gray")]
                    public string backGray;
                    [OptionQualifiers(Options.back | Options.transparent)]
                    [JsonProperty("back_transparent")]
                    public string backTransparent;
                    [OptionQualifiers(0)]
                    [JsonProperty("front_default")]
                    public string frontDefault;
                    [OptionQualifiers(Options.gray)]
                    [JsonProperty("front_gray")]
                    public string frontGray;
                    [OptionQualifiers(Options.transparent)]
                    [JsonProperty("front_transparent")]
                    public string frontTransparent;
                }

                [DisplayName("Red & Blue", "rb")]
                [JsonProperty("red-blue")]
                public Gen1Game redBlue { set; get; }
                [DisplayName("Yellow", "y")]
                [JsonProperty("yellow")]
                public Gen1Game yellow { set; get; }
            }

            public class Generation2
            {
                public class Crystal : SpriteCategory
                {
                    [OptionQualifiers(Options.back)]
                    [JsonProperty("back_default")]
                    public string backDefault;
                    [OptionQualifiers(Options.back | Options.shiny)]
                    [JsonProperty("back_shiny")]
                    public string backShiny;
                    [OptionQualifiers(Options.transparent | Options.shiny | Options.back)]
                    [JsonProperty("back_shiny_transparent")]
                    public string backShinyTransparent;
                    [OptionQualifiers(Options.back | Options.transparent)]
                    [JsonProperty("back_transparent")]
                    public string backTransparent;
                    [OptionQualifiers(0)]
                    [JsonProperty("front_default")]
                    public string frontDefault;
                    [OptionQualifiers(Options.shiny)]
                    [JsonProperty("front_shiny")]
                    public string frontShiny;
                    [OptionQualifiers(Options.transparent | Options.shiny)]
                    [JsonProperty("front_shiny_transparent")]
                    public string frontShinyTransparent;
                    [OptionQualifiers(Options.transparent)]
                    [JsonProperty("front_transparent")]
                    public string frontTransparent;
                }

                public class GoldSilver
                {
                    [OptionQualifiers(Options.back)]
                    [JsonProperty("back_default")]
                    public string backDefault;
                    [OptionQualifiers(Options.shiny)]
                    [JsonProperty("back_shiny")]
                    public string backShiny;
                    [OptionQualifiers(0)]
                    [JsonProperty("front_default")]
                    public string frontDefault;
                    [OptionQualifiers(Options.shiny)]
                    [JsonProperty("front_shiny")]
                    public string frontShiny;
                    [OptionQualifiers(Options.transparent)]
                    [JsonProperty("front_transparent")]
                    public string frontTransparent;
                }

                [DisplayName("Crystal", "c")]
                [JsonProperty("crystal")]
                public Crystal crystal { set; get; }
                [DisplayName("Gold", "g")]
                [JsonProperty("gold")]
                public GoldSilver gold { set; get; }
                [DisplayName("Silver", "s")]
                [JsonProperty("silver")]
                public GoldSilver silver { set; get; }
            }

            public class Generation3
            {
                public class Emerald : SpriteCategory
                {
                    [OptionQualifiers(0)]
                    [JsonProperty("front_default")]
                    public string frontDefault;
                    [OptionQualifiers(Options.shiny)]
                    [JsonProperty("front_shiny")]
                    public string frontShiny;
                }

                public class RSEFRLG
                {
                    [OptionQualifiers(0)]
                    [JsonProperty("front_default")]
                    public string frontDefault;
                    [OptionQualifiers(Options.shiny)]
                    [JsonProperty("front_shiny")]
                    public string frontShiny;
                    [OptionQualifiers(Options.back)]
                    [JsonProperty("back_default")]
                    public string backDefault;
                    [OptionQualifiers(Options.back | Options.shiny)]
                    [JsonProperty("back_shiny")]
                    public string backShiny;
                }

                [DisplayName("Emerald", "e")]
                [JsonProperty("emerald")]
                public Emerald emerald { set; get; }
                [DisplayName("Fire Red & Leaf Green", "frlg")]
                [JsonProperty("firered-leafgreen")]
                public RSEFRLG fireRedLeafGreen { set; get; }
                [DisplayName("Ruby & Sapphire", "rs")]
                [JsonProperty("ruby-sapphire")]
                public RSEFRLG rubySapphie { set; get; }
            }

            public class Generation4
            {
                public class Gen4Game : SpriteCategory
                {
                    [OptionQualifiers(Options.back)]
                    [JsonProperty("back_default")]
                    public string backDefault;
                    [OptionQualifiers(Options.back | Options.female)]
                    [JsonProperty("back_female")]
                    public string backFemale;
                    [OptionQualifiers(Options.back | Options.shiny)]
                    [JsonProperty("back_shiny")]
                    public string backShiny;
                    [OptionQualifiers(Options.back | Options.shiny | Options.female)]
                    [JsonProperty("back_shiny_female")]
                    public string backShinyFemale;
                    [OptionQualifiers(0)]
                    [JsonProperty("front_default")]
                    public string frontDefault;
                    [OptionQualifiers(Options.female)]
                    [JsonProperty("front_female")]
                    public string frontFemale;
                    [OptionQualifiers(Options.shiny)]
                    [JsonProperty("front_shiny")]
                    public string frontShiny;
                    [OptionQualifiers(Options.shiny | Options.female)]
                    [JsonProperty("front_shiny_female")]
                    public string frontShinyFemale;
                }

                [DisplayName("Diamond & Pearl", "dp")]
                [JsonProperty("diamond-pearl")]
                public Gen4Game diamondPearl { set; get; }
                [DisplayName("Heart Gold & Soul Silver")]
                [JsonProperty("heartgold-soulsilver")]
                public Gen4Game heartGoldSoulSilver { set; get; }
                [DisplayName("Platinum", "pt")]
                [JsonProperty("platinum")]
                public Gen4Game platinum { set; get; }
            }

            public class Generation5
            {
                public class Gen5Game : SpriteCategory
                {
                    public class Animated
                    {
                        [OptionQualifiers(Options.animated | Options.back)]
                        [JsonProperty("back_default")]
                        public string backDefault;
                        [OptionQualifiers(Options.animated | Options.back | Options.female)]
                        [JsonProperty("back_female")]
                        public string backFemale;
                        [OptionQualifiers(Options.animated | Options.back | Options.shiny)]
                        [JsonProperty("back_shiny")]
                        public string backShiny;
                        [OptionQualifiers(Options.animated | Options.back | Options.shiny | Options.female)]
                        [JsonProperty("back_shiny_female")]
                        public string backShinyFemale;
                        [OptionQualifiers(Options.animated)]
                        [JsonProperty("front_default")]
                        public string frontDefault;
                        [OptionQualifiers(Options.animated | Options.female)]
                        [JsonProperty("front_female")]
                        public string frontFemale;
                        [OptionQualifiers(Options.animated | Options.shiny)]
                        [JsonProperty("front_shiny")]
                        public string frontShiny;
                        [OptionQualifiers(Options.animated | Options.shiny | Options.female)]
                        [JsonProperty("front_shiny_female")]
                        public string frontShinyFemale;
                    }

                    [JsonProperty("animated")]
                    public Animated animated;

                    [OptionQualifiers(Options.back)]
                    [JsonProperty("back_default")]
                    public string backDefault;
                    [OptionQualifiers(Options.back | Options.female)]
                    [JsonProperty("back_female")]
                    public string backFemale;
                    [OptionQualifiers(Options.back | Options.shiny)]
                    [JsonProperty("back_shiny")]
                    public string backShiny;
                    [OptionQualifiers(Options.back | Options.shiny | Options.female)]
                    [JsonProperty("back_shiny_female")]
                    public string backShinyFemale;
                    [OptionQualifiers(0)]
                    [JsonProperty("front_default")]
                    public string frontDefault;
                    [OptionQualifiers(Options.female)]
                    [JsonProperty("front_female")]
                    public string frontFemale;
                    [OptionQualifiers(Options.shiny)]
                    [JsonProperty("front_shiny")]
                    public string frontShiny;
                    [OptionQualifiers(Options.shiny | Options.female)]
                    [JsonProperty("front_shiny_female")]
                    public string frontShinyFemale;
                }

                [DisplayName("Black & White", "bw")]
                [JsonProperty("black-white")]
                public Gen5Game blackWhite { set; get; }
            }

            public class Generation6
            {
                public class Gen6Game : SpriteCategory
                {
                    [OptionQualifiers(0)]
                    [JsonProperty("front_default")]
                    public string frontDefault;
                    [OptionQualifiers(Options.female)]
                    [JsonProperty("front_female")]
                    public string frontFemale;
                    [OptionQualifiers(Options.shiny)]
                    [JsonProperty("front_shiny")]
                    public string frontShiny;
                    [OptionQualifiers(Options.female | Options.shiny)]
                    [JsonProperty("front_shiny_female")]
                    public string frontShinyFemale;
                }

                [DisplayName("Omega Ruby & Alpha Sapphire", "oras")]
                [JsonProperty("omegaruby-alphasapphire")]
                public Gen6Game omegaRubyAlphaSapphire { set; get; }
                [DisplayName("X & Y", "xy")]
                [JsonProperty("x-y")]
                public Gen6Game XY { set; get; }
            }

            public class Generation7
            {
                public class Gen7IconSet
                {
                    [JsonProperty("front_default")]
                    [OptionQualifiers(Options.icon)]
                    public string iconFrontDefault;
                    [JsonProperty("front_female")]
                    [OptionQualifiers(Options.female | Options.icon)]
                    public string iconFrontFemale;
                }

                public class Gen7Game : SpriteCategory
                {
                    [OptionQualifiers(0)]
                    [JsonProperty("front_default")]
                    public string frontDefault;
                    [OptionQualifiers(Options.female)]
                    [JsonProperty("front_female")]
                    public string frontFemale;
                    [OptionQualifiers(Options.shiny)]
                    [JsonProperty("front_shiny")]
                    public string frontShiny;
                    [OptionQualifiers(Options.female | Options.shiny)]
                    [JsonProperty("front_shiny_female")]
                    public string frontShinyFemale;

                    [JsonIgnore]
                    [OptionQualifiers(Options.icon)]
                    public string iconFrontDefault;
                    [JsonIgnore]
                    [OptionQualifiers(Options.female | Options.icon)]
                    public string iconFrontFemale;
                }

                [JsonProperty("icons")]
                public Gen7IconSet icons;

                [JsonIgnore]
                private Gen7Game ultraSunUltraMoon_internal;
                [DisplayName("Ultra Sun & Ultra Moon", "usum")]
                [JsonProperty("ultra-sun-ultra-moon")]
                public Gen7Game ultraSunUltraMoon
                {
                    set
                    {
                        ultraSunUltraMoon_internal = value;
                    }
                    get
                    {
                        if (ultraSunUltraMoon_internal != null && icons != null)
                        {
                            ultraSunUltraMoon_internal.iconFrontDefault = icons.iconFrontDefault;
                            ultraSunUltraMoon_internal.iconFrontFemale = icons.iconFrontFemale;
                        }
                        return ultraSunUltraMoon_internal;
                    }
                }

                public Generation7 GetClone()
                {
                    return MemberwiseClone() as Generation7;
                }
            }

            public class Generation8
            {
                public Generation8(Generation7 sourceGeneration)
                {
                    swordShield = sourceGeneration.GetClone().ultraSunUltraMoon;
                }

                [JsonProperty("icons")]
                public Generation7.Gen7IconSet icons;

                [JsonIgnore]
                private Generation7.Gen7Game swordShield_internal;
                [DisplayName("Sword & Shield", "swsh")]
                public Generation7.Gen7Game swordShield
                {
                    set
                    {
                        swordShield_internal = value;
                    }
                    get
                    {
                        if (swordShield_internal != null && icons != null)
                        {
                            swordShield_internal.iconFrontDefault = icons.iconFrontDefault;
                            swordShield_internal.iconFrontFemale = icons.iconFrontFemale;
                        }
                        return swordShield_internal;
                    }
                }
            }

            [JsonProperty("generation-i")]
            public Generation1 generation1 { set; get; }

            [JsonProperty("generation-ii")]
            public Generation2 generation2 { set; get; }

            [JsonProperty("generation-iii")]
            public Generation3 generation3 { set; get; }

            [JsonProperty("generation-iv")]
            public Generation4 generation4 { set; get; }

            [JsonProperty("generation-v")]
            public Generation5 generation5 { set; get; }

            [JsonProperty("generation-vi")]
            public Generation6 generation6 { set; get; }

            [JsonProperty("generation-vii")]
            public Generation7 generation7 { set; get; }

            public Generation8 generation8 => new Generation8(generation7);
        }

        public class Generation9
        {
            public class ScarletViolet : SpriteCategory
            {
                [OptionQualifiers(Options.back)]
                public string backDefault;
                [OptionQualifiers(Options.back | Options.female)]
                public string backFemale;
                [OptionQualifiers(Options.back | Options.shiny)]
                public string backShiny;
                [OptionQualifiers(Options.back | Options.shiny | Options.female)]
                public string backShinyFemale;
                [OptionQualifiers(0)]
                public string frontDefault;
                [OptionQualifiers(Options.female)]
                public string frontFemale;
                [OptionQualifiers(Options.shiny)]
                public string frontShiny;
                [OptionQualifiers(Options.shiny | Options.female)]
                public string frontShinyFemale;
            }

            [DisplayName("Latest", "sv")]
            public ScarletViolet scarletViolet { get; set; }
        }

        public class Other
        {
            public class DreamWorld : SpriteCategory
            {
                [OptionQualifiers(0)]
                [JsonProperty("front_default")]
                public string frontDefault;
                [OptionQualifiers(Options.female)]
                [JsonProperty("front_female")]
                public string frontFemale;
            }

            public class Home : SpriteCategory
            {
                [OptionQualifiers(0)]
                [JsonProperty("front_default")]
                public string frontDefault;
                [OptionQualifiers(Options.female)]
                [JsonProperty("front_female")]
                public string frontFemale;
                [OptionQualifiers(Options.shiny)]
                [JsonProperty("front_shiny")]
                public string frontShiny;
                [OptionQualifiers(Options.female | Options.shiny)]
                [JsonProperty("front_shiny_female")]
                public string frontShinyFemale;
            }

            public class Official : SpriteCategory
            {
                [OptionQualifiers(0)]
                [JsonProperty("front_default")]
                public string frontDefault;
                [OptionQualifiers(Options.shiny)]
                [JsonProperty("front_shiny")]
                public string frontShiny;
            }

            [DisplayName("Dream World", "dw")]
            [JsonProperty("dream_world")]
            public DreamWorld dreamWorld { get; set; }
            [DisplayName("Home", "home")]
            [JsonProperty("home")]
            public Home home { get; set; }
            [DisplayName("Official Artwork", "official")]
            [JsonProperty("official-artwork")]
            public Official official { get; set; }
        }


        public Generation9 generation9 => new Generation9()
        {
            scarletViolet = new Generation9.ScarletViolet()
            {
                backFemale = backFemale,
                backShinyFemale = backShinyFemale,
                backDefault = backDefault,
                frontFemale = frontFemale,
                frontShinyFemale = frontShinyFemale,
                backShiny = backShiny,
                frontDefault = frontDefault,
                frontShiny = frontShiny
            }
        };

        [JsonProperty("back_female")]
        public string backFemale;
        [JsonProperty("back_shiny_female")]
        public string backShinyFemale;
        [JsonProperty("back_default")]
        public string backDefault;
        [JsonProperty("front_female")]
        public string frontFemale;
        [JsonProperty("front_shiny_female")]
        public string frontShinyFemale;
        [JsonProperty("back_shiny")]
        public string backShiny;
        [JsonProperty("front_default")]
        public string frontDefault;
        [JsonProperty("front_shiny")]
        public string frontShiny;

        [JsonProperty("other")]
        public Other other;

        [JsonProperty("versions")]
        public Versions versions;

    }

    public class PokemonForm : IDisplayName
    {
        public class FormSpriteCategory : SpriteCategory
        {
            [JsonIgnore]
            public bool isBaseForm = false;

            [OptionQualifiers(Options.back)]
            [JsonProperty("back_default")]
            public string backDefault;
            [OptionQualifiers(Options.back | Options.female)]
            [JsonProperty("back_female")]
            public string backFemale;
            [OptionQualifiers(Options.back | Options.shiny)]
            [JsonProperty("back_shiny")]
            public string backShiny;
            [OptionQualifiers(Options.back | Options.shiny | Options.female)]
            [JsonProperty("back_shiny_female")]
            public string backShinyFemale;
            [OptionQualifiers(0)]
            [JsonProperty("front_default")]
            public string frontDefault;
            [OptionQualifiers(Options.female)]
            [JsonProperty("front_female")]
            public string frontFemale;
            [OptionQualifiers(Options.shiny)]
            [JsonProperty("front_shiny")]
            public string frontShiny;
            [OptionQualifiers(Options.shiny | Options.female)]
            [JsonProperty("front_shiny_female")]
            public string frontShinyFemale;
        }

        [JsonProperty("sprites")]
        public FormSpriteCategory sprites;

        [JsonProperty("form_name")]
        public string formName;

        [JsonProperty("name")]
        public string name;


        public string GetName()
        {
            return formName;
        }

        public string GetUniqueIdentifier()
        {
            return name;
        }
    }

    public class PokemonSpecies
    {
        public class Variety
        {
            [JsonProperty("is_default")]
            public bool isDefault;
            [JsonProperty("pokemon")]
            public Pokemon.SpeciesInfo pokemon;
        }

        [JsonProperty("varieties")]
        public List<Variety> varieties;
    }

    public class Pokemon
    {
        public override string ToString()
        {
            return name;
        }

        public class FormInfo
        {
            [JsonProperty("name")]
            public string name;
            [JsonProperty("url")]
            public string url;
        }
        public class SpeciesInfo
        {
            [JsonProperty("name")]
            public string name;
            [JsonProperty("url")]
            public string url;
        }

        [JsonProperty("name")]
        public string name;

        [JsonProperty("forms")]
        public List<FormInfo> FormInfos;

        [JsonProperty("species")]
        public SpeciesInfo species;

        [JsonProperty("sprites")]
        public PokemonSprites Sprites;
    }

    public class PokemonInfo
    {
        public override string ToString() => name;

        [JsonProperty("name")]
        public string name;
        [JsonProperty("url")]
        public string url;
    }

    public class PokemonInfoList
    {
        [JsonProperty("count")]
        public int count;
        [JsonProperty("results")]
        public List<PokemonInfo> pokemon;
    }
}

// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Style", "IDE0090:Use 'new(...)'", Justification = "I prefer using explicit newing for clarity, plus it looks better")]
[assembly: SuppressMessage("Style", "IDE0017:Simplify object initialization", Justification = "I prefer using explicit newing for clarity, plus it looks better")]
[assembly: SuppressMessage("Style", "IDE1006:Naming Styles", Scope = "type", Target = "~T:PokeCounter.PokemonSprites", Justification = "A lot of the names conflict with class types, and I prefer them as properties because one of them needs to be one, so I'd rather have all of them be properties")]

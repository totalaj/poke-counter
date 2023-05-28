using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PokeCounter.ArgumentParser;

namespace PokeCounter
{
    public abstract class ArgumentParser
    {
        public abstract class BaseParsedArgument
        {
            public abstract bool IdentifyArgument(string arg);
            public abstract bool Parse(string[] args, ref int index);
        }

        public abstract class ParsedArgument<T> : BaseParsedArgument
        {
            public delegate bool Parser(string argument, out T data);

            public ParsedArgument(string identifier)
            {
                this.identifier = identifier;
            }

            public ParsedArgument(string identifier, Parser parser)
            {
                this.identifier = identifier;
                this.parser = parser;
            }

            public static implicit operator T(ParsedArgument<T> parsedArgument)
            {
                return parsedArgument.value;
            }

            protected Parser parser;
            protected string identifier;
            protected T value;
            public bool exists;

            public override bool IdentifyArgument(string arg)
            {
                return arg.Trim().ToLowerInvariant().Contains(identifier.ToLowerInvariant());
            }

            public override bool Parse(string[] args, ref int index)
            {
                exists = true;
                if (args.Length > index + 1)
                {
                    index++;
                    return parser(args[index], out value);
                }
                return false;
            }
        }

        /// <summary>
        /// Is true if identifier is found, false per default
        /// </summary>
        public class BoolArgument : ParsedArgument<bool>
        {
            public BoolArgument(string identifier, bool defaultValue = false) : base(identifier)
            { value = defaultValue; }

            public override bool Parse(string[] args, ref int index)
            {
                exists = true;
                return value = true;
            }

        }

        public class IntArgument : ParsedArgument<int>
        {
            public IntArgument(string identifier, int defaultValue = 0) : base(identifier,
                (string argument, out int value) => int.TryParse(argument, out value))
            { value = defaultValue; }
        }

        public class DoubleArgument : ParsedArgument<double>
        {
            public DoubleArgument(string identifier, double defaultValue = 0) : base(identifier,
                (string argument, out double value) => double.TryParse(argument, out value))
            { value = defaultValue; }
        }

        public class StringArgument : ParsedArgument<string>
        {
            public StringArgument(string identifier, string defaultValue = "") : base(identifier,
                (string argument, out string value) =>
                {
                    value = argument;
                    return true;
                })
            { value = defaultValue; }
        }

        public class FileArgument : ParsedArgument<string>
        {
            public FileArgument() : base("") { }

            public override bool IdentifyArgument(string arg)
            {
                return File.Exists(arg);
            }

            public override bool Parse(string[] args, ref int index)
            {
                exists = true;
                value = args[index];
                return true;
            }
        }

        public ArgumentParser(string[] args)
        {
            var properties = GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            for (int argIndex = 0; argIndex < args.Length; argIndex++)
            {
                foreach (var property in properties)
                {
                    if (property.GetValue(this) is BaseParsedArgument parser)
                    {
                        if (parser.IdentifyArgument(args[argIndex]))
                        {
                            parser.Parse(args, ref argIndex);
                            break;
                        }
                    }
                }
            }
        }
    }
}

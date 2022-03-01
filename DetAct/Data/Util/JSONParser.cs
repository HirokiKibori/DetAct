using System.Collections.Immutable;

using Pidgin;

using static Pidgin.Parser;
using static Pidgin.Parser<char>;

namespace DetAct.Data.Util
{
    /// <summary>
    /// Simple JSON-parser.
    /// <seealso cref="https://www.json.org/json-en.html"/>
    /// </summary>
    public class JSONParser
    {
        /// <summary>
        /// base-parsers for symbols (like the tutorial)
        /// </summary>
        #region skip-whitespace-symbols
        private static Parser<char, T> Symbol<T>(Parser<char, T> parser)
            => parser.Before(SkipWhitespaces);
        private static Parser<char, char> Symbol(char value)
           => Symbol(Char(value));
        private static Parser<char, string> Symbol(string value)
            => Symbol(String(value));
        #endregion

        #region block-symbols
        private static readonly Parser<char, char> _start_object_symbol = Symbol('{');
        private static readonly Parser<char, char> _end_object_symbol = Symbol('}');

        private static readonly Parser<char, char> _start_array_symbol = Symbol('[');
        private static readonly Parser<char, char> _end_array_symbol = Symbol(']');
        #endregion

        #region single-symbols
        private static readonly Parser<char, char> _property_split_symbol = Symbol(',');
        private static readonly Parser<char, char> _property_value_symbol = Symbol(':');

        private static readonly Parser<char, char> _array_split_symbol = Symbol(',');

        private static readonly Parser<char, char> _second_quote_symbol = Symbol('"');
        private static readonly Parser<char, string> _escaped_second_quote_symbol = Symbol("\"");
        private static readonly Parser<char, char> _sign_negative_symbol = Symbol('-');
        private static readonly Parser<char, char> _sign_positive_symbol = Symbol('+');
        private static readonly Parser<char, char> _dot_symbol = Symbol('.');
        private static readonly Parser<char, char> _exponent_symbol = Symbol('e');
        private static readonly Parser<char, char> _exponent_caps_symbol = Symbol('E');

        private static readonly Parser<char, char> _whitespace_space_symbol = Symbol((char)32);
        private static readonly Parser<char, char> _whitespace_linefeed_symbol = Symbol((char)10);
        private static readonly Parser<char, char> _whitespace_carriage_return_symbol = Symbol((char)13);
        private static readonly Parser<char, char> _whitespace_horizontal_tab_symbol = Symbol((char)9);

        private static readonly Parser<char, Value> _true_symbol = Symbol("true").ThenReturn(new Text("true") as Value);
        private static readonly Parser<char, Value> _false_symbol = Symbol("false").ThenReturn(new Text("false") as Value);
        private static readonly Parser<char, Value> _null_symbol = Symbol("null").ThenReturn(new Text("null") as Value);
        #endregion

        private static Parser<char, char> _whitespaces
            = OneOf(
                _whitespace_space_symbol,
                _whitespace_linefeed_symbol,
                _whitespace_carriage_return_symbol,
                _whitespace_horizontal_tab_symbol
                );

        private static readonly Parser<char, string> _any
            = from @char in Any
              select @char.ToString();

        private static readonly Parser<char, string> _text
            = _second_quote_symbol
            .Then(OneOf(
                _escaped_second_quote_symbol,
                _any)
                .Until(_second_quote_symbol)
                ).Select(cs => string.Concat(cs));

        // More than only Numbers but currently enough
        private static readonly Parser<char, string> _number
            = from start in OneOf(_sign_negative_symbol, Digit)
              from rest in OneOf(Digit,
                  _sign_negative_symbol,
                  _sign_positive_symbol,
                  _dot_symbol,
                  _exponent_symbol,
                  _exponent_caps_symbol).Many()
              select $"{start}{string.Concat(rest)}";

        private static readonly Parser<char, Value> _array
            = from _ in _start_array_symbol
              from items in Items(_item).Before(_end_array_symbol)
              select new Array(items) as Value;

        private static readonly Parser<char, Value> _value
           = Rec(() => _whitespaces.SkipMany()
           .Then(OneOf(
               _text.Select(text => new Text(text) as Value),
               _number.Select(number => new Text(number) as Value),
               _object.Cast<Value>(),
               _array,
               _true_symbol,
               _false_symbol,
               _null_symbol
               ))
           .Before(_whitespaces.SkipMany()));

        private static Parser<char, ImmutableArray<T>> Items<T>(Parser<char, T> items)
           => items.Separated(_array_split_symbol).Select(x => x.ToImmutableArray());

        private static readonly Parser<char, Value> _item
            = from _ in _whitespaces.SkipMany()
              from item in _value
              select item;

        private static Parser<char, ImmutableArray<T>> Properties<T>(Parser<char, T> properties)
           => properties.Separated(_property_split_symbol).Select(x => x.ToImmutableArray());

        private static readonly Parser<char, Property> _property
            = from name in _whitespaces.SkipMany().Then(_text).Before(_whitespaces.SkipMany())
              from value in _property_value_symbol.Then(_value)
              select new Property(name, value);

        private static readonly Parser<char, JSONObject> _object
            = _start_object_symbol
            .Then(Properties(_property))
            .Before(_end_object_symbol)
            .Select(@object => new JSONObject(@object));

        public static JSONObject ParseProgram(string input) => _object.ParseOrThrow(input);
    }

    public abstract class Value { }

    public class Property
    {
        public string Name { get; }

        public Value Value { get; }

        public Property(string name, Value value) => (Name, Value) = (name, value);
    }

    public class JSONObject : Value
    {
        public ImmutableArray<Property> Properties { get; }

        public JSONObject(ImmutableArray<Property> properties) => Properties = properties;
    }

    public class Text : Value
    {
        public string Content { get; }

        public Text(string text) => Content = text;
    }

    public class Array : Value
    {
        public ImmutableArray<Value> Items { get; }

        public Array(ImmutableArray<Value> items) => Items = items;
    }
}

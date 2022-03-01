using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using DetAct.Behaviour;

namespace DetAct.Data.Model
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MessageType
    {
        BEHAVIOUR,
        CONTROL,
        BLACKBOARD,
        ERROR,

        INDEFINITE
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum BehaviourType
    {
        CONDITION = 0,
        ACTION = 1
    }

    [JsonConverter(typeof(DetActMessageJsonConverter))]
    public class DetActMessage
    {
        private static readonly Random RANDOMIZER = new();
        private MessageContent _content;

        public int ID { get; set; }

        public MessageType Type { get; set; }

        public MessageContent Content {
            get => _content;
            set => _content = value as MessageContent ?? throw new ArgumentNullException(paramName: nameof(Content));
        }

        public DetActMessage(MessageContent content)
        {
            ID = RANDOMIZER.Next();
            Content = content;

            Type = content switch
            {
                Behaviour => MessageType.BEHAVIOUR,
                Control => MessageType.CONTROL,
                Blackboard => MessageType.BLACKBOARD,
                Error => MessageType.ERROR,
                Indefinite => MessageType.INDEFINITE,
                _ => throw new ArgumentException("type of content is unknown"),
            };
        }

        public T GetContent<T>() where T : MessageContent
        {
            return _content as T;
        }

        public override string ToString()
        {
            return $"ID: {ID} | {Type}{Environment.NewLine}{Content}";
        }

        public string ConvertToJSON()
        {
            return JsonSerializer.Serialize(value: this, inputType: typeof(DetActMessage));
        }

        public byte[] ConvertToByteArray()
        {
            return Encoding.UTF8.GetBytes(ConvertToJSON());
        }

        public override bool Equals(object obj)
        {
            if(obj is null)
                return false;

            if(obj is not DetActMessage detActMessageObj)
                return false;

            return ID == detActMessageObj.ID
                && Type == detActMessageObj.Type;
        }

        public override int GetHashCode()
            => HashCode.Combine(ID, Type);

        public static bool operator ==(DetActMessage message1, DetActMessage message2)
            => message1.Equals(obj: message2);

        public static bool operator !=(DetActMessage message1, DetActMessage message2)
            => !(message1.Equals(obj: message2));
    }

    public abstract class MessageContent
    {
        private string _name;

        public string Name {
            get => _name;
            set => _name = value ?? throw new ArgumentNullException(paramName: nameof(Name));
        }

        public MessageContent() : this("") { }

        public MessageContent(string name)
            => Name = name;

        public override string ToString()
        {
            return $"{Name}:{Environment.NewLine}";
        }
    }

    public class Behaviour : MessageContent
    {
        private Dictionary<string, IEnumerable<string>> _commands;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public BehaviourStatus Status { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public BehaviourType Type { get; set; }

        public Behaviour() : this("") { }

        public Dictionary<string, IEnumerable<string>> Commands {
            get => _commands;
            set => _commands = value ?? throw new ArgumentNullException(paramName: nameof(Commands));
        }

        public Behaviour(string name, BehaviourStatus status = BehaviourStatus.INDEFINITE)
            : this(name, new(), status) { }

        public Behaviour(string name, Dictionary<string, IEnumerable<string>> commands, BehaviourStatus status = BehaviourStatus.INDEFINITE)
            : base(name)
        {
            _ = commands ?? new();

            Status = status;
            Commands = commands;
        }

        public override string ToString()
        {
            var buffer = $"{base.ToString()}Status = {Status} | Type = {Type}{Environment.NewLine}";

            Commands.ToList()
                .ForEach(command =>
                {
                    buffer += $"{command.Key}({string.Join(", ", command.Value)});{Environment.NewLine}";

                });

            return buffer.TrimEnd(Environment.NewLine.ToCharArray());
        }
    }

    public class Control : MessageContent
    {
        private Dictionary<string, string> _items;

        public Dictionary<string, string> Items {
            get => _items;
            set => _items = value ?? throw new ArgumentNullException(paramName: nameof(Items));
        }

        public Control() : this("") { }

        public Control(string name)
            : this(name, new()) { }

        public Control(string name, Dictionary<string, string> items)
            : base(name) => Items = items;

        public override string ToString()
        {
            var buffer = base.ToString();

            Items.ToList()
                .ForEach(item =>
                {
                    buffer += $"({item.Key} | {item.Value}){Environment.NewLine}";

                });

            return buffer.TrimEnd(Environment.NewLine.ToCharArray());
        }
    }

    public class Blackboard : MessageContent
    {
        private Dictionary<string, string> _items;

        public Dictionary<string, string> Items {
            get => _items;
            set => _items = value ?? throw new ArgumentNullException(paramName: nameof(Items));
        }

        public Blackboard() : this("") { }

        public Blackboard(string name)
            : this(name, new()) { }

        public Blackboard(string name, Dictionary<string, string> items)
            : base(name) => Items = items;

        public override string ToString()
        {
            var buffer = base.ToString();

            Items.ToList()
                .ForEach(item =>
                {
                    buffer += $"({item.Key} | {item.Value}){Environment.NewLine}";

                });

            return buffer.TrimEnd(Environment.NewLine.ToCharArray());
        }
    }

    public class Error : MessageContent
    {
        private string _message;

        public string Message {
            get => _message;
            set => _message = value ?? throw new ArgumentNullException(paramName: nameof(Message));
        }

        public Error() : this("") { }

        public Error(string name)
            : this(name, "") { }

        public Error(string name, string message)
            : base(name) => Message = message;

        public override string ToString()
        {
            return $"{base.ToString()}{Message}";
        }
    }

    public class Indefinite : MessageContent
    {
        public Indefinite() : this("") { }

        public Indefinite(string name)
            : base(name) { }
    }

    //reference: https://josef.codes/polymorphic-deserialization-with-system-text-json/
    public class DetActMessageJsonConverter : JsonConverter<DetActMessage>
    {
        public override bool CanConvert(Type type)
        {
            return type.IsAssignableFrom(typeof(DetActMessage));
        }

        public override DetActMessage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if(JsonDocument.TryParseValue(ref reader, out var @object)) {
                if(@object.RootElement.TryGetProperty("Type", out var type)
                    && @object.RootElement.TryGetProperty("Content", out var content)
                    && @object.RootElement.TryGetProperty("ID", out var id)) {

                    var typeValue = Enum.Parse<MessageType>(type.GetString());

                    var contentValue = content.GetRawText();
                    var idValue = id.GetInt32();

                    MessageContent contentObject = typeValue switch
                    {
                        MessageType.BEHAVIOUR => JsonSerializer.Deserialize<Behaviour>(contentValue, options),
                        MessageType.CONTROL => JsonSerializer.Deserialize<Control>(contentValue, options),
                        MessageType.BLACKBOARD => JsonSerializer.Deserialize<Blackboard>(contentValue, options),
                        MessageType.ERROR => JsonSerializer.Deserialize<Error>(contentValue, options),
                        MessageType.INDEFINITE => JsonSerializer.Deserialize<Indefinite>(contentValue, options),

                        _ => throw new JsonException("type of content is unknown")
                    };

                    DetActMessage result = new(contentObject);
                    result.ID = idValue;

                    return result;
                }

                throw new JsonException("json-object contains no 'type' attribute");
            }

            throw new JsonException("json-object could not read");
        }

        public override void Write(Utf8JsonWriter writer, DetActMessage value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteNumber("ID", value.ID);

            writer.WritePropertyName("Type");
            JsonSerializer.Serialize(writer, value.Type, options);

            writer.WritePropertyName("Content");
            JsonSerializer.Serialize(writer, value.Content as object, options);

            writer.WriteEndObject();
        }
    }
}

using SIL.Transcriber.Models;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SIL.Transcriber.Serializers
{
    public class TranscriberConverter: JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeof(BaseModel).IsAssignableFrom(typeToConvert);

        }
        public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options)
        {
            Type keyType = type.GetGenericArguments()[0];
            Type valueType = type.GetGenericArguments()[1];

            JsonConverter converter = (JsonConverter)Activator.CreateInstance(
                typeof(BaseModel).MakeGenericType(
                    new Type[] { keyType, valueType }),
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                args: new object[] { options },
                culture: null)!;

            return converter;
        }
        private class TranscriberConverterInner<TEntity> :
            JsonConverter<TEntity> where TEntity : BaseModel
        {
            private readonly JsonConverter<TEntity> _valueConverter;

            public TranscriberConverterInner(JsonSerializerOptions options)
            {
                // For performance, use the existing converter if available.
                _valueConverter = (JsonConverter<TEntity>)options.GetConverter(typeof(TEntity));
            }

            public override TEntity Read(
                            ref Utf8JsonReader reader,
                            Type typeToConvert,
                            JsonSerializerOptions options)
            {
                return JsonSerializer.Deserialize<TEntity>(ref reader, options)!;
            }

            public override void Write(
                Utf8JsonWriter writer,
                TEntity entity,
                JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                //    var propertyName = key.ToString();
                //    writer.WritePropertyName
                //        (options.PropertyNamingPolicy?.ConvertName(propertyName) ?? propertyName);

                    if (_valueConverter != null)
                    {
                        _valueConverter.Write(writer, entity, options);
                    }
                    else
                    {
                        JsonSerializer.Serialize(writer, entity, options);
                    }
                //}

                writer.WriteEndObject();
            }
        }
    }
}


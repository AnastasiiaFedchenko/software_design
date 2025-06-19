using System.Text.Json;
using System.Text.Json.Serialization;
using Domain;

public class ProductJsonConverter : JsonConverter<Product>
{
    public override Product Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
        {
            var root = doc.RootElement;
            return new Product(
                root.GetProperty("IdNomenclature").GetInt32(),
                root.GetProperty("Price").GetDouble(),
                root.GetProperty("AmountInStock").GetInt32(),
                root.GetProperty("Type").GetString(),
                root.GetProperty("Country").GetString()
            );
        }
    }

    public override void Write(Utf8JsonWriter writer, Product value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("IdNomenclature", value.IdNomenclature);
        writer.WriteNumber("Price", value.Price);
        writer.WriteNumber("AmountInStock", value.AmountInStock);
        writer.WriteString("Type", value.Type);
        writer.WriteString("Country", value.Country);
        writer.WriteEndObject();
    }
}
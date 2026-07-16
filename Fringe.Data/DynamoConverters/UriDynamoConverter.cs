using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;

namespace Fringe.Data.DynamoConverters;

/// <summary>Converts between <see cref="Uri"/> and a DynamoDB string attribute.</summary>
public sealed class UriDynamoConverter : IPropertyConverter
{
    /// <inheritdoc/>
    public DynamoDBEntry ToEntry(object value)
    {
        return value is not Uri uri ? new DynamoDBNull() : new Primitive(uri.ToString());
    }

    /// <inheritdoc/>
    public object FromEntry(DynamoDBEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        string? s = entry.AsString();
        return string.IsNullOrEmpty(s)
            ? null!
            : Uri.TryCreate(s, UriKind.Absolute, out Uri? uri) ? uri : null!;
    }
}

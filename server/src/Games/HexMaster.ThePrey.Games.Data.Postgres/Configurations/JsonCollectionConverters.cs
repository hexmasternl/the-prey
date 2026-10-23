using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HexMaster.ThePrey.Games.Data.Postgres.Configurations;

/// <summary>
/// Helpers that map a collection of immutable value objects to a single <c>jsonb</c> column. Used for the
/// penalty and location history, which is only ever loaded as part of the game aggregate and never queried
/// in SQL — serialising it avoids the constructor-binding limits of EF-owned types nested in records.
/// </summary>
internal static class JsonCollectionConverters
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    internal static ValueConverter<List<T>, string> Converter<T>() =>
        new(
            value => JsonSerializer.Serialize(value, Options),
            json => JsonSerializer.Deserialize<List<T>>(json, Options) ?? new List<T>());

    internal static ValueComparer<List<T>> Comparer<T>() =>
        new(
            (left, right) => JsonSerializer.Serialize(left, Options) == JsonSerializer.Serialize(right, Options),
            value => JsonSerializer.Serialize(value, Options).GetHashCode(),
            value => JsonSerializer.Deserialize<List<T>>(JsonSerializer.Serialize(value, Options), Options) ?? new List<T>());
}

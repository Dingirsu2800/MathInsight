using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MathInsight.Shared.Persistence;

/// <summary>
/// SQL Server has no concept of DateTimeKind, so every DateTime column comes back from a query
/// as Kind=Unspecified regardless of the fact it was written via DateTime.UtcNow. System.Text.Json
/// then serializes that as an offset-less ISO string (no trailing "Z"), and browsers parse
/// offset-less date-times as local time — silently shifting every timestamp by the client's UTC
/// offset (e.g. notifications showing as "7 hours ago" the instant they're created in UTC+7).
/// Stamping Kind=Utc back on read keeps the "Z" suffix on the wire so clients interpret the
/// instant correctly, regardless of the server's or client's local timezone.
/// </summary>
public static class UtcDateTimeModelBuilderExtensions
{
    private static readonly ValueConverter<DateTime, DateTime> UtcConverter = new(
        toProvider => toProvider,
        fromProvider => DateTime.SpecifyKind(fromProvider, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> NullableUtcConverter = new(
        toProvider => toProvider,
        fromProvider => fromProvider.HasValue ? DateTime.SpecifyKind(fromProvider.Value, DateTimeKind.Utc) : fromProvider);

    public static void ApplyUtcDateTimeConversion(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(UtcConverter);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(NullableUtcConverter);
                }
            }
        }
    }
}

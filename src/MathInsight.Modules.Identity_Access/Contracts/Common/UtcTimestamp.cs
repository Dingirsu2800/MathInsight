namespace MathInsight.Modules.Identity_Access.Contracts.Common;

/// <summary>
/// Restores the UTC marker that EF Core drops when reading a <c>datetime2</c> column.
///
/// Every timestamp in this module is written as UTC (<c>DateTime.UtcNow</c>, or the column's
/// <c>SYSUTCDATETIME()</c> default), but SQL Server's <c>datetime2</c> stores no offset, so EF
/// materialises the value with <see cref="DateTimeKind.Unspecified"/>. System.Text.Json then
/// serialises it WITHOUT a trailing "Z" — "2026-08-01T14:30:00" — and a browser parsing that
/// string treats it as local time, showing the UTC clock reading as if it were local (7 hours
/// early in Vietnam).
///
/// Stamping the kind at the DTO boundary makes the JSON say "2026-08-01T14:30:00Z", which clients
/// can convert correctly. It changes no stored value and no business rule.
/// </summary>
public static class UtcTimestamp
{
    public static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public static DateTime? AsUtc(DateTime? value) =>
        value.HasValue ? AsUtc(value.Value) : null;
}

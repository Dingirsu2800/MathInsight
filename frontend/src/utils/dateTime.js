// Vietnam-time formatting for timestamps returned by the API.
//
// The backend stores every timestamp in UTC (DateTime.UtcNow / SYSUTCDATETIME()). Two things can
// go wrong on the way to the screen, and this module handles both:
//
//  1. PARSING. SQL Server's datetime2 carries no offset, so EF materialises DateTime values with
//     Kind=Unspecified and System.Text.Json writes them WITHOUT a "Z" ("2026-08-01T14:30:00").
//     `new Date(...)` reads a string like that as LOCAL time, so the UTC clock reading is shown
//     as if it were local — 7 hours early in Vietnam. ParseUtcDate treats a missing marker as UTC.
//     (Identity_Access now stamps the kind server-side, so its payloads arrive with the "Z";
//     the fallback stays for endpoints that have not been fixed yet.)
//
//  2. RENDERING. toLocaleString("vi-VN") only sets the LANGUAGE — the time zone still comes from
//     the viewer's machine, so an admin travelling abroad would see a different time than the
//     teacher. Pinning timeZone to Asia/Ho_Chi_Minh makes the same instant render identically for
//     everyone, which is what the two application screens must agree on.
//
// No offset arithmetic anywhere: Intl applies the zone, so there is no "+7" to drift or to
// double-apply if the API later starts sending an explicit offset.

export const VIETNAM_TIME_ZONE = "Asia/Ho_Chi_Minh";

const VIETNAM_DATE_TIME_FORMATTER = new Intl.DateTimeFormat("vi-VN", {
  timeZone: VIETNAM_TIME_ZONE,
  year: "numeric",
  month: "2-digit",
  day: "2-digit",
  hour: "2-digit",
  minute: "2-digit",
  hour12: false,
});

/**
 * Parses an API timestamp as UTC. A value that already carries a marker ("Z" or a ±hh:mm offset)
 * is left alone so an explicitly-offset timestamp is never shifted twice.
 */
export function ParseUtcDate(value) {
  if (!value) return null;
  if (value instanceof Date) return Number.isNaN(value.getTime()) ? null : value;

  const text = String(value).trim();
  if (text.length === 0) return null;

  // Matches a trailing "Z" or "+07:00" / "-0500" style offset on the time portion.
  const hasTimeZone = /(?:Z|[+-]\d{2}:?\d{2})$/i.test(text);
  const parsed = new Date(hasTimeZone ? text : `${text}Z`);

  return Number.isNaN(parsed.getTime()) ? null : parsed;
}

/**
 * "HH:mm dd/MM/yyyy" in Vietnam time — e.g. "14:30 01/08/2026".
 * Returns `fallback` for a missing or unparseable value.
 */
export function FormatVietnamDateTime(value, fallback = "-") {
  const date = ParseUtcDate(value);
  if (!date) return fallback;

  // formatToParts rather than format(), so the field order is ours and not the locale's.
  const parts = VIETNAM_DATE_TIME_FORMATTER.formatToParts(date).reduce((accumulator, part) => {
    accumulator[part.type] = part.value;
    return accumulator;
  }, {});

  return `${parts.hour}:${parts.minute} ${parts.day}/${parts.month}/${parts.year}`;
}

/** "dd/MM/yyyy" in Vietnam time, for places that show a date without a clock time. */
export function FormatVietnamDate(value, fallback = "-") {
  const date = ParseUtcDate(value);
  if (!date) return fallback;

  return date.toLocaleDateString("vi-VN", {
    timeZone: VIETNAM_TIME_ZONE,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  });
}

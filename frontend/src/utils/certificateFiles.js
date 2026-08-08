// Teacher certificate file rules and type detection (BR-05).
//
// Mirrors BlobCertificateStorage on the backend: JPG, PNG, PDF, DOC and DOCX, 10 MB per file.
// Shared by the upload picker (registration + resubmit) and the Admin review screen so the three
// cannot disagree about what is accepted or how a file is rendered.

export const CERT_MAX_FILES = 6;
export const CERT_MAX_BYTES = 10 * 1024 * 1024;

// `accept` for <input type="file">. Extensions are listed alongside the MIME types because
// Windows browsers frequently report .doc/.docx as application/octet-stream.
export const CERT_ACCEPT =
  "image/jpeg,image/png,application/pdf,application/msword," +
  "application/vnd.openxmlformats-officedocument.wordprocessingml.document," +
  ".jpg,.jpeg,.png,.pdf,.doc,.docx";

export const CERT_IMAGE_CONTENT_TYPES = ["image/jpeg", "image/png"];

const CERT_DOCUMENT_CONTENT_TYPES = [
  "application/pdf",
  "application/msword",
  "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
];

const CERT_IMAGE_EXTENSIONS = ["jpg", "jpeg", "png"];
const CERT_PDF_EXTENSIONS = ["pdf"];
const CERT_WORD_EXTENSIONS = ["doc", "docx"];

export const CERT_TYPE_ERROR = "Chứng chỉ phải là file JPG, PNG, PDF hoặc Word.";
export const CERT_SIZE_ERROR = "Mỗi chứng chỉ không được vượt quá 10MB.";
export const CERT_COUNT_ERROR = `Chỉ được tải lên tối đa ${CERT_MAX_FILES} file chứng chỉ.`;
export const CERT_REQUIRED_ERROR =
  "Vui lòng tải lên ít nhất một chứng chỉ giảng dạy (JPG, PNG, PDF hoặc Word).";
export const CERT_KEEP_REQUIRED_ERROR =
  "Vui lòng giữ lại hoặc tải lên ít nhất một chứng chỉ giảng dạy (JPG, PNG, PDF hoặc Word).";
export const CERT_HELPER_TEXT =
  `Chấp nhận JPG/PNG/PDF/Word, tối đa 10MB mỗi file (tối đa ${CERT_MAX_FILES} file).`;

// Lowercased extension without the dot. Query strings and fragments are stripped so a signed or
// versioned Cloudinary URL still resolves.
export function GetFileExtension(nameOrUrl) {
  const value = String(nameOrUrl || "").split(/[?#]/)[0];
  const lastSegment = value.split("/").pop() || "";
  const dotIndex = lastSegment.lastIndexOf(".");

  if (dotIndex <= 0 || dotIndex === lastSegment.length - 1) return "";
  return lastSegment.slice(dotIndex + 1).toLowerCase();
}

// "image" | "pdf" | "word" | "other". Type is derived from the URL/filename extension, which is why
// the backend appends the original extension to the public id for non-image uploads.
export function GetCertificateKind(nameOrUrl) {
  const extension = GetFileExtension(nameOrUrl);

  if (CERT_IMAGE_EXTENSIONS.includes(extension)) return "image";
  if (CERT_PDF_EXTENSIONS.includes(extension)) return "pdf";
  if (CERT_WORD_EXTENSIONS.includes(extension)) return "word";
  return "other";
}

// Material Symbols glyph for a non-image certificate.
export function GetCertificateIcon(kind) {
  if (kind === "pdf") return "picture_as_pdf";
  if (kind === "word") return "description";
  return "draft";
}

export function GetCertificateKindLabel(kind) {
  if (kind === "image") return "Ảnh";
  if (kind === "pdf") return "PDF";
  if (kind === "word") return "Word";
  return "Tài liệu";
}

// A file is accepted when EITHER its content type or its extension is recognised — the same
// two-signal rule the backend applies, for the same .doc/.docx reason.
export function IsAcceptedCertificateFile(file) {
  const contentType = String(file?.type || "").toLowerCase();

  if (CERT_IMAGE_CONTENT_TYPES.includes(contentType)) return true;
  if (CERT_DOCUMENT_CONTENT_TYPES.includes(contentType)) return true;

  const extension = GetFileExtension(file?.name);
  return (
    CERT_IMAGE_EXTENSIONS.includes(extension) ||
    CERT_PDF_EXTENSIONS.includes(extension) ||
    CERT_WORD_EXTENSIONS.includes(extension)
  );
}

export function IsImageFile(file) {
  const contentType = String(file?.type || "").toLowerCase();
  if (CERT_IMAGE_CONTENT_TYPES.includes(contentType)) return true;
  return GetCertificateKind(file?.name) === "image";
}

// Certificates uploaded before the storage fix were sent with an RFC 2047 encoded filename
// (=?utf-8?B?<base64>?=, which is how .NET encodes a non-ASCII multipart filename). Cloudinary
// sanitised the punctuation, so those URLs carry "utf-8_B_<base64>" instead of a readable name.
// Best effort only: the sanitising is lossy for base64 that contained + / or =, so a decode that
// does not come back as clean text falls through to the raw segment.
const RFC2047_PATTERN = /^=?\??utf-8[?_]B[?_](.+?)[?_]?=?$/i;

function TryDecodeEncodedWord(segment) {
  const match = RFC2047_PATTERN.exec(segment);
  if (!match) return null;

  try {
    // Cloudinary strips the '=' padding, which atob rejects — restore it before decoding.
    const raw = match[1];
    const padded = raw + "=".repeat((4 - (raw.length % 4)) % 4);
    const binary = atob(padded);
    const bytes = Uint8Array.from(binary, (character) => character.charCodeAt(0));
    const decoded = new TextDecoder("utf-8", { fatal: true }).decode(bytes);
    return decoded.trim() || null;
  } catch {
    // Not recoverable — the sanitising destroyed the padding or an alphabet character.
    return null;
  }
}

// Last path segment of a URL, used as a display name for an already-stored certificate.
export function GetUrlFileName(url) {
  const value = String(url || "").split(/[?#]/)[0];
  const parts = value.split("/");
  const segment = parts[parts.length - 1] || value;

  let readable = segment;
  try {
    // Cloudinary percent-encodes non-ASCII public ids in the delivery URL.
    readable = decodeURIComponent(segment);
  } catch {
    // Malformed escape sequence — keep the raw segment.
  }

  return TryDecodeEncodedWord(readable) ?? readable;
}

export function FormatFileSize(bytes) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

// Same name + size + mtime is treated as the same file, so re-picking is idempotent.
export function ToCertificateId(file) {
  return `${file.name}-${file.size}-${file.lastModified}`;
}

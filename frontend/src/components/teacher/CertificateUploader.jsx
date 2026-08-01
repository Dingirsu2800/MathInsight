import * as React from "react";

// Certificate constraints (BR-05): JPG/PNG only, ≤ 10 MB per file, at most 6 images.
// Mirrors TeacherRegisterRequest / UpdateMyApplicationRequest on the backend.
export const CERT_ACCEPT = "image/jpeg,image/png";
export const CERT_ALLOWED_TYPES = ["image/jpeg", "image/png"];
export const CERT_MAX_BYTES = 10 * 1024 * 1024;
export const CERT_MAX_FILES = 6;

export const CERT_TYPE_ERROR = "Chứng chỉ phải là ảnh JPG hoặc PNG.";
export const CERT_SIZE_ERROR = "Mỗi chứng chỉ không được vượt quá 10MB.";
export const CERT_COUNT_ERROR = `Chỉ được tải lên tối đa ${CERT_MAX_FILES} ảnh chứng chỉ.`;
export const CERT_REQUIRED_ERROR =
  "Vui lòng giữ lại hoặc tải lên ít nhất một chứng chỉ giảng dạy (JPG hoặc PNG).";

// Same name + size + mtime is treated as the same file, so re-picking is idempotent.
export function ToCertificateId(file) {
  return `${file.name}-${file.size}-${file.lastModified}`;
}

export function FormatFileSize(bytes) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

// Last path segment of a Cloudinary URL, used as a display name for an already-stored image.
function ToUrlLabel(url) {
  try {
    const parts = String(url).split("/");
    return parts[parts.length - 1] || url;
  } catch {
    return url;
  }
}

/**
 * Multi-image certificate picker shared by the teacher application screens.
 *
 * Handles BOTH sides of an edit: `existingUrls` are images already stored on the application
 * (removable, never re-uploaded) and `files` are newly picked File objects (uploaded on save).
 * The parent owns both lists so it can send "kept URLs + new files" in one multipart request.
 */
export default function CertificateUploader({
  existingUrls = [],
  onRemoveExisting,
  files = [],
  onFilesChange,
  error,
  disabled = false,
  inputId = "certificates",
}) {
  const totalCount = existingUrls.length + files.length;

  // Release every preview object URL on unmount. The ref keeps this effect from re-running on
  // each list change, which would revoke URLs that are still on screen.
  const filesRef = React.useRef(files);
  React.useEffect(() => {
    filesRef.current = files;
  }, [files]);
  React.useEffect(() => {
    return () => {
      filesRef.current.forEach((item) => URL.revokeObjectURL(item.previewUrl));
    };
  }, []);

  const handleChange = (e) => {
    const selected = Array.from(e.target.files ?? []);
    // Clear the input so removing a file and re-picking it still fires onChange.
    e.target.value = "";

    if (selected.length === 0) return;

    // Per-file rejection (BR-05) — valid files in the same batch are still kept.
    const existingIds = new Set(files.map((item) => item.id));
    const added = [];
    const rejectedType = [];
    const rejectedSize = [];
    let rejectedCount = false;
    let slotsLeft = CERT_MAX_FILES - totalCount;

    selected.forEach((file) => {
      if (!CERT_ALLOWED_TYPES.includes(file.type)) {
        rejectedType.push(file.name);
        return;
      }
      if (file.size > CERT_MAX_BYTES) {
        rejectedSize.push(file.name);
        return;
      }

      const id = ToCertificateId(file);
      if (existingIds.has(id)) return;

      if (slotsLeft <= 0) {
        rejectedCount = true;
        return;
      }

      existingIds.add(id);
      slotsLeft -= 1;
      added.push({ id, file, previewUrl: URL.createObjectURL(file) });
    });

    const messages = [];
    if (rejectedType.length > 0) messages.push(`${CERT_TYPE_ERROR} (${rejectedType.join(", ")})`);
    if (rejectedSize.length > 0) messages.push(`${CERT_SIZE_ERROR} (${rejectedSize.join(", ")})`);
    if (rejectedCount) messages.push(CERT_COUNT_ERROR);

    onFilesChange([...files, ...added], messages.length > 0 ? messages.join(" ") : undefined);
  };

  const handleRemoveFile = (id) => {
    const target = files.find((item) => item.id === id);
    if (target) URL.revokeObjectURL(target.previewUrl);

    onFilesChange(
      files.filter((item) => item.id !== id),
      undefined,
    );
  };

  return (
    <div className="space-y-1.5">
      <label htmlFor={inputId} className="block text-sm font-semibold text-[#1e2a4a]">
        Chứng chỉ giảng dạy
      </label>

      <label
        htmlFor={inputId}
        className={`flex items-center gap-3 px-4 py-3 bg-white border border-dashed border-slate-300 rounded-xl transition-all ${
          disabled
            ? "opacity-60 cursor-not-allowed"
            : "cursor-pointer hover:border-[#2f5fa8] hover:bg-[#2f5fa8]/[0.03]"
        }`}
      >
        <span className="material-symbols-outlined text-slate-400 text-[22px]">upload_file</span>
        <span className="text-sm text-slate-500 truncate">
          {totalCount > 0
            ? `Đã có ${totalCount}/${CERT_MAX_FILES} ảnh — bấm để thêm`
            : "Chọn một hoặc nhiều ảnh JPG/PNG (tối đa 10MB mỗi ảnh)"}
        </span>
        <input
          id={inputId}
          type="file"
          accept={CERT_ACCEPT}
          multiple
          onChange={handleChange}
          className="hidden"
          disabled={disabled}
        />
      </label>

      {(existingUrls.length > 0 || files.length > 0) && (
        <ul className="mt-2 space-y-2">
          {existingUrls.map((url) => (
            <li key={url} className="flex items-center gap-3 p-2 border border-slate-200 rounded-lg">
              <img
                src={url}
                alt={`Chứng chỉ ${ToUrlLabel(url)}`}
                className="w-12 h-12 object-cover rounded-lg border border-slate-200 shrink-0"
              />
              <div className="min-w-0 flex-1">
                <a
                  href={url}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-xs text-[#2f5fa8] font-medium truncate hover:underline block"
                >
                  {ToUrlLabel(url)}
                </a>
                <p className="text-xs text-slate-400">Đã tải lên trước đó</p>
              </div>
              {onRemoveExisting && (
                <button
                  type="button"
                  onClick={() => onRemoveExisting(url)}
                  className="shrink-0 text-slate-400 hover:text-deep-rose transition-colors cursor-pointer disabled:opacity-60 disabled:cursor-not-allowed"
                  aria-label={`Xóa ${ToUrlLabel(url)}`}
                  disabled={disabled}
                >
                  <span className="material-symbols-outlined text-[20px]">close</span>
                </button>
              )}
            </li>
          ))}

          {files.map((item) => (
            <li key={item.id} className="flex items-center gap-3 p-2 border border-slate-200 rounded-lg">
              <img
                src={item.previewUrl}
                alt={`Xem trước ${item.file.name}`}
                className="w-12 h-12 object-cover rounded-lg border border-slate-200 shrink-0"
              />
              <div className="min-w-0 flex-1">
                <p className="text-xs text-[#1e2a4a] font-medium truncate">{item.file.name}</p>
                <p className="text-xs text-slate-400">{FormatFileSize(item.file.size)} — ảnh mới</p>
              </div>
              <button
                type="button"
                onClick={() => handleRemoveFile(item.id)}
                className="shrink-0 text-slate-400 hover:text-deep-rose transition-colors cursor-pointer disabled:opacity-60 disabled:cursor-not-allowed"
                aria-label={`Xóa ${item.file.name}`}
                disabled={disabled}
              >
                <span className="material-symbols-outlined text-[20px]">close</span>
              </button>
            </li>
          ))}
        </ul>
      )}

      {error ? (
        <p className="text-xs text-deep-rose font-medium">{error}</p>
      ) : (
        <p className="text-xs text-slate-400">
          Có thể giữ lại ảnh cũ hoặc tải lên ảnh mới, tối đa {CERT_MAX_FILES} ảnh. Chỉ chấp nhận
          JPG/PNG, dung lượng tối đa 10MB mỗi ảnh.
        </p>
      )}
    </div>
  );
}

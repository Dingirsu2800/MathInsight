import * as React from "react";
import {
  CERT_ACCEPT,
  CERT_COUNT_ERROR,
  CERT_HELPER_TEXT,
  CERT_MAX_BYTES,
  CERT_MAX_FILES,
  CERT_SIZE_ERROR,
  CERT_TYPE_ERROR,
  FormatFileSize,
  GetCertificateIcon,
  GetCertificateKind,
  GetCertificateKindLabel,
  GetUrlFileName,
  IsAcceptedCertificateFile,
  IsImageFile,
  ToCertificateId,
} from "../../utils/certificateFiles";

// Square tile shown for a non-image file, in place of a thumbnail.
function FileTypeTile({ kind }) {
  return (
    <div className="w-12 h-12 rounded-lg border border-slate-200 bg-slate-50 flex items-center justify-center shrink-0">
      <span className="material-symbols-outlined text-slate-500 text-[24px]">
        {GetCertificateIcon(kind)}
      </span>
    </div>
  );
}

/**
 * Multi-file certificate picker shared by the teacher application screens (BR-05).
 *
 * Accepts JPG, PNG, PDF, DOC and DOCX. Handles BOTH sides of an edit: `existingUrls` are files
 * already stored on the application (removable, never re-uploaded) and `files` are newly picked
 * File objects (uploaded on save). The parent owns both lists so it can send "kept URLs + new
 * files" in one multipart request.
 *
 * Images render as a thumbnail; documents render as an icon tile plus an open/download link,
 * because a PDF or Word file cannot be shown inline.
 */
export default function CertificateUploader({
  existingUrls = [],
  onRemoveExisting,
  files = [],
  onFilesChange,
  error,
  disabled = false,
  inputId = "certificates",
  helperText = CERT_HELPER_TEXT,
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
      filesRef.current.forEach((item) => {
        if (item.previewUrl) URL.revokeObjectURL(item.previewUrl);
      });
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
      if (!IsAcceptedCertificateFile(file)) {
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
      added.push({
        id,
        file,
        // Only images get an object URL; there is nothing to preview for a PDF or Word file.
        previewUrl: IsImageFile(file) ? URL.createObjectURL(file) : null,
      });
    });

    const messages = [];
    if (rejectedType.length > 0) messages.push(`${CERT_TYPE_ERROR} (${rejectedType.join(", ")})`);
    if (rejectedSize.length > 0) messages.push(`${CERT_SIZE_ERROR} (${rejectedSize.join(", ")})`);
    if (rejectedCount) messages.push(CERT_COUNT_ERROR);

    onFilesChange([...files, ...added], messages.length > 0 ? messages.join(" ") : undefined);
  };

  const handleRemoveFile = (id) => {
    const target = files.find((item) => item.id === id);
    if (target?.previewUrl) URL.revokeObjectURL(target.previewUrl);

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
            ? `Đã có ${totalCount}/${CERT_MAX_FILES} file — bấm để thêm`
            : "Chọn một hoặc nhiều file JPG/PNG/PDF/Word (tối đa 10MB mỗi file)"}
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
          {existingUrls.map((url) => {
            const kind = GetCertificateKind(url);
            const fileName = GetUrlFileName(url);

            return (
              <li key={url} className="flex items-center gap-3 p-2 border border-slate-200 rounded-lg">
                {kind === "image" ? (
                  <img
                    src={url}
                    alt={`Chứng chỉ ${fileName}`}
                    className="w-12 h-12 object-cover rounded-lg border border-slate-200 shrink-0"
                  />
                ) : (
                  <FileTypeTile kind={kind} />
                )}
                <div className="min-w-0 flex-1">
                  <a
                    href={url}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-xs text-[#2f5fa8] font-medium truncate hover:underline block"
                  >
                    {fileName}
                  </a>
                  <p className="text-xs text-slate-400">
                    {GetCertificateKindLabel(kind)} — đã tải lên trước đó
                  </p>
                </div>
                {onRemoveExisting && (
                  <button
                    type="button"
                    onClick={() => onRemoveExisting(url)}
                    className="shrink-0 text-slate-400 hover:text-deep-rose transition-colors cursor-pointer disabled:opacity-60 disabled:cursor-not-allowed"
                    aria-label={`Xóa ${fileName}`}
                    disabled={disabled}
                  >
                    <span className="material-symbols-outlined text-[20px]">close</span>
                  </button>
                )}
              </li>
            );
          })}

          {files.map((item) => {
            const kind = GetCertificateKind(item.file.name);

            return (
              <li key={item.id} className="flex items-center gap-3 p-2 border border-slate-200 rounded-lg">
                {item.previewUrl ? (
                  <img
                    src={item.previewUrl}
                    alt={`Xem trước ${item.file.name}`}
                    className="w-12 h-12 object-cover rounded-lg border border-slate-200 shrink-0"
                  />
                ) : (
                  <FileTypeTile kind={kind} />
                )}
                <div className="min-w-0 flex-1">
                  <p className="text-xs text-[#1e2a4a] font-medium truncate">{item.file.name}</p>
                  <p className="text-xs text-slate-400">
                    {GetCertificateKindLabel(kind)} — {FormatFileSize(item.file.size)}
                  </p>
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
            );
          })}
        </ul>
      )}

      {error ? (
        <p className="text-xs text-deep-rose font-medium">{error}</p>
      ) : (
        <p className="text-xs text-slate-400">{helperText}</p>
      )}
    </div>
  );
}

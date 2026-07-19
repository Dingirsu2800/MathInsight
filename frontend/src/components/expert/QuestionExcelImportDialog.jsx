import React from "react";
import { Dialog, DialogHeader, DialogTitle, DialogDescription, DialogContent, DialogFooter } from "../ui/dialog";
import { Button } from "../ui/button";
import LatexPreview from "./LatexPreview";
import { cn } from "../../utils/cn";
import { questionBankApi } from "../../services/questionBankApi";
import { getQuestionTypeLabel } from "../../utils/questionLabels";

const questionImportIssueMessages = {
  QUESTION_CONTENT_REQUIRED: "Nội dung câu hỏi không được để trống.",
  QUESTION_INVALID_TYPE: "Thể loại câu hỏi không hợp lệ hoặc không được hỗ trợ.",
  QUESTION_DIFFICULTY_REQUIRED: "Vui lòng chọn mức độ khó cho câu hỏi.",
  QUESTION_DIFFICULTY_NOT_FOUND: "Mức độ khó không tồn tại hoặc không còn hoạt động.",
  QUESTION_GRADE_INVALID: "Khối lớp phải là 10, 11 hoặc 12.",
  QUESTION_DEFAULT_POINT_INVALID: "Điểm mặc định phải nằm trong khoảng từ 0 đến 10.",
  QUESTION_TOPIC_REQUIRED: "Vui lòng chọn ít nhất một chủ đề cho câu hỏi.",
  QUESTION_TOPIC_NOT_FOUND: "Chủ đề không tồn tại, không còn hoạt động hoặc không khớp khối lớp.",
  QUESTION_PRIMARY_TOPIC_REQUIRED: "Vui lòng chọn chủ đề chính cho câu hỏi.",
  QUESTION_PRIMARY_TOPIC_INVALID: "Câu hỏi phải có đúng một chủ đề chính.",
  QUESTION_TOPIC_DUPLICATE: "Không được gán trùng chủ đề cho câu hỏi.",
  QUESTION_ANSWER_REQUIRED: "Vui lòng nhập danh sách phương án trả lời.",
  QUESTION_ANSWER_CONTENT_REQUIRED: "Nội dung phương án trả lời không được để trống.",
  QUESTION_CORRECT_ANSWER_REQUIRED: "Vui lòng chọn đáp án đúng theo thể loại câu hỏi.",
  QUESTION_TRUE_FALSE_ANSWER_COUNT_INVALID: "Câu hỏi Đúng/Sai phải có đúng hai phương án.",
  QUESTION_TRUE_FALSE_CORRECT_ANSWER_REQUIRED: "Câu hỏi Đúng/Sai phải có đúng một đáp án đúng.",
  QUESTION_SHORT_ANSWER_CORRECT_ANSWER_REQUIRED: "Câu hỏi trả lời ngắn phải có đúng một đáp án hợp lệ.",
  QUESTION_PART_REQUIRED: "Câu hỏi tổng hợp phải có ít nhất một mệnh đề hoặc câu hỏi phụ.",
  QUESTION_PART_CONTENT_REQUIRED: "Nội dung mệnh đề hoặc câu hỏi phụ không được để trống.",
  QUESTION_PART_ORDER_INVALID: "Thứ tự mệnh đề hoặc câu hỏi phụ phải lớn hơn 0.",
  QUESTION_PART_ORDER_DUPLICATE: "Không được trùng thứ tự mệnh đề hoặc câu hỏi phụ.",
  QUESTION_PART_LABEL_INVALID: "Nhãn mệnh đề không được vượt quá 10 ký tự.",
  QUESTION_PART_LABEL_DUPLICATE: "Không được trùng nhãn mệnh đề trong cùng câu hỏi.",
  QUESTION_PART_DEFAULT_POINT_INVALID: "Điểm của mệnh đề hoặc câu hỏi phụ phải nằm trong khoảng từ 0 đến 10.",
  QUESTION_PART_NUMERIC_TOLERANCE_INVALID: "Dung sai đáp án số phải lớn hơn hoặc bằng 0.",
  QUESTION_PART_NUMERIC_VALUE_INVALID: "Đáp án số vượt quá phạm vi hoặc có quá 6 chữ số thập phân.",
  QUESTION_PART_INVALID_TYPE: "Thể loại mệnh đề hoặc câu hỏi phụ không hợp lệ.",
  QUESTION_PART_ANSWER_INVALID: "Đáp án của mệnh đề hoặc câu hỏi phụ không phù hợp với thể loại.",
  QUESTION_ANSWER_NOT_ALLOWED: "Câu hỏi tổng hợp không được có phương án trả lời cấp trên.",
  QUESTION_PART_NOT_ALLOWED: "Câu hỏi không tổng hợp không được có mệnh đề hoặc câu hỏi phụ.",
  QUESTION_IMPORT_NO_QUESTIONS: "Tệp Excel phải có ít nhất một dòng câu hỏi.",
  QUESTION_IMPORT_ID_INVALID: "Phiên xem trước không hợp lệ. Vui lòng tải lại tệp Excel.",
  QUESTION_IMPORT_FORMULA_NOT_ALLOWED: "Không hỗ trợ công thức Excel trong các vùng dữ liệu nhập.",
  QUESTION_IMPORT_QUESTION_KEY_INVALID: "QuestionKey là bắt buộc và không được vượt quá 50 ký tự.",
  QUESTION_IMPORT_QUESTION_KEY_DUPLICATE: "QuestionKey phải là duy nhất trong tệp Excel.",
  QUESTION_IMPORT_ORPHAN_ROW: "Có dòng dữ liệu phụ tham chiếu đến QuestionKey không tồn tại.",
  QUESTION_IMPORT_NUMERIC_INVALID: "Giá trị số không hợp lệ. Không sử dụng dấu phân cách hàng nghìn.",
  QUESTION_IMPORT_BOOLEAN_INVALID: "Giá trị logic phải là TRUE/FALSE, YES/NO, 1/0 hoặc ĐÚNG/SAI.",
  QUESTION_IMPORT_TOPIC_AMBIGUOUS: "Tên chủ đề khớp với nhiều chủ đề đang hoạt động trong cùng khối lớp.",
};

const importErrorMessages = {
  QUESTION_IMPORT_FILE_REQUIRED: "Vui lòng chọn một tệp Excel để nhập.",
  QUESTION_IMPORT_FILE_TOO_LARGE: "Tệp tải lên vượt quá giới hạn dung lượng cho phép.",
  QUESTION_IMPORT_FILE_TYPE_NOT_SUPPORTED: "Định dạng tệp không được hỗ trợ. Vui lòng tải lên tệp Excel (.xlsx).",
  QUESTION_IMPORT_TEMPLATE_INVALID: "Tệp mẫu Excel không hợp lệ. Vui lòng tải lại tệp mẫu chính xác từ hệ thống.",
  QUESTION_IMPORT_TEMPLATE_VERSION_UNSUPPORTED: "Phiên bản tệp mẫu Excel không còn được hỗ trợ. Vui lòng sử dụng tệp mẫu mới nhất.",
  QUESTION_IMPORT_LIMIT_EXCEEDED: "Số lượng câu hỏi vượt quá giới hạn cho phép trong một lần nhập.",
  QUESTION_IMPORT_VALIDATION_FAILED: "Dữ liệu nhập chưa hợp lệ. Vui lòng kiểm tra lại lỗi của từng dòng.",
};

const mapIssueCodeToVietnamese = (code) => {
  if (questionImportIssueMessages[code]) {
    return questionImportIssueMessages[code];
  }

  return importErrorMessages[code] || "Dữ liệu nhập chưa hợp lệ. Vui lòng kiểm tra lại và thử lại.";
};

const mapErrorCodeToVietnamese = (code) => {
  switch (code) {
    case "QUESTION_IMPORT_FILE_REQUIRED":
      return "Vui lòng chọn một tệp Excel để nhập.";
    case "QUESTION_IMPORT_FILE_TOO_LARGE":
      return "Tệp tải lên vượt quá giới hạn dung lượng cho phép.";
    case "QUESTION_IMPORT_FILE_TYPE_NOT_SUPPORTED":
      return "Định dạng tệp không được hỗ trợ. Vui lòng tải lên tệp Excel (.xlsx).";
    case "QUESTION_IMPORT_TEMPLATE_INVALID":
      return "Tệp mẫu Excel không hợp lệ. Vui lòng tải lại tệp mẫu chính xác từ hệ thống.";
    case "QUESTION_IMPORT_TEMPLATE_VERSION_UNSUPPORTED":
      return "Phiên bản tệp mẫu Excel không còn được hỗ trợ. Vui lòng sử dụng tệp mẫu mới nhất.";
    case "QUESTION_IMPORT_LIMIT_EXCEEDED":
      return "Số lượng câu hỏi vượt quá giới hạn cho phép trong một lần nhập.";
    case "QUESTION_IMPORT_VALIDATION_FAILED":
      return "Xác thực dữ liệu nhập thất bại. Vui lòng kiểm tra lại lỗi của từng dòng.";
    default:
      return "Có lỗi xảy ra trong quá trình xử lý trên hệ thống. Vui lòng thử lại sau.";
  }
};

export default function QuestionExcelImportDialog({ isOpen, onClose, onImportSuccess }) {
  const [step, setStep] = React.useState("upload"); // upload | preview | result
  const [file, setFile] = React.useState(null);
  const [previewData, setPreviewData] = React.useState(null);
  const [selectedQuestionKeys, setSelectedQuestionKeys] = React.useState(new Set());
  const [expandedQuestionKeys, setExpandedQuestionKeys] = React.useState(new Set());
  const [fileErrors, setFileErrors] = React.useState([]);
  const [generalConfirmErrors, setGeneralConfirmErrors] = React.useState([]);

  // Custom validations / confirm status locks
  const [confirmLocked, setConfirmLocked] = React.useState(false);
  const [isUploading, setIsUploading] = React.useState(false);
  const [isConfirming, setIsConfirming] = React.useState(false);
  const [uploadError, setUploadError] = React.useState("");
  const [confirmError, setConfirmError] = React.useState("");
  const [isNetworkError, setIsNetworkError] = React.useState(false);
  const [resultData, setResultData] = React.useState(null);
  const [isDragging, setIsDragging] = React.useState(false);

  const fileInputRef = React.useRef(null);

  const resetState = () => {
    setFile(null);
    setPreviewData(null);
    setSelectedQuestionKeys(new Set());
    setExpandedQuestionKeys(new Set());
    setFileErrors([]);
    setGeneralConfirmErrors([]);
    setConfirmLocked(false);
    setIsUploading(false);
    setIsConfirming(false);
    setUploadError("");
    setConfirmError("");
    setIsNetworkError(false);
    setResultData(null);
    setStep("upload");
    setIsDragging(false);
    if (fileInputRef.current) {
      fileInputRef.current.value = "";
    }
  };

  // Reset when dialog opens / closes
  React.useEffect(() => {
    if (isOpen) {
      resetState();
    }
  }, [isOpen]);

  const handleClose = () => {
    if (isConfirming) return; // Block close during confirming state
    resetState();
    onClose();
  };

  const handleDownloadTemplate = async () => {
    try {
      const res = await questionBankApi.getQuestionImportTemplate();
      const blob = new Blob([res.data], { type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" });
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.setAttribute("download", "ImportQuestionTemplate.xlsx");
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);

      // Delayed cleanup to prevent browser interrupts
      setTimeout(() => {
        window.URL.revokeObjectURL(url);
      }, 500);
    } catch (err) {
      console.error("Failed to download template:", err);
      alert("Tải file mẫu thất bại. Vui lòng thử lại sau.");
    }
  };

  const handleFileSelect = (selectedFile) => {
    setUploadError("");
    setConfirmError("");
    setPreviewData(null);
    setSelectedQuestionKeys(new Set());
    setExpandedQuestionKeys(new Set());
    setFileErrors([]);
    setGeneralConfirmErrors([]);
    setConfirmLocked(false);

    const allowedExtensions = /(\.xlsx)$/i;
    if (!allowedExtensions.exec(selectedFile.name)) {
      setUploadError("Định dạng tệp không được hỗ trợ. Vui lòng chọn tệp Excel (.xlsx).");
      setFile(null);
      if (fileInputRef.current) fileInputRef.current.value = "";
      return;
    }

    const maxSize = 20 * 1024 * 1024; // 20 MB
    if (selectedFile.size > maxSize) {
      setUploadError("Kích thước tệp vượt quá giới hạn 20MB. Vui lòng chọn tệp nhỏ hơn.");
      setFile(null);
      if (fileInputRef.current) fileInputRef.current.value = "";
      return;
    }

    setFile(selectedFile);
    if (fileInputRef.current) {
      fileInputRef.current.value = "";
    }
  };

  const handleFileChange = (e) => {
    const selectedFile = e.target.files?.[0];
    if (!selectedFile) return;
    handleFileSelect(selectedFile);
  };

  const handlePreviewUpload = async () => {
    if (!file) return;
    setIsUploading(true);
    setUploadError("");
    try {
      const res = await questionBankApi.previewQuestionImport(file);
      const data = res.data;
      setPreviewData(data);
      setFileErrors(data.fileErrors || []);

      // Auto-select all valid rows
      const validKeys = new Set();
      if (data.items) {
        data.items.forEach(item => {
          if (item.isValid) {
            validKeys.add(item.questionKey);
          }
        });
      }
      setSelectedQuestionKeys(validKeys);
      setStep("preview");
    } catch (err) {
      console.error("Preview failed:", err);
      const errCode = err.response?.data?.code;
      setUploadError(mapErrorCodeToVietnamese(errCode));
    } finally {
      setIsUploading(false);
    }
  };

  const toggleSelectAll = () => {
    if (confirmLocked || fileErrors.length > 0) return;
    const allValidSelected = Array.from(previewData?.items || [])
      .filter(item => item.isValid)
      .every(item => selectedQuestionKeys.has(item.questionKey));

    if (allValidSelected) {
      setSelectedQuestionKeys(new Set());
    } else {
      const newSelected = new Set();
      previewData?.items.forEach(item => {
        if (item.isValid) {
          newSelected.add(item.questionKey);
        }
      });
      setSelectedQuestionKeys(newSelected);
    }
  };

  const toggleSelectItem = (key) => {
    if (confirmLocked || fileErrors.length > 0) return;
    const newSelected = new Set(selectedQuestionKeys);
    if (newSelected.has(key)) {
      newSelected.delete(key);
    } else {
      newSelected.add(key);
    }
    setSelectedQuestionKeys(newSelected);
  };

  const toggleExpandItem = (key) => {
    const newExpanded = new Set(expandedQuestionKeys);
    if (newExpanded.has(key)) {
      newExpanded.delete(key);
    } else {
      newExpanded.add(key);
    }
    setExpandedQuestionKeys(newExpanded);
  };

  const handleConfirmImport = async () => {
    if (selectedQuestionKeys.size === 0 || confirmLocked) return;
    setIsConfirming(true);
    setConfirmError("");
    setGeneralConfirmErrors([]);
    setIsNetworkError(false);

    // Build items payload exactly matching preview drafts
    const itemsPayload = Array.from(previewData?.items || [])
      .filter(item => selectedQuestionKeys.has(item.questionKey))
      .map(item => ({
        questionKey: item.questionKey,
        draft: item.draft
      }));

    const payload = {
      importId: previewData.importId,
      items: itemsPayload
    };

    try {
      const res = await questionBankApi.confirmQuestionImport(payload);
      setResultData(res.data);
      setStep("result");
    } catch (err) {
      console.error("Confirm failed:", err);
      const response = err.response;
      if (!response) {
        // Network failure
        setConfirmError(
          "Mất kết nối mạng hoặc hết thời gian phản hồi từ máy chủ khi đang thực hiện nhập câu hỏi. " +
          "Vui lòng làm mới danh sách ngân hàng câu hỏi để kiểm tra xem câu hỏi đã được tạo hay chưa trước khi gửi lại yêu cầu để tránh tạo lặp dữ liệu."
        );
        setIsNetworkError(true);
      } else if (response.status === 400 && response.data?.code === "QUESTION_IMPORT_VALIDATION_FAILED") {
        // Validation error on confirm
        const data = response.data;
        const serverErrors = data.errors || [];

        // Lock confirm & stay on preview step
        setConfirmLocked(true);
        setConfirmError("Xác thực dữ liệu nhập thất bại. Vui lòng kiểm tra lại lỗi của các câu hỏi bị ảnh hưởng.");

        // Map errors back to items
        const updatedItems = previewData.items.map(item => {
          const itemErrors = serverErrors.filter(e => {
            const key = e.questionKey || e.QuestionKey;
            return key === item.questionKey;
          });

          if (itemErrors.length > 0) {
            return {
              ...item,
              isValid: false,
              errors: [...(item.errors || []), ...itemErrors]
            };
          }
          return item;
        });

        // deselect rows with errors
        const newSelected = new Set(selectedQuestionKeys);
        serverErrors.forEach(e => {
          const key = e.questionKey || e.QuestionKey;
          if (key) {
            newSelected.delete(key);
          }
        });
        setSelectedQuestionKeys(newSelected);

        // Map general/unmapped errors (where questionKey is null, empty or not matching)
        const unmapped = serverErrors.filter(e => {
          const key = e.questionKey || e.QuestionKey;
          return !key || !previewData.items.some(item => item.questionKey === key);
        });
        setGeneralConfirmErrors(unmapped);

        setPreviewData(prev => ({
          ...prev,
          items: updatedItems
        }));
      } else {
        const errCode = response.data?.code;
        setConfirmError(mapErrorCodeToVietnamese(errCode));
      }
    } finally {
      setIsConfirming(false);
    }
  };

  const getTruncatedContent = (content) => {
    if (!content) return "";
    return content.length > 80 ? content.substring(0, 80) + "..." : content;
  };

  const renderIssueText = (err) => {
    if (!err) return "";
    if (typeof err === "string") return err;

    const code = err.code || err.Code;

    // Map code if possible
    const mapped = mapIssueCodeToVietnamese(code);

    const column = err.column || err.Column;
    const row = err.row || err.Row;

    if (column && row) {
      return `Dòng ${row}, Cột ${column}: ${mapped}`;
    }
    if (row) {
      return `Dòng ${row}: ${mapped}`;
    }
    return mapped;
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={isConfirming ? () => {} : handleClose}
      variant="modal"
      className="max-w-6xl w-[min(96vw,1200px)] h-[85vh] max-h-[85vh] flex flex-col"
    >
      <DialogHeader className="shrink-0 pb-4 border-b border-outline-variant">
        <DialogTitle className="flex items-center gap-2 text-primary">
          <span className="material-symbols-outlined text-[24px]">publish</span>
          Nhập câu hỏi từ Excel
        </DialogTitle>
        <DialogDescription>
          Tải lên bảng tính chứa danh sách câu hỏi để thêm tự động vào ngân hàng câu hỏi.
        </DialogDescription>
      </DialogHeader>

      <div className="flex-1 min-h-0 overflow-y-auto p-6 space-y-4 font-semibold text-on-surface">
        {step === "upload" && (
          <div className="space-y-6 py-4">
            {/* Guide box */}
            <div className="p-4 bg-primary/5 border border-primary/10 rounded-xl space-y-2">
              <h4 className="text-xs font-bold text-primary flex items-center gap-1.5 uppercase tracking-wide">
                <span className="material-symbols-outlined text-[18px]">info</span>
                Hướng dẫn định dạng tệp Excel
              </h4>
              <ul className="text-xs text-on-surface-variant list-disc pl-5 space-y-1">
                <li>Vui lòng sử dụng tệp mẫu Excel được cung cấp từ hệ thống để chuẩn hóa dữ liệu.</li>
                <li>Hệ thống hỗ trợ các loại câu hỏi: Trắc nghiệm (SINGLE_CHOICE, MULTIPLE_CHOICE), Đúng/Sai (TRUE_FALSE), Trả lời ngắn (SHORT_ANSWER) và Câu hỏi tổng hợp (COMPOSITE).</li>
                <li>Mã hóa LaTeX cho công thức toán cần được bọc trong cặp dấu đô la (ví dụ: <code className="bg-surface-container px-1 py-0.5 rounded font-mono font-bold">$x^2 - 4 = 0$</code>).</li>
              </ul>
              <div className="pt-2">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={handleDownloadTemplate}
                  className="font-bold flex items-center gap-1.5 cursor-pointer"
                >
                  <span className="material-symbols-outlined text-[16px]">download</span>
                  Tải file mẫu Excel (.xlsx)
                </Button>
              </div>
            </div>

            {/* Drag drop area */}
            <div
              onDragOver={(e) => {
                if (isUploading) return;
                e.preventDefault();
                setIsDragging(true);
              }}
              onDragLeave={() => setIsDragging(false)}
              onDrop={(e) => {
                if (isUploading) return;
                e.preventDefault();
                setIsDragging(false);
                const selectedFile = e.dataTransfer.files?.[0];
                if (selectedFile) {
                  handleFileSelect(selectedFile);
                }
              }}
              onClick={() => {
                if (!isUploading) {
                  fileInputRef.current?.click();
                }
              }}
              className={cn(
                "border-2 border-dashed rounded-xl p-8 text-center flex flex-col items-center justify-center gap-2.5 transition-all bg-pure-surface cursor-pointer select-none",
                isDragging ? "border-primary bg-primary/5 scale-[1.01]" : "",
                file && !isDragging ? "border-primary bg-primary/5 hover:border-primary/80" : "border-outline-variant hover:border-primary/50"
              )}
            >
              <span className={cn(
                "material-symbols-outlined text-[48px]",
                file || isDragging ? "text-primary" : "text-on-surface-variant"
              )}>
                description
              </span>
              {file ? (
                <div className="space-y-1">
                  <p className="text-sm font-bold text-on-surface">{file.name}</p>
                  <p className="text-xs text-on-surface-variant font-medium">
                    Dung lượng: {(file.size / 1024 / 1024).toFixed(2)} MB
                  </p>
                  <span className="text-[10px] text-primary hover:underline font-bold mt-1.5 inline-block">
                    Nhập hoặc kéo thả để thay đổi tệp tin
                  </span>
                </div>
              ) : (
                <div className="space-y-1.5">
                  <p className="text-sm font-bold text-on-surface">Kéo thả tệp tin Excel (.xlsx) tại đây hoặc nhấp để chọn tệp</p>
                  <p className="text-[11px] text-on-surface-variant font-medium">Kích thước tối đa 20MB</p>
                </div>
              )}
              <input
                ref={fileInputRef}
                type="file"
                accept=".xlsx, application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                onChange={handleFileChange}
                className="hidden"
                id="excel-file-uploader"
                disabled={isUploading}
              />
            </div>

            {uploadError && (
              <div className="p-3 text-xs text-error bg-error/5 border border-error/10 rounded-lg flex items-start gap-2" role="alert">
                <span className="material-symbols-outlined text-[16px] shrink-0 mt-0.5">error</span>
                <span>{uploadError}</span>
              </div>
            )}
          </div>
        )}

        {step === "preview" && previewData && (
          <div className="space-y-4">
            {/* Header counters */}
            <div className="grid grid-cols-3 gap-4 shrink-0">
              <div className="p-3.5 bg-surface-container-lowest border border-whisper-border rounded-xl text-center space-y-0.5">
                <p className="text-[10px] font-bold text-on-surface-variant uppercase tracking-wide">Tổng số câu hỏi</p>
                <p className="text-2xl font-black text-on-surface">{previewData.totalCount}</p>
              </div>
              <div className="p-3.5 bg-emerald-success/5 border border-emerald-success/10 rounded-xl text-center space-y-0.5">
                <p className="text-[10px] font-bold text-emerald-success uppercase tracking-wide">Số câu hợp lệ</p>
                <p className="text-2xl font-black text-emerald-success">{previewData.validCount}</p>
              </div>
              <div className="p-3.5 bg-error/5 border border-error/10 rounded-xl text-center space-y-0.5">
                <p className="text-[10px] font-bold text-error uppercase tracking-wide">Số câu lỗi</p>
                <p className="text-2xl font-black text-error">{previewData.invalidCount}</p>
              </div>
            </div>

            {/* Error messages card */}
            {fileErrors.length > 0 && (
              <div className="p-4 bg-error/5 border border-error/15 rounded-xl space-y-2">
                <h4 className="text-xs font-bold text-error flex items-center gap-1.5">
                  <span className="material-symbols-outlined text-[18px]">warning</span>
                  Phát hiện lỗi cấu trúc tệp mẫu Excel
                </h4>
                <ul className="text-xs text-error/95 list-disc pl-5 space-y-1 font-semibold">
                  {fileErrors.map((err, idx) => (
                    <li key={idx}>{renderIssueText(err)}</li>
                  ))}
                </ul>
                <p className="text-[10px] text-on-surface-variant font-medium pt-1">
                  * Vui lòng chỉnh sửa tệp Excel theo đúng yêu cầu cấu trúc và thực hiện tải lên xem trước lại.
                </p>
              </div>
            )}

            {/* Confirm validation error banner */}
            {confirmError && (
              <div className="p-3.5 text-xs text-error bg-error/5 border border-error/10 rounded-lg space-y-1.5" role="alert">
                <div className="flex items-start gap-2">
                  <span className="material-symbols-outlined text-[16px] shrink-0 mt-0.5">error</span>
                  <span className="font-bold">{confirmError}</span>
                </div>
                {generalConfirmErrors.length > 0 && (
                  <ul className="list-disc pl-6 space-y-1 font-semibold text-error/90 text-left">
                    {generalConfirmErrors.map((err, idx) => (
                      <li key={idx}>{renderIssueText(err)}</li>
                    ))}
                  </ul>
                )}
                {isNetworkError && (
                  <p className="text-[10px] text-on-surface-variant font-bold">
                    Chú ý: Hãy bấm tải lại trang danh sách trước khi thực hiện thử lại để xác thực không tạo trùng câu hỏi.
                  </p>
                )}
              </div>
            )}

            {confirmLocked && !confirmError && (
              <div className="p-3 text-xs text-error bg-error/5 border border-error/10 rounded-lg flex items-start gap-2" role="alert">
                <span className="material-symbols-outlined text-[16px] shrink-0 mt-0.5">lock</span>
                <span>Yêu cầu nhập Excel đã bị khóa do lỗi xác thực. Vui lòng tải lên tệp Excel mới đã được chỉnh sửa.</span>
              </div>
            )}

            {/* Table layout */}
            <div className="border border-outline-variant rounded-xl overflow-hidden bg-pure-surface flex-1 flex flex-col min-h-[300px]">
              <div className="overflow-x-auto overflow-y-auto max-h-[360px] flex-1">
                <table className="w-full text-left border-collapse table-fixed">
                  <thead>
                    <tr className="bg-surface-container border-b border-outline-variant text-[11px] font-bold text-on-surface-variant select-none uppercase tracking-wide">
                      <th className="p-3 text-center w-12">
                        <input
                          type="checkbox"
                          checked={
                            Array.from(previewData?.items || []).filter(item => item.isValid).length > 0 &&
                            Array.from(previewData?.items || [])
                              .filter(item => item.isValid)
                              .every(item => selectedQuestionKeys.has(item.questionKey))
                          }
                          onChange={toggleSelectAll}
                          disabled={confirmLocked || fileErrors.length > 0 || Array.from(previewData?.items || []).filter(item => item.isValid).length === 0}
                          className="w-4 h-4 cursor-pointer focus:ring-0 disabled:opacity-50"
                        />
                      </th>
                      <th className="p-3 text-center w-16">Dòng</th>
                      <th className="p-3 text-center w-24">Mã câu</th>
                      <th className="p-3 w-32 text-center">Thể loại</th>
                      <th className="p-3 w-[40%]">Nội dung đề bài</th>
                      <th className="p-3 w-[30%]">Trạng thái xác thực</th>
                    </tr>
                  </thead>
                  <tbody>
                    {previewData.items.map((item, idx) => {
                      const isExpanded = expandedQuestionKeys.has(item.questionKey);
                      return (
                        <React.Fragment key={idx}>
                          <tr className={cn(
                            "border-b border-outline-variant text-xs font-semibold text-on-surface hover:bg-surface-container-lowest transition-colors",
                            !item.isValid && "bg-error/[0.02]"
                          )}>
                            <td className="p-3 text-center">
                              <input
                                type="checkbox"
                                checked={selectedQuestionKeys.has(item.questionKey)}
                                onChange={() => toggleSelectItem(item.questionKey)}
                                disabled={!item.isValid || confirmLocked || fileErrors.length > 0}
                                className="w-4 h-4 cursor-pointer focus:ring-0 disabled:opacity-30 disabled:cursor-not-allowed"
                              />
                            </td>
                            <td className="p-3 text-center text-on-surface-variant font-mono">
                              {item.sourceRow}
                            </td>
                            <td className="p-3 text-center font-mono font-bold text-primary">
                              {item.questionKey}
                            </td>
                            <td className="p-3 text-center">
                              <span className="px-2 py-0.5 bg-primary/5 text-primary text-[10px] font-extrabold rounded">
                                {getQuestionTypeLabel(item.draft?.questionType)}
                              </span>
                            </td>
                            <td className="p-3 font-normal">
                              <div className="flex flex-col gap-1">
                                <div className="max-h-[48px] overflow-hidden relative text-xs">
                                  <LatexPreview content={item.draft?.questionContent || ""} />
                                  <div className="absolute bottom-0 left-0 right-0 h-3.5 bg-gradient-to-t from-pure-surface/90 to-transparent pointer-events-none" />
                                </div>
                                <button
                                  type="button"
                                  onClick={() => toggleExpandItem(item.questionKey)}
                                  className="text-[10px] text-primary hover:underline font-bold text-left self-start cursor-pointer flex items-center gap-0.5 mt-1"
                                >
                                  <span className="material-symbols-outlined text-[14px]">
                                    {isExpanded ? "expand_less" : "expand_more"}
                                  </span>
                                  {isExpanded ? "Ẩn chi tiết" : "Xem chi tiết"}
                                </button>
                              </div>
                            </td>
                            <td className="p-3">
                              {item.isValid ? (
                                <span className="text-emerald-success flex items-center gap-1 font-bold">
                                  <span className="material-symbols-outlined text-[16px]">check_circle</span>
                                  Hợp lệ
                                </span>
                              ) : (
                                <div className="space-y-1">
                                  {item.errors.map((err, eIdx) => (
                                    <div key={eIdx} className="text-error flex items-start gap-1 font-semibold leading-relaxed">
                                      <span className="material-symbols-outlined text-[14px] shrink-0 mt-0.5">warning</span>
                                      <span>{renderIssueText(err)}</span>
                                    </div>
                                  ))}
                                </div>
                              )}
                            </td>
                          </tr>

                          {/* Expanded LaTeX preview block */}
                          {isExpanded && (
                            <tr className="bg-surface-container-lowest border-b border-outline-variant">
                              <td colSpan={6} className="p-4">
                                {!item.draft ? (
                                  <div className="p-3.5 bg-error/5 border border-error/15 rounded-xl text-xs text-error flex items-start gap-2 select-none shadow-sm animate-fade-in">
                                    <span className="material-symbols-outlined text-[16px] shrink-0 mt-0.5">warning</span>
                                    <span className="font-semibold">Dòng này không thể xem chi tiết bản nháp vì dữ liệu tệp Excel chưa hợp lệ.</span>
                                  </div>
                                ) : (
                                  <div className="space-y-4 bg-pure-surface border border-outline-variant/60 rounded-xl p-4 text-xs leading-relaxed shadow-sm">
                                    {/* 1. Đề bài */}
                                    <div>
                                      <p className="font-bold text-on-surface-variant uppercase tracking-wider text-[9px] mb-1.5 flex items-center gap-1 select-none font-sans">
                                        <span className="material-symbols-outlined text-[14px]">description</span>
                                        Đề bài:
                                      </p>
                                      <div className="p-3.5 border border-outline-variant/60 rounded-xl bg-pure-surface min-h-[40px] flex items-center">
                                        <LatexPreview content={item.draft.questionContent || ""} />
                                      </div>
                                    </div>

                                    {/* 2. Hình minh họa */}
                                    {item.draft.pictureUrl && (
                                      <div className="border-t border-outline-variant/40 pt-3">
                                        <p className="font-bold text-on-surface-variant uppercase tracking-wider text-[9px] mb-1.5 flex items-center gap-1 select-none font-sans">
                                          <span className="material-symbols-outlined text-[14px]">image</span>
                                          Hình ảnh minh họa:
                                        </p>
                                        <div className="p-2 border border-outline-variant/60 rounded-xl bg-surface-container-low max-w-xs flex items-center justify-center">
                                          <img
                                            src={item.draft.pictureUrl}
                                            alt="Ảnh minh họa"
                                            className="max-h-40 max-w-full w-auto object-contain rounded"
                                          />
                                        </div>
                                      </div>
                                    )}

                                    {/* 3. Phương án trả lời */}
                                    {item.draft.answers && item.draft.answers.length > 0 && (
                                      <div className="border-t border-outline-variant/40 pt-3">
                                        <p className="font-bold text-on-surface-variant uppercase tracking-wider text-[9px] mb-1.5 flex items-center gap-1 select-none font-sans">
                                          <span className="material-symbols-outlined text-[14px]">list</span>
                                          Phương án trả lời:
                                        </p>
                                        <div className="grid grid-cols-1 md:grid-cols-2 gap-2 mt-1">
                                          {item.draft.answers.map((answer, ansIdx) => (
                                            <div key={ansIdx} className={cn(
                                              "flex items-start gap-2 text-xs p-2.5 rounded-xl border leading-relaxed",
                                              answer.isCorrect
                                                ? "bg-emerald-success/5 border-emerald-success/20 text-emerald-success"
                                                : "bg-surface-container-low border-whisper-border/30 text-on-surface"
                                            )}>
                                              <span className="font-bold shrink-0">{String.fromCharCode(65 + ansIdx)}.</span>
                                              <div className="flex-1 min-w-0">
                                                <LatexPreview content={answer.answerContent || ""} />
                                              </div>
                                              {answer.isCorrect && (
                                                <span className="material-symbols-outlined text-[16px] text-emerald-success shrink-0 mt-0.5">check_circle</span>
                                              )}
                                            </div>
                                          ))}
                                        </div>
                                      </div>
                                    )}

                                    {/* 4. Các mệnh đề / phần câu hỏi */}
                                    {item.draft.parts && item.draft.parts.length > 0 && (
                                      <div className="border-t border-outline-variant/40 pt-3 space-y-2.5">
                                        <p className="font-bold text-on-surface-variant uppercase tracking-wider text-[9px] flex items-center gap-1 select-none font-sans">
                                          <span className="material-symbols-outlined text-[14px]">view_list</span>
                                          Các mệnh đề / Câu hỏi phụ:
                                        </p>
                                        <div className="space-y-2.5">
                                          {[...item.draft.parts]
                                            .sort((a, b) => (a.partOrder || 0) - (b.partOrder || 0))
                                            .map((part, partIdx) => {
                                              const displayLabel = part.partLabel || String.fromCharCode(97 + partIdx);
                                              return (
                                                <div key={partIdx} className="p-3 border border-outline-variant/60 rounded-xl bg-surface-container-low space-y-2 text-xs leading-relaxed">
                                                  <div className="flex items-start gap-2">
                                                    <span className="font-bold shrink-0">{displayLabel}.</span>
                                                    <div className="flex-1 min-w-0 font-medium">
                                                      <LatexPreview content={part.partContent || ""} />
                                                    </div>
                                                  </div>

                                                  {/* Answers based on partType */}
                                                  <div className="pl-4 py-1 border-l-2 border-outline-variant flex flex-wrap gap-x-4 gap-y-1 font-semibold text-on-surface-variant text-[11px]">
                                                    {part.partType === "TRUE_FALSE" && (
                                                      <span>
                                                        Đáp án: {" "}
                                                        <span className="text-primary font-bold">
                                                          {part.correctBoolean === true || part.correctBoolean === "true"
                                                            ? "Đúng"
                                                            : part.correctBoolean === false || part.correctBoolean === "false"
                                                              ? "Sai"
                                                              : "Chưa chọn"}
                                                        </span>
                                                      </span>
                                                    )}
                                                    {part.partType === "SHORT_ANSWER" && (
                                                      <span>
                                                        Đáp án chuỗi: <span className="text-primary font-mono font-bold">{part.correctText || "Rỗng"}</span>
                                                      </span>
                                                    )}
                                                    {part.partType === "NUMERIC_ANSWER" && (
                                                      <span>
                                                        Đáp án số: <span className="text-primary font-mono font-bold">{part.correctNumeric != null ? part.correctNumeric : "Rỗng"}</span>
                                                        {part.numericTolerance != null && (
                                                          <span className="text-on-surface-variant/80 font-normal">
                                                            {" "}
                                                            (Dung sai: ±{part.numericTolerance})
                                                          </span>
                                                        )}
                                                      </span>
                                                    )}
                                                  </div>

                                                  {part.explanation && (
                                                    <details className="group pl-4 select-none">
                                                      <summary className="text-[10px] font-bold text-on-surface-variant/80 hover:underline cursor-pointer list-none flex items-center gap-0.5 font-sans">
                                                        <span className="material-symbols-outlined text-[12px] transition-transform group-open:rotate-180">expand_more</span>
                                                        Xem hướng dẫn giải mệnh đề
                                                      </summary>
                                                      <div className="mt-1.5 p-2 bg-pure-surface rounded-lg text-[11px] font-medium text-on-surface-variant leading-relaxed border border-outline-variant/30">
                                                        <LatexPreview content={part.explanation} />
                                                      </div>
                                                    </details>
                                                  )}
                                                </div>
                                              );
                                            })}
                                        </div>
                                      </div>
                                    )}

                                    {/* 5. Lời giải */}
                                    {item.draft.solutionContent && (
                                      <details className="group border-t border-outline-variant/40 pt-3 select-none">
                                        <summary className="text-[10px] font-bold text-primary cursor-pointer list-none flex items-center gap-0.5 hover:underline font-sans">
                                          <span className="material-symbols-outlined text-[14px] transition-transform group-open:rotate-180">
                                            expand_more
                                          </span>
                                          Xem lời giải chi tiết
                                        </summary>
                                        <div className="mt-2.5 p-3.5 bg-surface-container rounded-xl border border-outline-variant/30">
                                          <LatexPreview content={item.draft.solutionContent} />
                                        </div>
                                      </details>
                                    )}

                                    {/* 6. Mã nguồn LaTeX */}
                                    <details className="group border-t border-outline-variant/40 pt-3 select-none font-sans">
                                      <summary className="text-[10px] font-bold text-on-surface-variant hover:underline cursor-pointer list-none flex items-center gap-0.5">
                                        <span className="material-symbols-outlined text-[14px] transition-transform group-open:rotate-180">
                                          expand_more
                                        </span>
                                        Xem mã nguồn LaTeX
                                      </summary>
                                      <div className="mt-2.5 font-medium p-3 bg-surface-container rounded-xl font-mono break-all whitespace-pre-wrap select-all text-xs border border-outline-variant/30 text-on-surface-variant">
                                        {item.draft.questionContent}
                                      </div>
                                    </details>
                                  </div>
                                )}
                              </td>
                            </tr>
                          )}
                        </React.Fragment>
                      );
                    })}
                  </tbody>
                </table>
              </div>
              <div className="bg-surface-container p-3 shrink-0 text-[11px] font-bold text-on-surface-variant border-t border-outline-variant flex justify-between select-none">
                <span>Tổng số dòng: {previewData.items.length}</span>
                <span>Đã chọn: {selectedQuestionKeys.size} / {previewData.validCount} câu hỏi hợp lệ</span>
              </div>
            </div>
          </div>
        )}

        {step === "result" && resultData && (
          <div className="space-y-5 py-4 text-center max-w-lg mx-auto">
            <div className="w-16 h-16 bg-emerald-success/10 rounded-full flex items-center justify-center mx-auto">
              <span className="material-symbols-outlined text-[40px] text-emerald-success">check_circle</span>
            </div>
            <div className="space-y-1">
              <h3 className="text-lg font-black text-on-surface">Nhập dữ liệu thành công!</h3>
              <p className="text-xs text-on-surface-variant">
                Đã thêm thành công <strong>{resultData.createdCount}</strong> câu hỏi mới vào ngân hàng câu hỏi môn Toán học.
              </p>
            </div>

            {resultData.questions && resultData.questions.length > 0 && (
              <div className="border border-outline-variant rounded-xl overflow-hidden bg-pure-surface text-left">
                <div className="bg-surface-container px-3 py-2 text-[10px] font-bold text-on-surface-variant uppercase tracking-wide border-b border-outline-variant">
                  Danh sách câu hỏi đã tạo
                </div>
                <div className="max-h-48 overflow-y-auto divide-y divide-outline-variant text-xs">
                  {resultData.questions.map((q, idx) => (
                    <div key={idx} className="px-3 py-2 flex justify-between font-mono">
                      <span className="font-bold text-primary">{q.questionKey}</span>
                      <span className="text-on-surface-variant">{q.questionId}</span>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        )}
      </div>

      <DialogFooter className="shrink-0 pt-4 border-t border-outline-variant bg-surface-container-lowest">
        {step === "upload" && (
          <div className="flex gap-2 w-full justify-end">
            <Button
              type="button"
              variant="outline"
              onClick={handleClose}
              disabled={isUploading}
              className="cursor-pointer"
            >
              Hủy
            </Button>
            <Button
              type="button"
              variant="primary"
              onClick={handlePreviewUpload}
              disabled={isUploading || !file}
              className="cursor-pointer font-bold flex items-center gap-1"
            >
              {isUploading ? (
                <>
                  <span className="animate-spin w-4 h-4 border-2 border-white border-t-transparent rounded-full mr-1.5"></span>
                  Đang tải lên…
                </>
              ) : (
                <>
                  <span className="material-symbols-outlined text-[16px]">visibility</span>
                  Xem trước dữ liệu
                </>
              )}
            </Button>
          </div>
        )}

        {step === "preview" && (
          <div className="flex justify-between w-full items-center">
            <Button
              type="button"
              variant="outline"
              onClick={() => {
                setStep("upload");
                setConfirmError("");
                setConfirmLocked(false);
                setGeneralConfirmErrors([]);
              }}
              disabled={isConfirming}
              className="cursor-pointer"
            >
              Quay lại tải file
            </Button>
            <div className="flex gap-2">
              <Button
                type="button"
                variant="outline"
                onClick={handleClose}
                disabled={isConfirming}
                className="cursor-pointer"
              >
                Hủy
              </Button>
              <Button
                type="button"
                variant="primary"
                onClick={handleConfirmImport}
                disabled={isConfirming || selectedQuestionKeys.size === 0 || confirmLocked || fileErrors.length > 0}
                className="cursor-pointer font-bold flex items-center gap-1"
              >
                {isConfirming ? (
                  <>
                    <span className="animate-spin w-4 h-4 border-2 border-white border-t-transparent rounded-full mr-1.5"></span>
                    Đang lưu câu hỏi…
                  </>
                ) : (
                  <>
                    <span className="material-symbols-outlined text-[16px]">check</span>
                    Nhập {selectedQuestionKeys.size} câu hỏi đã chọn
                  </>
                )}
              </Button>
            </div>
          </div>
        )}

        {step === "result" && (
          <div className="flex gap-2 w-full justify-end">
            <Button
              type="button"
              variant="primary"
              onClick={() => {
                onImportSuccess();
                handleClose();
              }}
              className="cursor-pointer font-bold"
            >
              Hoàn tất
            </Button>
          </div>
        )}
      </DialogFooter>
    </Dialog>
  );
}

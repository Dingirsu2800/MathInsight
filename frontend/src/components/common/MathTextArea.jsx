import React, { useState, useRef } from "react";
import LatexPreview from "../expert/LatexPreview";

export default function MathTextArea({ 
  value, 
  onChange, 
  placeholder = "Nhập nội dung chi tiết... Hỗ trợ mã LaTeX bọc trong dấu $ hoặc $$", 
  minHeight = "min-h-[100px]",
  className = ""
}) {
  const [isMathHelperOpen, setIsMathHelperOpen] = useState(false);
  const textareaRef = useRef(null);

  const handleInsertLatex = (latex) => {
    const textarea = textareaRef.current;
    if (textarea) {
      const startPos = textarea.selectionStart;
      const endPos = textarea.selectionEnd;
      const text = value;
      const newText = text.substring(0, startPos) + latex + text.substring(endPos, text.length);
      
      // Simulate an onChange event
      onChange({ target: { value: newText } });
      
      setTimeout(() => {
        textarea.focus();
        textarea.setSelectionRange(startPos + latex.length, startPos + latex.length);
      }, 0);
    } else {
      onChange({ target: { value: value + " " + latex } });
    }
  };

  return (
    <div className={`border border-outline-variant rounded-xl overflow-hidden focus-within:border-primary focus-within:ring-1 focus-within:ring-primary transition-shadow bg-pure-surface ${className}`}>
      {/* Editor Toolbar */}
      <div className="flex items-center gap-1 p-1.5 border-b border-outline-variant bg-surface-container-lowest">
        <button
          type="button"
          onClick={() => setIsMathHelperOpen(!isMathHelperOpen)}
          className={`px-2 py-1 rounded text-xs font-bold transition-all duration-150 flex items-center gap-1 cursor-pointer active:scale-[0.97] ${
            isMathHelperOpen
              ? "bg-primary text-white shadow-sm scale-[1.01]"
              : "hover:bg-surface-container hover:-translate-y-0.5 text-primary bg-primary/5"
          }`}
          title="Mở công cụ chèn công thức LaTeX"
        >
          <span className="material-symbols-outlined text-[16px]">calculate</span>
          Mã toán
        </button>
      </div>

      {/* Math Helper Panel */}
      {isMathHelperOpen && (
        <div className="bg-surface-container-lowest border-b border-outline-variant p-4 space-y-4 mi-panel-down">
          <div>
            <h5 className="text-[10px] font-black text-on-surface-variant mb-2 uppercase tracking-wider">Mã toán nhanh:</h5>
            <div className="flex flex-wrap gap-2">
              {[
                { label: "Tích phân", code: "\\int_{a}^{b} f(x) dx" },
                { label: "Nguyên hàm", code: "\\int f(x) dx" },
                { label: "Đạo hàm", code: "f'(x) = \\lim_{\\Delta x \\to 0} \\frac{\\Delta y}{\\Delta x}" },
                { label: "Giới hạn", code: "\\lim_{x \\to x_0} f(x)" },
                { label: "Phân số", code: "\\frac{a}{b}" },
                { label: "Căn thức", code: "\\sqrt{x^2 + 1}" }
              ].map((sym, idx) => (
                <button
                  key={idx}
                  type="button"
                  onClick={() => handleInsertLatex(`$${sym.code}$`)}
                  className="flex items-center gap-2 bg-pure-surface hover:bg-surface-container border border-whisper-border px-2.5 py-1.5 rounded-lg text-xs transition-all duration-150 active:scale-[0.96] cursor-pointer"
                >
                  <div className="scale-90 select-none">
                    <LatexPreview content={`$${sym.code}$`} />
                  </div>
                  <span className="text-[10px] text-on-surface-variant font-mono bg-surface-container-low px-1.5 py-0.5 rounded">
                    {sym.code}
                  </span>
                </button>
              ))}
            </div>
          </div>

          <div>
            <h5 className="text-[10px] font-black text-on-surface-variant mb-2 uppercase tracking-wider">Ký tự Hy Lạp:</h5>
            <div className="flex flex-wrap gap-1.5">
              {["\\alpha", "\\beta", "\\gamma", "\\delta", "\\theta", "\\lambda", "\\pi", "\\omega", "\\Delta"].map((sym, idx) => (
                <button
                  key={idx}
                  type="button"
                  onClick={() => handleInsertLatex(`$${sym}$`)}
                  className="flex flex-col items-center justify-center bg-pure-surface hover:bg-surface-container border border-whisper-border min-w-[50px] py-1 rounded-lg text-xs transition-all duration-150 active:scale-[0.96] cursor-pointer text-center"
                >
                  <div className="scale-100 select-none h-6 flex items-center justify-center">
                    <LatexPreview content={`$${sym}$`} />
                  </div>
                  <span className="text-[9px] text-on-surface-variant font-mono mt-0.5">
                    {sym}
                  </span>
                </button>
              ))}
            </div>
          </div>
        </div>
      )}

      {/* Textarea */}
      <textarea
        ref={textareaRef}
        className={`w-full ${minHeight} bg-pure-surface px-4 py-3 text-[14px] text-on-surface placeholder:text-outline border-none focus:ring-0 resize-y outline-none`}
        placeholder={placeholder}
        value={value}
        onChange={onChange}
      />
    </div>
  );
}

import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import QuestionOcrUploadDrawer, { stabilizeSelectedFile } from "./QuestionOcrUploadDrawer";

afterEach(() => {
  cleanup();
});

describe("QuestionOcrUploadDrawer", () => {
  it("copies the selected image before the file input is reset", () => {
    const sourceFile = new File(["image-bytes"], "question.png", {
      type: "image/png",
      lastModified: 123
    });

    const stableFile = stabilizeSelectedFile(sourceFile);

    expect(stableFile).not.toBe(sourceFile);
    expect(stableFile.name).toBe(sourceFile.name);
    expect(stableFile.type).toBe(sourceFile.type);
    expect(stableFile.size).toBe(sourceFile.size);
    expect(stableFile.lastModified).toBe(sourceFile.lastModified);
  });

  it("enables 'Quét tạo bản nháp' button when a file is selected and does not render pre-scan crop", () => {
    const mockFile = new File(["dummy"], "de-thi.jpg", { type: "image/jpeg" });

    render(
      <QuestionOcrUploadDrawer
        isOpen={true}
        onClose={vi.fn()}
        ocrFile={mockFile}
        ocrPreviewUrl="blob:http://localhost/dummy-preview"
        ocrScanning={false}
        ocrScanError=""
        onFileSelect={vi.fn()}
        onFileClear={vi.fn()}
        onScan={vi.fn()}
        isOcrBusy={false}
      />
    );

    const scanBtn = screen.getByRole("button", { name: /Quét tạo bản nháp/i });
    expect(scanBtn).not.toBeDisabled();

    // Verify removed pre-scan crop text
    expect(screen.queryByText(/Dùng toàn bộ ảnh/i)).not.toBeInTheDocument();
  });
});

import { afterEach, describe, expect, it, vi } from "vitest";
import { createCroppedImageFile } from "./QuestionEditorPage";

describe("createCroppedImageFile", () => {
  const originalImage = globalThis.Image;
  const originalCreateObjectUrl = URL.createObjectURL;
  const originalRevokeObjectUrl = URL.revokeObjectURL;
  const originalCreateElement = document.createElement.bind(document);

  afterEach(() => {
    globalThis.Image = originalImage;
    URL.createObjectURL = originalCreateObjectUrl;
    URL.revokeObjectURL = originalRevokeObjectUrl;
    document.createElement = originalCreateElement;
    vi.restoreAllMocks();
  });

  it("uses the already loaded preview URL and does not revoke it", async () => {
    const file = new File(["source"], "question.png", { type: "image/png" });
    const previewUrl = "blob:preview-url";
    const createObjectUrl = vi.fn(() => "blob:temporary-url");
    const revokeObjectUrl = vi.fn();
    const drawImage = vi.fn();
    const canvas = {
      width: 0,
      height: 0,
      getContext: vi.fn(() => ({ drawImage })),
      toBlob: vi.fn((callback) => callback(new Blob(["crop"], { type: "image/png" }))),
    };

    URL.createObjectURL = createObjectUrl;
    URL.revokeObjectURL = revokeObjectUrl;
    document.createElement = vi.fn((tagName, options) => {
      if (tagName === "canvas") return canvas;
      return originalCreateElement(tagName, options);
    });
    globalThis.Image = class {
      naturalWidth = 576;
      naturalHeight = 196;

      set src(value) {
        this.source = value;
        queueMicrotask(() => this.onload());
      }
    };

    const result = await createCroppedImageFile(
      file,
      { x: 0, y: 0, width: 1, height: 1 },
      previewUrl
    );

    expect(result).toBeInstanceOf(File);
    expect(createObjectUrl).not.toHaveBeenCalled();
    expect(revokeObjectUrl).not.toHaveBeenCalled();
    expect(drawImage).toHaveBeenCalledOnce();
  });
});

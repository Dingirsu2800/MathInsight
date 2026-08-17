import { describe, expect, it } from "vitest";
import { stabilizeSelectedFile } from "./QuestionOcrUploadDrawer";

describe("QuestionOcrUploadDrawer file handling", () => {
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
});

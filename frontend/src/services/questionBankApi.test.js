import { describe, expect, it, vi } from "vitest";

const client = {
  delete: vi.fn(),
};

vi.mock("./questionBankApiClient", () => ({ default: client }));

const { questionBankApi } = await import("./questionBankApi");

describe("questionBankApi tag deletion", () => {
  it("deletes a topic using the expert tag endpoint", () => {
    questionBankApi.deleteTopic("topic-1");

    expect(client.delete).toHaveBeenCalledWith("/api/question-bank/tags/topics/topic-1");
  });

  it("deletes a difficulty using the expert tag endpoint", () => {
    questionBankApi.deleteDifficulty("difficulty-1");

    expect(client.delete).toHaveBeenCalledWith("/api/question-bank/tags/difficulties/difficulty-1");
  });
});

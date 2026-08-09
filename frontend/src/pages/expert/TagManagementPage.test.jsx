import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import TagManagementPage from "./TagManagementPage";
import { questionBankApi } from "../../services/questionBankApi";

vi.mock("./ExpertLayout", () => ({ default: ({ children }) => <main>{children}</main> }));
vi.mock("../../components/layout/DashboardPageHeader", () => ({ default: ({ children, title }) => <section><h1>{title}</h1>{children}</section> }));
vi.mock("../../components/ui/button", () => ({ Button: ({ children, ...props }) => <button {...props}>{children}</button> }));
vi.mock("../../components/ui/custom-select", () => ({
  CustomSelect: ({ value, onValueChange }) => <select value={value} onChange={(event) => onValueChange(event.target.value)} />
}));
vi.mock("../../components/ui/dialog", () => ({
  Dialog: ({ isOpen, children }) => isOpen ? <div role="dialog">{children}</div> : null,
  DialogHeader: ({ children }) => <div>{children}</div>,
  DialogTitle: ({ children }) => <h2>{children}</h2>,
  DialogDescription: ({ children }) => <p>{children}</p>,
  DialogContent: ({ children }) => <div>{children}</div>,
  DialogFooter: ({ children }) => <div>{children}</div>
}));
vi.mock("../../services/questionBankApi", () => ({
  questionBankApi: {
    getTopicTags: vi.fn(),
    getDifficulties: vi.fn(),
    deleteTopic: vi.fn(),
    deleteDifficulty: vi.fn(),
    updateTopic: vi.fn(),
    updateDifficulty: vi.fn(),
    createTopic: vi.fn(),
    createDifficulty: vi.fn()
  }
}));

const topic = {
  tagId: "topic-1",
  tagName: "Đại số",
  description: "",
  grade: 10,
  displayOrder: 1,
  isActive: true,
  children: []
};

const difficulty = {
  difficultyId: "difficulty-1",
  difficultyName: "Khó",
  description: "",
  levelValue: 3,
  displayOrder: 3,
  isActive: true
};

beforeEach(() => {
  window.scrollTo = vi.fn();
  questionBankApi.getTopicTags.mockResolvedValue({ data: [topic] });
  questionBankApi.getDifficulties.mockResolvedValue({ data: [] });
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("TagManagementPage deletion", () => {
  it("does not offer a second delete action for a tag that is already inactive", async () => {
    questionBankApi.getTopicTags.mockResolvedValue({ data: [{ ...topic, isActive: false }] });

    render(<TagManagementPage />);

    await screen.findByText("Đại số");
    expect(screen.queryByRole("button", { name: "Xóa chủ đề Đại số" })).not.toBeInTheDocument();
  });

  it("confirms then soft-deletes a topic through the API", async () => {
    questionBankApi.deleteTopic.mockResolvedValue({ data: { deleteMode: "SoftDeleted" } });

    render(<TagManagementPage />);
    fireEvent.click(await screen.findByRole("button", { name: "Xóa chủ đề Đại số" }));

    expect(screen.getByText(/Đây là thao tác ngừng sử dụng/)).toBeVisible();
    fireEvent.click(screen.getByRole("button", { name: "Ngừng sử dụng" }));

    await waitFor(() => expect(questionBankApi.deleteTopic).toHaveBeenCalledWith("topic-1"));
    expect(await screen.findByText(/Các câu hỏi cũ vẫn giữ dữ liệu lịch sử/)).toBeVisible();
  });

  it("confirms then soft-deletes a difficulty through the API", async () => {
    questionBankApi.getDifficulties.mockResolvedValue({ data: [difficulty] });
    questionBankApi.deleteDifficulty.mockResolvedValue({ data: { deleteMode: "SoftDeleted" } });

    render(<TagManagementPage />);
    fireEvent.click(screen.getByRole("button", { name: "Độ khó" }));
    fireEvent.click(await screen.findByRole("button", { name: "Xóa độ khó Khó" }));
    fireEvent.click(screen.getByRole("button", { name: "Ngừng sử dụng" }));

    await waitFor(() => expect(questionBankApi.deleteDifficulty).toHaveBeenCalledWith("difficulty-1"));
  });

  it("sends only one delete request while the confirmation is pending", async () => {
    let resolveDelete;
    questionBankApi.deleteTopic.mockReturnValue(new Promise((resolve) => { resolveDelete = resolve; }));

    render(<TagManagementPage />);
    fireEvent.click(await screen.findByRole("button", { name: "Xóa chủ đề Đại số" }));
    const confirmButton = screen.getByRole("button", { name: "Ngừng sử dụng" });
    fireEvent.click(confirmButton);

    await waitFor(() => expect(confirmButton).toBeDisabled());
    fireEvent.click(confirmButton);
    expect(questionBankApi.deleteTopic).toHaveBeenCalledTimes(1);

    resolveDelete({ data: { deleteMode: "SoftDeleted" } });
  });

  it("surfaces the server conflict when a topic still has active descendants", async () => {
    questionBankApi.deleteTopic.mockRejectedValue({
      response: { status: 409, data: { message: "Topic has active descendant topics." } }
    });

    render(<TagManagementPage />);
    fireEvent.click(await screen.findByRole("button", { name: "Xóa chủ đề Đại số" }));
    fireEvent.click(screen.getByRole("button", { name: "Ngừng sử dụng" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Topic has active descendant topics.");
    expect(questionBankApi.deleteTopic).toHaveBeenCalledWith("topic-1");
  });

  it("refreshes safely when another Expert has already deleted the tag", async () => {
    questionBankApi.deleteTopic.mockRejectedValue({ response: { status: 404 } });

    render(<TagManagementPage />);
    fireEvent.click(await screen.findByRole("button", { name: "Xóa chủ đề Đại số" }));
    fireEvent.click(screen.getByRole("button", { name: "Ngừng sử dụng" }));

    expect(await screen.findByText("Tag không còn tồn tại; danh sách đã được làm mới.")).toBeVisible();
    expect(questionBankApi.getTopicTags).toHaveBeenCalledTimes(2);
  });
});

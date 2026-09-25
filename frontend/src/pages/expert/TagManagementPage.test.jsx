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

beforeEach(() => {
  window.scrollTo = vi.fn();
  questionBankApi.getTopicTags.mockResolvedValue({ data: [topic] });
  questionBankApi.getDifficulties.mockResolvedValue({ data: [] });
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("TagManagementPage", () => {
  it("shows topic management without a difficulty management tab", async () => {
    render(<TagManagementPage />);

    await screen.findByText("Đại số");
    expect(screen.getByRole("heading", { name: "Quản lý chủ đề" })).toBeVisible();
    expect(screen.queryByRole("button", { name: "Độ khó" })).not.toBeInTheDocument();
    expect(questionBankApi.getDifficulties).not.toHaveBeenCalled();
  });

  it("uses the status switch instead of a misleading delete button", async () => {
    render(<TagManagementPage />);

    await screen.findByText("Đại số");
    expect(screen.getByRole("switch", { name: "Ngừng sử dụng chủ đề Đại số" })).toBeVisible();
    expect(screen.queryByRole("button", { name: "Xóa chủ đề Đại số" })).not.toBeInTheDocument();
  });

  it("confirms deactivation through the status switch", async () => {
    questionBankApi.updateTopic.mockResolvedValue({ data: { isActive: false } });

    render(<TagManagementPage />);
    fireEvent.click(await screen.findByRole("switch", { name: "Ngừng sử dụng chủ đề Đại số" }));
    expect(screen.getByText(/Các câu hỏi hiện tại đang gắn chủ đề này vẫn được giữ nguyên/)).toBeVisible();
    fireEvent.click(screen.getByRole("button", { name: "Ngừng sử dụng" }));

    await waitFor(() => expect(questionBankApi.updateTopic).toHaveBeenCalledWith("topic-1", expect.objectContaining({ isActive: false })));
    expect(questionBankApi.deleteTopic).not.toHaveBeenCalled();
  });
});

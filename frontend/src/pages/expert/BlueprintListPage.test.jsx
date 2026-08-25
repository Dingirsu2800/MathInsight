import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import BlueprintListPage from './BlueprintListPage';
import { testGeneratorApi } from '../../services/testGeneratorApi';

vi.mock('../../services/testGeneratorApi', () => ({
  testGeneratorApi: {
    getBlueprints: vi.fn(),
    cloneBlueprint: vi.fn(),
  },
}));

vi.mock('./ExpertLayout', () => ({
  default: ({ children }) => <div data-testid="expert-layout">{children}</div>,
}));

vi.mock('../../components/layout/DashboardLayout', () => ({
  default: ({ children }) => <div data-testid="dashboard-layout">{children}</div>,
}));

afterEach(() => {
  cleanup();
  vi.resetAllMocks();
});

describe('BlueprintListPage pinning', () => {
  const sampleItems = [
    { blueprintId: 'bp-1', blueprintName: 'Đề thi khảo sát số 1', grade: 12, status: 'Draft', totalQuestions: 40, durationMinutes: 90, totalScore: 10 },
    { blueprintId: 'bp-2', blueprintName: 'Đề thi vừa tạo mới', grade: 12, status: 'Draft', totalQuestions: 40, durationMinutes: 90, totalScore: 10 },
    { blueprintId: 'bp-3', blueprintName: 'Đề thi khảo sát số 3', grade: 12, status: 'Draft', totalQuestions: 40, durationMinutes: 90, totalScore: 10 },
  ];

  it('pins newly created blueprint to the top with "Vừa tạo" badge when router state is provided', async () => {
    testGeneratorApi.getBlueprints.mockResolvedValue({
      data: {
        items: sampleItems,
        totalCount: 3,
        pageIndex: 1,
        pageSize: 10,
        totalPages: 1,
      },
    });

    render(
      <MemoryRouter initialEntries={[{ pathname: '/expert/blueprints', state: { newlyCreatedBlueprintId: 'bp-2' } }]}>
        <BlueprintListPage />
      </MemoryRouter>
    );

    expect(await screen.findByText('Đề thi vừa tạo mới')).toBeInTheDocument();
    expect(screen.getByText('Vừa tạo')).toBeInTheDocument();

    const titleButtons = screen.getAllByRole('button', { name: /Đề thi/i });
    expect(titleButtons[0]).toHaveTextContent('Đề thi vừa tạo mới');
  });

  it('renders canonical list order when no router state is present', async () => {
    testGeneratorApi.getBlueprints.mockResolvedValue({
      data: {
        items: sampleItems,
        totalCount: 3,
        pageIndex: 1,
        pageSize: 10,
        totalPages: 1,
      },
    });

    render(
      <MemoryRouter initialEntries={['/expert/blueprints']}>
        <BlueprintListPage />
      </MemoryRouter>
    );

    expect(await screen.findByText('Đề thi khảo sát số 1')).toBeInTheDocument();
    expect(screen.queryByText('Vừa tạo')).not.toBeInTheDocument();

    const titleButtons = screen.getAllByRole('button', { name: /Đề thi/i });
    expect(titleButtons[0]).toHaveTextContent('Đề thi khảo sát số 1');
  });
});

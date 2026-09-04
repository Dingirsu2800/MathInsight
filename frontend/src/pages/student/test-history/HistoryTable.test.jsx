import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import HistoryTable, { toEndOfDayIso, toStartOfDayIso } from './HistoryTable';
import { getSessionHistory } from '../../../services/gradingApi';

vi.mock('../../../services/gradingApi', () => ({
  getSessionHistory: vi.fn(),
}));

afterEach(() => {
  cleanup();
  vi.resetAllMocks();
});

describe('HistoryTable date conversion helpers', () => {
  it('toStartOfDayIso converts YYYY-MM-DD to start of day ISO', () => {
    const iso = toStartOfDayIso('2026-09-01');
    expect(iso).toBeDefined();
    const d = new Date(iso);
    expect(d.getHours()).toBeDefined();
  });

  it('toEndOfDayIso converts YYYY-MM-DD to end of day ISO', () => {
    const startIso = toStartOfDayIso('2026-09-03');
    const endIso = toEndOfDayIso('2026-09-03');
    expect(new Date(endIso).getTime()).toBeGreaterThan(new Date(startIso).getTime());
  });
});

describe('HistoryTable component', () => {
  beforeEach(() => {
    getSessionHistory.mockResolvedValue({
      items: [
        {
          sessionId: 'session-12345678-abcd',
          testName: 'Đề thi khảo sát',
          testFormat: 'Exam',
          submittedAt: '2026-09-03T14:30:00Z',
          durationMinutes: 45,
          numCorrect: 8,
          numIncorrect: 2,
          numAbandoned: 0,
          score: 8.0,
          submissionType: 'StudentSubmit',
        },
      ],
      totalCount: 1,
      totalPages: 1,
      page: 1,
    });
  });

  it('passes date boundaries to getSessionHistory and displays row', async () => {
    render(
      <HistoryTable
        filters={{
          testFormat: 'Exam',
          fromDate: '2026-09-01',
          toDate: '2026-09-03',
        }}
        onViewDetail={vi.fn()}
      />
    );

    expect(await screen.findByText('Đề thi khảo sát')).toBeVisible();
    expect(screen.getByText('8.0')).toBeVisible();

    expect(getSessionHistory).toHaveBeenCalledWith(
      expect.objectContaining({
        testFormat: 'Exam',
        fromDate: expect.stringMatching(/^\d{4}-\d{2}-\d{2}T/),
        toDate: expect.stringMatching(/^\d{4}-\d{2}-\d{2}T/),
      })
    );
  });
});

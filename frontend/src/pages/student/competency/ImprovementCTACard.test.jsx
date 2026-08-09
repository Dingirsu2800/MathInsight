import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import ImprovementCTACard from './ImprovementCTACard';
import { getRecommendedMaterials } from '../../../services/recommenderApi';

vi.mock('../../../services/recommenderApi', () => ({
  getRecommendedMaterials: vi.fn(),
}));

afterEach(() => {
  cleanup();
  vi.resetAllMocks();
});

describe('ImprovementCTACard', () => {
  it('renders an API material as a safe external link', async () => {
    getRecommendedMaterials.mockResolvedValue([
      {
        materialId: 'material-1',
        title: 'Luyen tap ham so',
        fileUrl: 'https://cdn.example.test/material-1.pdf',
        officialPoint: 4.5,
        isRemedial: true,
      },
    ]);

    render(<ImprovementCTACard />);

    const link = await screen.findByRole('link', { name: 'Luyen tap ham so' });
    expect(link).toHaveAttribute('href', 'https://cdn.example.test/material-1.pdf');
    expect(link).toHaveAttribute('target', '_blank');
    expect(link).toHaveAttribute('rel', 'noopener noreferrer');
  });

  it('shows an empty state when the completed API response has no materials', async () => {
    getRecommendedMaterials.mockResolvedValue([]);

    render(<ImprovementCTACard />);

    expect(await screen.findByTestId('recommendation-materials-empty')).toBeVisible();
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
  });

  it('shows an error state when the recommendation request fails', async () => {
    getRecommendedMaterials.mockRejectedValue(new Error('network failure'));

    render(<ImprovementCTACard />);

    expect(await screen.findByTestId('recommendation-materials-error')).toBeVisible();
  });

  it('does not create a link for an unsafe material URL', async () => {
    getRecommendedMaterials.mockResolvedValue([
      {
        materialId: 'material-unsafe',
        title: 'Unsafe material',
        fileUrl: 'javascript:alert(1)',
        officialPoint: 4.5,
        isRemedial: true,
      },
    ]);

    render(<ImprovementCTACard />);

    expect(await screen.findByText('Unsafe material')).toBeVisible();
    expect(screen.queryByRole('link', { name: 'Unsafe material' })).not.toBeInTheDocument();
  });
});

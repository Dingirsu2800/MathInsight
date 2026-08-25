import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import BlueprintTopicPicker from './BlueprintTopicPicker';

afterEach(() => {
  cleanup();
});

describe('BlueprintTopicPicker', () => {
  const sampleTopics = [
    { tagId: 'topic-root-1', name: 'Đại số 12', depth: 0 },
    { tagId: 'topic-child-1', name: 'Hàm số và đồ thị', depth: 1 },
    { tagId: 'topic-child-2', name: 'Tích phân', depth: 1 },
  ];

  it('renders root topic as disabled header item and child topic as selectable', () => {
    const onValueChange = vi.fn();
    render(
      <BlueprintTopicPicker
        topics={sampleTopics}
        value=""
        onValueChange={onValueChange}
      />
    );

    // Open the dropdown
    const trigger = screen.getByRole('combobox');
    fireEvent.click(trigger);

    // Verify root is rendered as disabled
    const rootOption = screen.getByText(/Đại số 12/i);
    expect(rootOption.closest('[role="option"]')).toHaveAttribute('data-disabled');

    // Verify child is selectable
    const childOption = screen.getByText(/Hàm số và đồ thị/i);
    expect(childOption.closest('[role="option"]')).not.toHaveAttribute('data-disabled');
  });
});

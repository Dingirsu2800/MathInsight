import React, { useState, useRef, useEffect, useId } from "react";
import { cn } from "../../utils/cn";

export default function BlueprintTopicPicker({
  value,
  onValueChange,
  topics = [],
  placeholder = "Chọn chủ đề...",
  className,
  disabled = false
}) {
  const [isOpen, setIsOpen] = useState(false);
  const [searchQuery, setSearchQuery] = useState("");
  const [focusedIndex, setFocusedIndex] = useState(-1);
  const listboxId = useId();

  const containerRef = useRef(null);
  const triggerRef = useRef(null);
  const searchInputRef = useRef(null);
  const listRef = useRef(null);

  // Find the selected topic in the flat topics list
  const selectedTopic = topics.find((t) => String(t.tagId || t.id) === String(value));

  // Filter topics based on search query (case-insensitive, tone-insensitive or basic Vietnamese matching)
  const filteredTopics = topics.filter((topic) => {
    if (!searchQuery.trim()) return true;
    const name = (topic.name || topic.tagName || "").toLowerCase();
    const query = searchQuery.toLowerCase();
    return name.includes(query);
  });

  // Handle open/close toggle
  const toggleOpen = () => {
    if (disabled) return;
    setIsOpen((prev) => !prev);
  };

  // Close helper
  const closePopover = () => {
    setIsOpen(false);
    setSearchQuery("");
    setFocusedIndex(-1);
    // Return focus to trigger
    triggerRef.current?.focus();
  };

  // Click outside listener
  useEffect(() => {
    const handleClickOutside = (event) => {
      if (containerRef.current && !containerRef.current.contains(event.target)) {
        setIsOpen(false);
        setSearchQuery("");
        setFocusedIndex(-1);
      }
    };
    if (isOpen) {
      document.addEventListener("mousedown", handleClickOutside);
    }
    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
    };
  }, [isOpen]);

  // Focus input when popover opens
  useEffect(() => {
    if (isOpen) {
      setTimeout(() => {
        searchInputRef.current?.focus();
      }, 50);
    }
  }, [isOpen]);

  const moveFocus = (direction) => {
    setFocusedIndex((prev) => {
      const next = prev + direction;
      if (direction > 0) {
        return next < filteredTopics.length ? next : prev;
      }
      return next >= 0 ? next : 0;
    });
  };

  const selectFocusedTopic = () => {
    if (focusedIndex >= 0 && focusedIndex < filteredTopics.length) {
      const selected = filteredTopics[focusedIndex];
      onValueChange(selected.tagId || selected.id);
      closePopover();
    }
  };

  // Trigger supports Space as an activation key. Search input must keep Space for text entry.
  const handleTriggerKeyDown = (e) => {
    if (!isOpen) {
      if (e.key === "Enter" || e.key === " " || e.key === "ArrowDown") {
        e.preventDefault();
        setIsOpen(true);
      }
      return;
    }

    switch (e.key) {
      case "Escape":
        e.preventDefault();
        closePopover();
        break;

      case "ArrowDown":
        e.preventDefault();
        moveFocus(1);
        break;

      case "ArrowUp":
        e.preventDefault();
        moveFocus(-1);
        break;

      case "Enter":
      case " ":
        e.preventDefault();
        selectFocusedTopic();
        break;

      default:
        break;
    }
  };

  const handleSearchKeyDown = (e) => {
    switch (e.key) {
      case "Escape":
        e.preventDefault();
        closePopover();
        break;
      case "ArrowDown":
        e.preventDefault();
        moveFocus(1);
        break;
      case "ArrowUp":
        e.preventDefault();
        moveFocus(-1);
        break;
      case "Enter":
        e.preventDefault();
        selectFocusedTopic();
        break;
      default:
        break;
    }
  };

  // Scroll focused option into view
  useEffect(() => {
    if (focusedIndex >= 0 && listRef.current) {
      const activeEl = listRef.current.children[focusedIndex];
      if (activeEl) {
        activeEl.scrollIntoView({ block: "nearest" });
      }
    }
  }, [focusedIndex]);

  return (
    <div ref={containerRef} className={cn("relative w-full", className)}>
      {/* Trigger Button */}
      <button
        ref={triggerRef}
        type="button"
        disabled={disabled}
        onClick={toggleOpen}
        onKeyDown={handleTriggerKeyDown}
        aria-haspopup="listbox"
        aria-expanded={isOpen}
        aria-controls={listboxId}
        className={cn(
          "flex h-10 w-full items-center justify-between rounded-xl border border-outline-variant hover:border-outline-variant/80 bg-surface-container-lowest px-3 py-2 text-xs text-on-surface text-left focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary disabled:cursor-not-allowed disabled:opacity-50 transition-all select-none cursor-pointer",
          { "ring-2 ring-primary/20 border-primary": isOpen }
        )}
      >
        <span className="truncate pr-2">
          {selectedTopic ? (selectedTopic.name || selectedTopic.tagName) : placeholder}
        </span>
        <span className="material-symbols-outlined text-[16px] text-on-surface-variant font-bold leading-none select-none">
          keyboard_arrow_down
        </span>
      </button>

      {/* Popover Dropdown */}
      {isOpen && (
        <div className="absolute z-50 mt-1 w-full min-w-[280px] rounded-xl border border-outline-variant bg-pure-surface text-on-surface shadow-lg overflow-hidden flex flex-col max-h-72">
          {/* Search Box */}
          <div className="p-2 border-b border-whisper-border bg-surface-container-low flex items-center gap-1.5 relative">
            <span className="material-symbols-outlined text-[18px] text-on-surface-variant pl-1">
              search
            </span>
            <input
              ref={searchInputRef}
              type="text"
              placeholder="Tìm kiếm chủ đề..."
              value={searchQuery}
              onChange={(e) => {
                setSearchQuery(e.target.value);
                setFocusedIndex(0);
              }}
              onKeyDown={handleSearchKeyDown}
              className="flex-1 bg-transparent border-0 p-1 text-xs text-on-surface placeholder:text-on-surface-variant focus:outline-none focus:ring-0"
            />
            {searchQuery && (
              <button
                type="button"
                onClick={() => {
                  setSearchQuery("");
                  searchInputRef.current?.focus();
                }}
                aria-label="Xóa văn bản tìm kiếm"
                className="p-0.5 hover:bg-surface-container-high rounded-full flex items-center justify-center cursor-pointer"
              >
                <span className="material-symbols-outlined text-[16px] text-on-surface-variant">
                  close
                </span>
              </button>
            )}
          </div>

          {/* Listbox */}
          <ul
            ref={listRef}
            id={listboxId}
            role="listbox"
            className="flex-1 overflow-y-auto p-1 max-h-56"
          >
            {filteredTopics.length === 0 ? (
              <li className="py-3 px-3 text-center text-xs text-on-surface-variant italic select-none">
                Không tìm thấy chủ đề nào
              </li>
            ) : (
              filteredTopics.map((topic, idx) => {
                const isSelected = String(topic.tagId || topic.id) === String(value);
                const isFocused = idx === focusedIndex;
                const depth = topic.depth || 0;

                return (
                  <li
                    key={topic.tagId || topic.id}
                    role="option"
                    aria-selected={isSelected}
                    onClick={() => {
                      onValueChange(topic.tagId || topic.id);
                      closePopover();
                    }}
                    onMouseEnter={() => setFocusedIndex(idx)}
                    className={cn(
                      "relative flex w-full cursor-pointer select-none items-center rounded-lg py-2 pl-3 pr-8 text-xs text-on-surface outline-none transition-colors",
                      {
                        "bg-surface-container-low text-primary font-semibold": isSelected,
                        "bg-surface-container-high text-primary": isFocused && !isSelected,
                        "bg-transparent hover:bg-surface-container-low/50": !isSelected && !isFocused
                      }
                    )}
                    style={{ paddingLeft: `${12 + depth * 16}px` }}
                  >
                    <span className="truncate">{topic.name || topic.tagName}</span>
                    {isSelected && (
                      <span className="absolute right-2.5 flex h-3.5 w-3.5 items-center justify-center">
                        <span className="material-symbols-outlined text-[14px] font-bold text-primary">
                          check
                        </span>
                      </span>
                    )}
                  </li>
                );
              })
            )}
          </ul>
        </div>
      )}
    </div>
  );
}

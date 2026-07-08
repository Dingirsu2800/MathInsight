import React from "react";
import * as SelectPrimitive from "@radix-ui/react-select";
import { cn } from "../../utils/cn";

export const CustomSelect = React.forwardRef(
  ({ value, onValueChange, placeholder, items = [], className, disabled }, ref) => {
    // Normalize items to array of { value, label }
    const formattedItems = items.map(item => {
      if (typeof item === "string" || typeof item === "number") {
        return { value: item.toString(), label: item.toString() };
      }
      return {
        value: item.value?.toString() || "",
        label: item.label?.toString() || item.name?.toString() || ""
      };
    });

    const activeItem = formattedItems.find(item => item.value === value);

    return (
      <SelectPrimitive.Root
        value={value}
        onValueChange={onValueChange}
        disabled={disabled}
      >
        <SelectPrimitive.Trigger
          ref={ref}
          className={cn(
            "flex h-10 w-full items-center justify-between rounded-xl border border-outline-variant bg-surface-container-lowest px-3 py-2 text-xs text-on-surface placeholder:text-on-surface-variant focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary disabled:cursor-not-allowed disabled:opacity-50 transition-all select-none text-left cursor-pointer",
            className
          )}
        >
          <SelectPrimitive.Value>
            {activeItem ? activeItem.label : (placeholder || "Chọn...")}
          </SelectPrimitive.Value>
          <SelectPrimitive.Icon asChild>
            <span className="material-symbols-outlined text-[16px] text-on-surface-variant font-bold leading-none select-none">
              keyboard_arrow_down
            </span>
          </SelectPrimitive.Icon>
        </SelectPrimitive.Trigger>

        <SelectPrimitive.Portal>
          <SelectPrimitive.Content
            className="relative z-50 min-w-[8rem] overflow-hidden rounded-xl border border-outline-variant bg-pure-surface text-on-surface shadow-md data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95 data-[side=bottom]:slide-in-from-top-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2"
            position="popper"
            sideOffset={4}
          >
            <SelectPrimitive.Viewport
              className="p-1 min-w-[var(--radix-select-trigger-width)] max-h-60 overflow-y-auto"
            >
              {formattedItems.length === 0 ? (
                <div className="py-2 px-3 text-center text-xs text-on-surface-variant italic select-none">
                  Không có lựa chọn nào
                </div>
              ) : (
                formattedItems.map((item, idx) => (
                  <SelectPrimitive.Item
                    key={idx}
                    value={item.value}
                    className="relative flex w-full cursor-pointer select-none items-center rounded-lg py-2 pl-3 pr-8 text-xs text-on-surface outline-none focus:bg-surface-container-low focus:text-primary data-[disabled]:pointer-events-none data-[disabled]:opacity-50 transition-colors"
                  >
                    <SelectPrimitive.ItemText>{item.label}</SelectPrimitive.ItemText>
                    <SelectPrimitive.ItemIndicator className="absolute right-2.5 flex h-3.5 w-3.5 items-center justify-center">
                      <span className="material-symbols-outlined text-[14px] font-bold text-primary">
                        check
                      </span>
                    </SelectPrimitive.ItemIndicator>
                  </SelectPrimitive.Item>
                ))
              )}
            </SelectPrimitive.Viewport>
          </SelectPrimitive.Content>
        </SelectPrimitive.Portal>
      </SelectPrimitive.Root>
    );
  }
);

CustomSelect.displayName = "CustomSelect";

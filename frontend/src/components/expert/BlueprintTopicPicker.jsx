import React from "react";
import { CustomSelect } from "../ui/custom-select";
import { cn } from "../../utils/cn";

export default function BlueprintTopicPicker({
  value,
  onValueChange,
  topics = [],
  placeholder = "Chọn chủ đề...",
  className,
  disabled = false
}) {
  const items = topics.map((topic) => {
    const topicId = String(topic.tagId || topic.id || "");
    const topicName = topic.name || topic.tagName || "";
    const depth = Number(topic.depth) || 0;

    return {
      value: topicId,
      label: `${"\u00A0\u00A0".repeat(depth)}${topicName}`
    };
  });

  return (
    <CustomSelect
      value={value || ""}
      onValueChange={onValueChange}
      placeholder={placeholder}
      items={items}
      disabled={disabled}
      className={cn(className)}
    />
  );
}

# Feature Specification: Test Generator Module

**Feature Branch**: `009-test-generator`

**Created**: 2026-06-23 | **Updated**: 2026-07-04

**Status**: Approved

**Source Documents**: PRD §4 (FT-03), UCS UC-40–UC-46, TDS §2.2 (TestGen layer), §2.4

## User Scenarios & Testing *(mandatory)*

### Core Use Cases (Priority: P1)

| UC-ID | Name | Primary Actor | Notes |
|-------|------|---------------|-------|
| UC-40 | View Pending Blueprints | Expert | List blueprints in `PENDING_REVIEW` created by others |
| UC-41 | Approve / Reject Blueprint | Expert | Expert peer-review workflow |
| UC-42 | Manage Blueprint | Expert | List, search, filter own + all blueprints |
| UC-43 | Create Blueprint | Expert | Design sectioned topic/difficulty distribution |
| UC-44 | Clone Blueprint | Expert | Deep-copy independent instance |
| UC-45 | Update Blueprint | Expert | Only DRAFT or REJECTED blueprints |
| UC-46 | Delete Blueprint | Expert | Hard-delete if DRAFT/unused; soft-delete if used |

### Edge Cases

- **Self-Approval Blocked** (BR-Blueprint-01): Expert cannot view or approve their own submitted blueprints in pending list.
- **Sum Verification** (BR-07): Blueprint section totals must sum to `blueprint.total_questions`; each section's detail quantities must sum to `section.total_questions`.
- **Active Locking**: Blueprints associated with generated `Test` records cannot be modified or hard-deleted.
- **Empty Matrix**: Blueprint with zero `BlueprintSection` or zero `BlueprintDetail` rows → cannot submit for review.
- **Legacy Multiple Choice Blueprint**: Old/pre-2025 style tests are represented as one default `BlueprintSection` with `question_type = SingleChoice`.
- **Clone of non-existent**: Clone a deleted blueprint → 404.

## Requirements *(mandatory)*

### Functional Requirements

- **BR-Blueprint-01**: Experts cannot review or approve blueprints they created. Pending list excludes creator's own blueprints (filter `expert_id != currentUserId`).
- **BR-Blueprint-02**: Blueprint review action requires `status = PENDING_REVIEW`. Attempt to review a DRAFT/APPROVED → 422.
- **BR-Blueprint-03**: Rejection requires a **non-empty** `review_note` describing corrective actions.
- **BR-07**: Blueprint quantity validation has two levels: `SUM(BlueprintSection.total_questions) == Blueprint.total_questions`, and for each section `SUM(BlueprintDetail.quantity) == BlueprintSection.total_questions`. Validation runs on submit-for-review and on update.
- **BR-09**: Cloning creates a **completely independent** entity with a new UUID. Changes to the clone do not affect the original.
- **BR-47**: Only blueprints with `status = DRAFT` or `REJECTED` can be updated (UC-45). `APPROVED`, `PENDING_REVIEW`, and `ACTIVE` blueprints are locked.
- **BR-48**: Blueprints with `status = APPROVED` that have been used to generate tests transition to `ACTIVE` status — they cannot be modified or hard-deleted.
- **BR-49**: A blueprint must contain at least one `BlueprintSection`. Each `BlueprintDetail` must belong to exactly one `BlueprintSection`.
- **BR-50**: `BlueprintSection.question_type` constrains candidate questions during generation. TestGen must filter candidates by matching `Question.question_type`.
- **BR-51**: `Composite` sections must define `part_count_per_question` and `default_point_per_part`. Non-composite sections must leave these fields null.
- **BR-52**: Experts do not directly create/update/delete `Test` records in MVP. Experts manage `Question` and `Blueprint` data; the backend `GenerationEngine` creates `Test` and `TestQuestion` records.
- **BR-53**: `Test.test_code` is optional. Generate it only for shareable/code-entry tests; personal adaptive/recommendation tests keep `test_code = NULL`.
- **Dynamic WeakTag Adjustment Loop** (for `TestGen` service):
  - **WeakTag Cap**: Max **20%** of total questions in a generated test may be WeakTag-biased.
  - **Adaptive Bias Probability**: WeakTag question selection bias = **40%** per matching blueprint slot.
  - **Difficulty Downscaling**: If a blueprint detail slot specifies a `Hard` (level 3) or `Very Hard` (level 4) difficulty, and the topic is a WeakTag for the student (student's `official_point < 5.00`), the engine downscales the question selection for that slot to `Medium` (level 2) to rebuild confidence. If the slot specifies `Medium` (level 2) and the topic's `official_point < 3.00` (which maps to level 1 `Easy`), the engine downscales the question selection to `Easy` (level 1) (F2 resolution). Scale back to the original slot difficulty (e.g., Hard) only when the topic's `official_point >= 5.00` (meaning it is no longer a WeakTag) (F3 resolution).
  - **Difficulty Upscaling (Challenge Mode)**: If `official_point >= 8.0` AND `mastery_status = Mastered` → select one difficulty level higher (F6 resolution).
  - **Remedial Learning (Easy-Level Protection)**: If `official_point < 5.0` at `Easy` → bias probability drops to **10%**; no further downscaling; prioritize foundational lectures (F6 resolution).

- **Student Practice & Exam Flow Options** (BR-54):
  - **Initial Generation Formats**: Students have access to two primary test formats:
    1. **Exam Mode**: A full-length test session generated from an approved blueprint (`test_format = Exam`).
    2. **Practice Mode**: A practice session of exactly **10 questions** targeting a specific diagnosed WeakTag topic (`test_format = Practice`).
  - **Post-Exam Choices**: After completing an Exam session and receiving/updating WeakTags, the student can choose between:
    1. **Format A (Tiếp tục làm bài với cấu trúc đề giữ nguyên)**: Generate a new `Exam` test session from the same blueprint structure (retains the original slot quantities and sections, but applies standard WeakTag adjustments like Cap, Bias, and Downscale based on the student's updated WeakTag state).
    2. **Format B (Luyện tập chuỗi 10 câu liên quan đến WeakTag)**: Generate a `Practice` session of exactly 10 questions for a selected WeakTag topic, using the student's `recommended_difficulty_level` for that topic. Answers in this session will update the `practice_point` using the Elo-inspired formula and blend/reset after completing the 10-question series.

### Blueprint Status Machine

```
[Create]
    │
    ▼
  DRAFT ──(submit for review)──────▶ PENDING_REVIEW
    ▲                                      │
    │                                      ├──(approve)──▶ APPROVED ──(test generated)──▶ ACTIVE
    └──(rejection)◀─── REJECTED ◀──(reject)─┘
                            │
                            └──(Expert fixes & re-submits)──▶ PENDING_REVIEW (again)
```

| Status | Can Edit | Can Delete | Can Generate Test |
|--------|----------|------------|-------------------|
| DRAFT | Yes | Yes (hard) | No |
| PENDING_REVIEW | No | No | No |
| APPROVED | No | No | Yes |
| REJECTED | Yes | Yes (hard) | No |
| ACTIVE | No | No (soft only) | Yes |

### Key Entities *(include if feature involves data)*

- **Blueprint**: `blueprint_id` (PK), `blueprint_name` (VARCHAR 100), `grade` (10/11/12), `total_questions` (INT), `duration_minutes` (INT), `expert_id` (FK → experts), `status` (**DRAFT** | **PENDING_REVIEW** | **APPROVED** | **REJECTED** | **ACTIVE**), `review_note` (VARCHAR 255, nullable), `reviewed_by` (FK → experts, nullable), `reviewed_time` (nullable), `created_time`
- **BlueprintSection**: `blueprint_section_id` (PK), `blueprint_id` (FK), `section_order`, `section_code`, `section_name`, `question_type`, `instruction_text`, `total_questions`, `default_point_per_question`, `default_point_per_part`, `part_count_per_question`
- **BlueprintDetail**: `blueprint_detail_id` (PK), `blueprint_id`, `blueprint_section_id` (FK), `tag_id` (FK → tag_topics), `difficulty_id` (FK → tag_difficulties), `quantity` (INT) — composite UNIQUE `(blueprint_section_id, tag_id, difficulty_id)`
- **Test**: `test_id` (PK), `blueprint_id` (nullable FK), `test_mode`, `generated_for_student_id` (nullable), `generated_by` (`System` by default), `test_name`, `test_code` (nullable; unique when not null), `duration_minutes`, `total_questions`

### Enums

| Entity | Field | Allowed Values |
|--------|-------|----------------|
| Blueprint | status | `DRAFT`, `PENDING_REVIEW`, `APPROVED`, `REJECTED`, `ACTIVE` |
| Blueprint | grade | `10`, `11`, `12` |
| BlueprintSection | question_type | `SingleChoice`, `MultipleChoice`, `TrueFalse`, `ShortAnswer`, `Composite` |

## Success Criteria *(mandatory)*

### Measurable Outcomes

- Blueprint section/detail validation (sum check) runs within **500ms**.
- Pending blueprint list loads within **1.5 seconds** (NFR-P04).
- Test generation from approved blueprint completes within **3 seconds** for 40-question tests.
- Backend maps TestGen entities to the current SQL script tables; no separate `tst` schema is created for MVP.
- Self-approval is blocked 100% of the time (BR-Blueprint-01).

## Assumptions

- Database is SQL Server. Backend maps to current DB script tables (`Blueprint`, `BlueprintSection`, `BlueprintDetail`, `Test`, `TestQuestion`) instead of schema-prefixed tables.
- Expert accounts and roles verified via Identity module (001).
- `IRecommenderService.GetStudentWeakTagsAsync()` is called in-process from Recommender module (005) during test generation.
- Test generation creates `Test` + `TestQuestion` records in current DB script tables with `generated_by = System`. The source expert is derived through `Test.blueprint_id -> Blueprint.expert_id` when a blueprint is used.

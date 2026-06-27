# Feature Specification: Test Generator Module

**Feature Branch**: `009-test-generator`

**Created**: 2026-06-23 | **Updated**: 2026-06-26

**Status**: Approved

**Source Documents**: PRD §4 (FT-03), UCS UC-40–UC-46, TDS §2.2 (TestGen layer), §2.4

## User Scenarios & Testing *(mandatory)*

### Core Use Cases (Priority: P1)

| UC-ID | Name | Primary Actor | Notes |
|-------|------|---------------|-------|
| UC-40 | View Pending Blueprints | Expert | List blueprints in `PENDING_REVIEW` created by others |
| UC-41 | Approve / Reject Blueprint | Expert | Expert peer-review workflow |
| UC-42 | Manage Blueprint | Expert | List, search, filter own + all blueprints |
| UC-43 | Create Blueprint | Expert | Design topic/difficulty distribution matrix |
| UC-44 | Clone Blueprint | Expert | Deep-copy independent instance |
| UC-45 | Update Blueprint | Expert | Only DRAFT or REJECTED blueprints |
| UC-46 | Delete Blueprint | Expert | Hard-delete if DRAFT/unused; soft-delete if used |

### Edge Cases

- **Self-Approval Blocked** (BR-Blueprint-01): Expert cannot view or approve their own submitted blueprints in pending list.
- **Sum Verification** (BR-07): Blueprint matrix slot quantities must sum to exactly `total_questions`.
- **Active Locking**: Blueprints associated with generated `Test` records cannot be modified or hard-deleted.
- **Empty Matrix**: Blueprint with zero `BlueprintDetail` rows → cannot submit for review.
- **Clone of non-existent**: Clone a deleted blueprint → 404.

## Requirements *(mandatory)*

### Functional Requirements

- **BR-Blueprint-01**: Experts cannot review or approve blueprints they created. Pending list excludes creator's own blueprints (filter `expert_id != currentUserId`).
- **BR-Blueprint-02**: Blueprint review action requires `status = PENDING_REVIEW`. Attempt to review a DRAFT/APPROVED → 422.
- **BR-Blueprint-03**: Rejection requires a **non-empty** `review_note` describing corrective actions.
- **BR-07**: The sum of all `BlueprintDetail.quantity` values in a blueprint must equal exactly `blueprint.total_questions`. Validation runs on submit-for-review and on update.
- **BR-09**: Cloning creates a **completely independent** entity with a new UUID. Changes to the clone do not affect the original.
- **BR-47**: Only blueprints with `status = DRAFT` or `REJECTED` can be updated (UC-45). `APPROVED`, `PENDING_REVIEW`, and `ACTIVE` blueprints are locked.
- **BR-48**: Blueprints with `status = APPROVED` that have been used to generate tests transition to `ACTIVE` status — they cannot be modified or hard-deleted.
- **Dynamic WeakTag Adjustment Loop** (for `TestGen` service):
  - **WeakTag Cap**: Max **20%** of total questions may be WeakTag-biased.
  - **Adaptive Bias Probability**: WeakTag question selection bias = **40%** per matching blueprint slot (reduced from 70% default).
  - **Difficulty Downscaling**: If student's WeakTag is at `Hard` → select `Medium` question for that topic (rebuild confidence). Scale back to `Hard` only when `accuracy_rate > 70%` on Medium.
  - **Difficulty Upscaling (Challenge Mode)**: If `P_tag >= 8.0` AND `mastery_status = MASTERED` → select one difficulty level higher.
  - **Remedial Learning (Easy-Level Protection)**: If `P_tag < 5.0` at `Easy` → bias probability drops to **10%**; no further downscaling; prioritize foundational lectures.

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
- **BlueprintDetail**: `blueprint_detail_id` (PK), `blueprint_id` (FK), `tag_id` (FK → tag_topics), `difficulty_id` (FK → tag_difficulties), `quantity` (INT) — composite UNIQUE `(blueprint_id, tag_id, difficulty_id)`

### Enums

| Entity | Field | Allowed Values |
|--------|-------|----------------|
| Blueprint | status | `DRAFT`, `PENDING_REVIEW`, `APPROVED`, `REJECTED`, `ACTIVE` |
| Blueprint | grade | `10`, `11`, `12` |

## Success Criteria *(mandatory)*

### Measurable Outcomes

- Blueprint matrix validation (sum check) runs within **500ms**.
- Pending blueprint list loads within **1.5 seconds** (NFR-P04).
- Test generation from approved blueprint completes within **3 seconds** for 40-question tests.
- Backend maps TestGen entities to the current SQL script tables; no separate `tst` schema is created for MVP.
- Self-approval is blocked 100% of the time (BR-Blueprint-01).

## Assumptions

- Database is SQL Server. Backend maps to current DB script tables (`Blueprint`, `BlueprintDetail`, `Test`, `TestQuestion`) instead of schema-prefixed tables.
- Expert accounts and roles verified via Identity module (001).
- `IRecommenderService.GetStudentWeakTagsAsync()` is called in-process from Recommender module (005) during test generation.
- Test generation creates `Test` + `TestQuestion` records in current DB script tables.

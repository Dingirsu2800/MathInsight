# Feature Specification: Test Generator Module

**Feature Branch**: `009-test-generator`

**Created**: 2026-06-23

**Status**: Approved

**Input**: User description: "Manages blueprint lifecycle including manual and dynamic blueprint matrices creation, expert reviews/approvals workflows, cloning, and test auto-generation matching student profiles."

## User Scenarios & Testing *(mandatory)*

### Core Use Cases (Priority: P1)

- **UC-40: View Pending Blueprints**
  - Expert views a list of blueprints created by other Experts that are in `Pending_Review` status.
- **UC-41: Approve/Reject Blueprint**
  - Expert approves a pending blueprint, setting its status to `Approved`.
  - Expert rejects a pending blueprint with a rejection reason (Review Note), setting its status to `Rejected` and notifying the creator.
- **UC-42: Manage blueprint**
  - Expert lists, searches, and filters all blueprints.
- **UC-43: Create Blueprint**
  - Expert designs a new blueprint by adding matrix slots defining TagTopic, TagDifficulty, and Percentage (sum must equal 100%).
- **UC-44: Clone Blueprint**
  - Expert deep-copies a blueprint to create a new draft copy with a unique ID.
- **UC-45: Update Blueprint**
  - Expert updates a draft blueprint. Active/Approved blueprints are locked.
- **UC-46: Delete Blueprint**
  - Expert deletes a draft blueprint. If already used to generate exams, it is soft-deleted/disabled.

### Edge Cases

- **Self-Approval Blocked**: Experts cannot approve or view their own submitted blueprints in the pending list (BR-Blueprint-01).
- **Sum Verification**: Blueprint matrix slots percentage sum must equal exactly 100% (BR-07).
- **Active Locking**: Blueprints associated with generated tests cannot be modified or hard-deleted. They must be cloned or disabled.

## Requirements *(mandatory)*

### Functional Requirements

- **BR-Blueprint-01**: Experts cannot review or approve blueprints they created.
- **BR-Blueprint-03**: Rejection requires a non-empty ReviewNote describing corrective actions.
- **BR-07**: The sum of all topic percentages in a blueprint matrix must equal exactly 100%.
- **BR-09**: Cloning creates a completely independent entity with a new UUID.
- **Dynamic WeakTag Adjustment Loop**: When generating a test for a student, the selection engine queries the Recommender module for the student's `WeakTags`. It adapts the question candidate pool by biasing question selection with anti-fatigue and challenge rules:
  - **WeakTag Cap**: The selection engine limits the maximum number of WeakTag-biased questions to **20%** of the total questions in the generated test to prevent student fatigue.
  - **Adaptive Bias Probability**: Reduces the selection bias probability of WeakTag questions from 70% to **40%** per matching blueprint slot.
  - **Difficulty Downscaling**: If a student's diagnosed WeakTag is at a `Hard` difficulty level, the engine downscales the difficulty to selection of a `Medium` question for that topic to rebuild confidence, scaling back up to `Hard` only after they achieve an accuracy rate > 70% on that topic.
  - **Difficulty Upscaling (Challenge Mode)**: If a student's competency score for a topic-difficulty is high ($P_{\text{tag}} \ge 8.0$ and status is `MASTERED`), the engine upscales the difficulty by selecting questions of one level higher (e.g., from `Medium` to `Hard`) to challenge the student.
  - **Easy-Level Protection (Remedial Learning)**: If a student fails at the `Easy` level ($P_{\text{tag}} < 5.0$ at `Easy`), further downscaling is blocked. The engine automatically reduces the bias probability of this topic to **10%** in subsequent tests to prevent frustration, while the system prioritizes directing the student to foundational lectures and reading materials first.

### Key Entities *(include if feature involves data)*

- **Blueprint**: blueprint_id (PK), blueprint_name, grade, total_questions, duration_minutes, expert_id (FK)
- **BlueprintDetail**: blueprint_detail_id (PK), blueprint_id (FK), tag_id (FK), difficulty_id (FK), quantity

## Success Criteria *(mandatory)*

### Measurable Outcomes

- Matrix validation ensures no invalid blueprints are saved.
- Page rendering and filters load within 1.5 seconds.
- Schema isolation per module is enforced under the shared `tst` namespace (physically separated within database mapping).

## Assumptions

- Database is SQL Server.
- User accounts and roles (Expert) are verified via the Identity & Access module.

# Feature Specification: Test Blueprint & Generation & Taking

**Feature Branch**: `[specs/003-testing]`  
**Created**: 2026-06-22  
**Status**: Draft  
**Input**: User description: "From reports in C:\Users\Admin\Documents\ĐỒ ÁN\Report"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create and Manage Test Blueprints (Priority: P1)
As an Expert, I want to create and modify exam blueprints specifying questions, duration, and topic-difficulty weights so that standard exams are generated.
**Why this priority**: Blueprints govern the structure and distribution of topics on exams, ensuring academic validity.
**Independent Test**: Expert sets weights in a blueprint form, and system validates that weight percentages sum to 100%.
**Acceptance Scenarios**:
1. **Given** Expert is creating a blueprint, **When** they allocate weights for Algebra (40%) and Calculus (60%), **Then** the system saves the blueprint and marks it as active.
2. **Given** a blueprint, **When** Expert clones it, **Then** all topic and difficulty configurations are duplicated into a new draft blueprint.

---

### User Story 2 - Practice and Mock Exam Taking (Priority: P1)
As a Student, I want to start a practice exam and answer questions with a synchronized countdown timer so that I can evaluate my performance under exam conditions.
**Why this priority**: Core student user journey. The exam room must be secure, reliable, and auto-save work to prevent data loss.
**Independent Test**: Answering a question cache-saves progress in Redis. Letting the timer reach zero automatically submits.
**Acceptance Scenarios**:
1. **Given** a Student starts a test, **When** they select option A for question 1, **Then** the answer is cached in Redis, and the navigator panel marks the question as completed.
2. **Given** a test is in progress, **When** the countdown timer hits 00:00, **Then** all input forms are locked, and the test is force-submitted to the server (AF-03).
3. **Given** student loses internet connection during test, **When** they select an answer, **Then** a warning alert displays: "Connection lost. Retrying..." and retries in background.

---

### User Story 3 - Academic Integrity Controls (Priority: P2)
As a Student, I want the system to warn me if I switch tabs and submit my test automatically if I violate rules repeatedly so that test integrity is maintained.
**Why this priority**: Essential for mock exams where students must not look up answers in other browser tabs.
**Independent Test**: Switching tabs three times triggers an automatic force-submission.
**Acceptance Scenarios**:
1. **Given** a test session is active, **When** Student switches tabs for the first time, **Then** the system displays: "Tab switching is restricted. 1 of 3 violations. Test will be force-submitted on the 3rd violation." (AF-01).
2. **Given** a student has 2 violations, **When** they switch tabs a third time, **Then** the screen locks, and the test is automatically submitted (AF-02).

### Edge Cases

- **State Immutability**: Once a test session transitions to 'Submitted' or 'Force-Submitted', all associated answers become strictly read-only (DC-03).
- **Server Shutdown/Disconnect**: If the student closes the browser, the test session must be recoverable from the cached state in Redis upon reopening, provided time has not expired.
- **Concurrent Generator Load**: Generating a randomized test matching a blueprint must execute efficiently even when the database question bank is large.

## Requirements *(mandatory)*

### Functional Requirements
- **FR-001**: Expert MUST be able to define, clone, update, and delete test blueprints (UC-42 to UC-46).
- **FR-002**: System MUST validate that blueprint weight allocations match the total question count and sum criteria (UC-43).
- **FR-003**: System MUST auto-generate practice tests by compiling active pool questions matching the blueprint topic-difficulty distribution (UC-47).
- **FR-004**: System MUST provide a countdown timer synchronized with the server (UC-47).
- **FR-005**: System MUST save student answers to the Redis cache every 30 seconds or on answer selection (BR-11 in UC-47).
- **FR-006**: System MUST track browser visibility change events and log tab-switching violations, warning on the 1st/2nd and force-submitting on the 3rd (BR-10, AF-01, AF-02).
- **FR-007**: System MUST lock all inputs and trigger force-submission once the time limit expires (BR-12).
- **FR-008**: System MUST allow students to ask Google Gemini AI Chatbot for hints or step-by-step assistant explanations on solution review screen (UC-51).

### Key Entities
- **Blueprint & BlueprintDetail**: Define the test structure and topic weight grid.
- **Test**: A generated practice test instance.
- **TestSession**: An active or submitted test taking record containing startTime, timeLimit, status, and tabViolations.
- **TestAnswer**: Individual question responses submitted by a student.
- **TestIncidents**: Logs of cheating violations (e.g. tab switches).

## Success Criteria *(mandatory)*

### Measurable Outcomes
- **SC-001**: Generating a test from a blueprint must complete in under 2 seconds.
- **SC-002**: Auto-save updates to the Redis cache must complete in under 100ms.
- **SC-003**: Test submission and final score generation (via auto-grading background job JOB-01) must complete within 5 seconds.

## Assumptions

- Students perform testing in modern browsers supporting HTML5 Page Visibility API.
- Redis cache is configured and running with high availability.

# Tasks Checklist: Test Generator Module

- [ ] **Phase 1: Persistence Setup**
    - [ ] Define blueprint and blueprint_detail entities mapping under the `tst` schema namespace.
    - [ ] Create EF configurations and seed data.
- [ ] **Phase 2: Core Domain Logic**
    - [ ] Implement matrix validation checks (sum of percentages = 100%).
    - [ ] Implement blueprint review service (Approve/Reject status flows).
    - [ ] Design and implement the generation engine with dynamic WeakTag biasing.
- [ ] **Phase 3: Controller and Routing**
    - [ ] Expose blueprint CRUD, clone, and review controllers with JWT verification.
    - [ ] Register DI mappings inside `TestGenModuleExtensions.cs`.
- [ ] **Phase 4: Verification**
    - [ ] Validate project build compilation and write unit tests for percentage validation and WeakTag biasing.

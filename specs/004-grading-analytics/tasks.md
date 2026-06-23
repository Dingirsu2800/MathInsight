# Tasks Checklist: Grading & Analytics Module

- [ ] **Phase 1: Persistence Setup**
    - [ ] Create EF configurations and entity mapping under the `grd` schema namespace.
    - [ ] Run dotnet EF migration command to compile migrations in WebAPI.
- [ ] **Phase 2: Core Domain Logic**
    - [ ] Implement services interfaces and concrete handler logic.
    - [ ] Add MediatR domain event handlers or MassTransit RabbitMQ queue consumer classes.
- [ ] **Phase 3: Controller and Routing**
    - [ ] Expose REST controllers with JWT validation policies.
    - [ ] Register DI services mapping inside `GradingAnalyticsModuleExtensions.cs`.
- [ ] **Phase 4: Verification**
    - [ ] Verify using project compilation and unit tests assertion checks.
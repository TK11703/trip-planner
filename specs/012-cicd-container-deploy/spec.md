# Feature Specification: Containerized Delivery & CI/CD Pipeline

**Feature Branch**: `012-cicd-container-deploy`

**Created**: 2026-07-08

**Status**: Draft

**Input**: User description: "I need to start thinking about deploying this into a container for users to access. I'd like to build a ci/cd pipeline in my repo, which builds and deploys the code."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Automated build and container image on every change (Priority: P1)

As a maintainer, when I push code to the repository, an automated pipeline compiles the
application, runs the test suite, and produces versioned container images for the web and
API services so that a known-good, deployable artifact always exists without manual steps.

**Why this priority**: This is the foundation of the whole feature. Without a reliable,
automated build-and-package step, there is nothing to deploy. It also immediately protects
the codebase by gating every change behind a green build and tests, delivering value even
before automated deployment exists.

**Independent Test**: Push a commit (or open a pull request) and confirm the pipeline runs
end to end, fails on a broken build or failing test, and on success publishes tagged
container images to the container registry. This can be validated with no deployment target
configured.

**Acceptance Scenarios**:

1. **Given** a change is pushed to the main branch, **When** the pipeline runs, **Then** the
   solution builds, all tests pass, and container images for the web and API services are
   published to the registry, each tagged with a traceable version.
2. **Given** a change that breaks the build or a test, **When** the pipeline runs, **Then**
   the pipeline fails, no images are published, and the maintainer is notified of the failure.
3. **Given** a pull request is opened, **When** the pipeline runs, **Then** build and tests
   execute as a required status check before the change can be merged.

---

### User Story 2 - Automated deployment so users can access the app (Priority: P2)

As a maintainer, when a change is successfully built and validated on the main branch, the
pipeline deploys the new container images to a hosted environment so that end users can reach
the running application over the internet without me performing manual deployment steps.

**Why this priority**: This delivers the user-facing goal ("deploying this into a container
for users to access"). It depends on P1 producing images, so it comes second, but it is the
outcome that makes the feature valuable to end users.

**Independent Test**: Merge a change to the main branch and confirm that the updated
application becomes reachable at a stable URL, serving the new version, with the database and
authentication working, without any manual deployment action.

**Acceptance Scenarios**:

1. **Given** images were published from a successful main-branch build, **When** the deploy
   stage runs, **Then** the web and API services are deployed to the hosted environment and
   are reachable at a stable URL.
2. **Given** a deployment completes, **When** a user visits the application URL, **Then** they
   can sign in and use core trip-planning features against the deployed database.
3. **Given** a deployment fails partway, **When** the failure is detected, **Then** the
   previously running version remains serving users and the maintainer is notified.

---

### User Story 3 - Safe, traceable, and repeatable releases (Priority: P3)

As a maintainer, I can see exactly which version is deployed, trace it back to the commit that
produced it, and re-run or roll back a deployment so that releases are auditable and
recoverable when something goes wrong.

**Why this priority**: Operational safety and traceability make the pipeline trustworthy for
ongoing use. It builds on P1 and P2 and is important for real-world operation, but the
pipeline delivers value before this is fully realized.

**Independent Test**: Inspect a completed deployment and confirm the running version maps to a
specific commit/tag, then trigger a rollback (redeploy of a prior image) and confirm the
earlier version is restored.

**Acceptance Scenarios**:

1. **Given** a deployment has completed, **When** the maintainer inspects the environment,
   **Then** the deployed version is identifiable and traceable to the source commit.
2. **Given** a bad release, **When** the maintainer initiates a rollback to a prior image
   version, **Then** the previously known-good version is restored and reachable by users.
3. **Given** secrets and configuration are required at runtime, **When** the app is deployed,
   **Then** no secrets are stored in source control and the app reads configuration from a
   secure store.

---

### Edge Cases

- What happens when the test suite is flaky or a dependency (e.g., the test database
  container) fails to start during the pipeline run?
- How does the system handle a deployment that succeeds for one service (e.g., API) but fails
  for another (e.g., web)?
- What happens when two changes are merged in quick succession and pipelines run concurrently?
- How are database schema changes/migrations applied during deployment without breaking the
  currently running version?
- What happens when required runtime secrets or configuration are missing or invalid at deploy
  time?
- How does the pipeline behave for pull requests opened from forks (where registry/deploy
  credentials should not be exposed)?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The repository MUST contain a CI/CD pipeline definition, versioned alongside the
  source code, that runs automatically on defined triggers (at minimum: pushes to the main
  branch and pull requests targeting it).
- **FR-002**: The pipeline MUST restore, build, and run the automated test suite for the
  solution, and MUST fail the run if the build or any test fails.
- **FR-003**: The pipeline MUST produce container images for the deployable services (the web
  application and the API) as part of a successful build.
- **FR-004**: Each container image MUST be tagged with a version that is traceable to the exact
  source commit that produced it.
- **FR-005**: The pipeline MUST publish successfully built images to a container registry.
- **FR-006**: On a successful build of the main branch, the pipeline MUST deploy the produced
  images to a hosted environment reachable by end users over the internet at a stable URL.
- **FR-007**: The deployed application MUST run with its required backing services (database and
  authentication) functioning, so users can sign in and use core features.
- **FR-008**: The pipeline MUST NOT store secrets or credentials in source control; runtime and
  pipeline secrets MUST be provided through a secure secret store.
- **FR-009**: The pipeline MUST notify maintainers of failed runs (build, test, or deployment).
- **FR-010**: Pull-request pipeline runs MUST execute build and tests as a status check and MUST
  NOT deploy to the user-facing environment.
- **FR-011**: The deployment MUST apply required database schema changes/migrations in a
  controlled manner as part of the release.
- **FR-012**: The maintainer MUST be able to roll back to a previously published image version.
- **FR-013**: The pipeline MUST be repeatable — the same commit produces the same deployable
  result — and MUST require no manual local build steps to release.
- **FR-014**: A successful main-branch build MUST require a manual approval before it provisions
  infrastructure and deploys to the production (user-facing) environment. (A failed build or test
  still blocks the deployment per FR-002.)
- **FR-015**: The pipeline MUST target a single production environment for v1; separate
  staging/pre-production environments are out of scope.

### Key Entities *(include if feature involves data)*

- **Pipeline Run**: A single execution of the CI/CD workflow triggered by a repository event;
  has a status (success/failure), the triggering commit, and the artifacts it produced.
- **Container Image**: A versioned, deployable package of a service (web or API), tagged and
  stored in the container registry, traceable to a source commit.
- **Deployment/Release**: An application of a set of container images to a hosted environment;
  has a target environment, a version, a status, and a link to the pipeline run that created it.
- **Environment**: A hosted destination where the application runs (e.g., production), with its
  own configuration, secrets, and reachable URL.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of changes merged to the main branch are built, tested, and packaged into
  container images automatically, with no manual build steps.
- **SC-002**: A change merged to the main branch is live and reachable by users within 30
  minutes without manual deployment intervention.
- **SC-003**: Every deployed version is traceable to a specific source commit in under 1 minute
  of inspection.
- **SC-004**: 100% of pull requests are blocked from merging while their build or tests are
  failing.
- **SC-005**: A maintainer can roll back to the previous known-good version within 15 minutes.
- **SC-006**: Zero secrets or credentials are present in the source repository.
- **SC-007**: After deployment, users can sign in and complete a core trip-planning task
  (create/view a trip) against the deployed environment.

## Assumptions

- The CI/CD platform is GitHub Actions, since the repository is hosted on GitHub.
- The hosting target is a managed container platform on Azure (Azure Container Apps is the
  natural fit for a .NET Aspire application), with an Azure Container Registry for images. This
  aligns with the app's existing Azure Entra authentication.
- Images are built for the web (Blazor) and API services; the PostgreSQL database is a managed
  service in the hosted environment rather than a container the pipeline deploys.
- Database schema changes are delivered via the existing SQL scripts in
  `src/TripPlanner.Database/Scripts/` applied during deployment.
- Runtime secrets (Azure Entra client IDs/secrets, database connection string) are stored in a
  secure secret store and injected at deploy time; none are committed to the repository.
- "Users" are external end users accessing the app over the public internet via a stable URL.
- The existing test suite (xUnit / bUnit / Testcontainers / Playwright) is the quality gate; the
  pipeline runner must be able to run container-based tests (Docker available).
- Initial scope is a single application/tenant deployment; multi-region or high-availability
  topologies are out of scope for v1.
- Production deployments require a manual approval (a reviewer approves the `production`
  environment) before infrastructure is provisioned and the apps are deployed.
- A single production environment is targeted for v1; no separate staging/pre-production
  environment is in scope.

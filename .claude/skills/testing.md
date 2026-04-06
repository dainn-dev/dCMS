# Testing Skill

## Stack Testing

**Backend (ASP.NET Core / Umbraco):**
- Framework: xUnit
- Mocking: Moq hoặc NSubstitute
- Integration tests: `Microsoft.AspNetCore.Mvc.Testing` (WebApplicationFactory)
- Run: `dotnet test`
- Coverage: `dotnet test --collect:"XPlat Code Coverage"`

**Frontend (Next.js):**
- Framework: Jest + React Testing Library
- Run: `npm run test` (trong `src/frontend/`)
- Watch mode: `npm run test:watch`

**E2E:**
- Framework: Playwright
- Run: `npx playwright test`
- UI mode: `npx playwright test --ui`

## Test Requirements

**New feature:**
- Backend: viết unit test cho service/domain logic TRƯỚC implementation
- Frontend: viết component test song song với implementation
- API endpoint mới: test happy path + error paths + auth/RBAC

**Bug fix:**
- Viết regression test reproduce bug TRƯỚC khi fix
- Confirm test fails → fix → confirm test passes

**Multi-tenant code:**
- Luôn test với ít nhất 2 tenant context để verify isolation
- Test rằng tenant A không thể access data của tenant B

**Payment flows:**
- Dùng mock API Gateway — không gọi payment system thật trong tests
- Test: success flow, payment failure, timeout, invalid response

## Backend Test Structure

```
dCMS.Tests/
├── Unit/
│   ├── Domain/          # Domain model tests
│   ├── Services/        # Business logic tests (mock repos)
│   └── Validators/      # Input validation tests
├── Integration/
│   ├── Api/             # API endpoint tests (WebApplicationFactory)
│   └── Repositories/    # DB integration tests (test DB)
└── Fixtures/            # Shared test setup, builders
```

## Frontend Test Structure

```
src/frontend/
└── __tests__/
    ├── components/      # Component behavior tests
    ├── pages/           # Page integration tests
    └── lib/             # Utility function tests
```

## Sau khi test

Report format:
```
Backend: X/X passing (dotnet test)
Frontend: X/X passing (jest)
E2E: X/X passing (playwright) [nếu chạy]
```
Nếu có failure: phân tích root cause, fix trước khi tiếp tục. Không bàn giao khi còn failing tests.

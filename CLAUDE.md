# dCMS — eCommerce CMS Platform

Nền tảng eCommerce CMS headless theo mô hình siêu thị. dCMS quản lý nhiều **Siêu thị** (mỗi Siêu thị là 1 tenant độc lập). Mỗi Siêu thị sở hữu nhiều **Brands** (thương hiệu con), mỗi Brand vận hành nhiều **Stores** (cửa hàng/kênh bán). Mỗi Siêu thị có 1 Umbraco instance riêng.

> **Phase hiện tại:** Build headless CMS + Commerce API (REST). Next.js storefront là Phase 2 riêng biệt — chưa trong scope.

---

## Project Context

| | |
|---|---|
| **Stack** | Umbraco CMS (ASP.NET Core) — Phase 2: Next.js storefront (out of scope hiện tại) |
| **Database** | SQL Server (Umbraco default per instance) |
| **Search** | Elasticsearch |
| **Payment** | External system qua API Gateway |
| **Kiến trúc** | Headless multi-tenant — Platform → Siêu thị (Tenant) → Brands → Stores → Storefront |
| **Deployment** | Docker (containerized) |
| **CI/CD** | GitHub Actions |
| **Users** | Super Admin / Chain Admin / Brand Manager / Store Manager / Store Staff / End Customers |

**Luôn nhớ:**
- Hierarchy 4 cấp: **Siêu thị (Tenant) → Brands[] → Stores[] → Storefront** (storefront là Phase 2)
- **Tenant = Siêu thị** — isolation tại cấp Siêu thị (1 Umbraco instance + 1 SQL Server per Siêu thị)
- Brand là layer tổ chức bên trong Siêu thị — không isolated riêng
- Store là đơn vị bán hàng — expose qua Commerce API, storefront (Next.js) build ở Phase 2
- Elasticsearch index scoped theo Siêu thị: `dcms-{tenantId}-*`, filter thêm brandId/storeId trong query
- Auth dùng RBAC với dynamic roles (không phải Umbraco built-in auth đơn giản)
- Rate limiting và CORS policy bắt buộc trên mọi API endpoint
- Performance target: 2000 CCU — tránh N+1 query, tránh in-memory state không an toàn
- Multi-language và multi-currency phải được thiết kế từ đầu, không add-on sau
- Payment KHÔNG xử lý trực tiếp — luôn thông qua API Gateway external

---

## Làm việc với Claude (CONSULTANT AGENT mode)

### Flow

Claude nhận yêu cầu → tự plan → tự implement → tự review → bàn giao.
Không cần xác nhận từng bước trừ khi có ambiguity thực sự không thể tự quyết định.

### Triển khai

1. Đọc CLAUDE.md + `.claude/memory/MEMORY.md` + docs liên quan
2. Nếu yêu cầu không đủ rõ để bắt đầu: hỏi tối đa 2 câu, sau đó tự quyết định
3. Plan → implement → test, không dừng hỏi giữa chừng

Khi code, luôn kiểm tra:
- **Security:** Input đã validate chưa? Có lỗ hổng injection, auth bypass không?
- **RBAC:** Role của user có đủ quyền cho action này không? Dynamic role check.
- **Cluster-safe:** Có dùng in-memory state không? Nếu có → chuyển qua Redis hoặc DB
- **Performance:** Có N+1 query không? Cần cache không? Batch được không? (target: 2000 CCU)
- **Multi-tenant isolation:** Code có vô tình access data của Siêu thị khác không? Luôn scope theo TenantId (= Siêu thị). Trong tenant, filter thêm brandId/storeId khi cần.
- **Multi-language/currency:** Text có hardcode không? Currency conversion có đúng không?
- **Pattern nhất quán:** Có theo đúng pattern của codebase không?
- **Side effects:** Thay đổi này có break feature/logic khác không?
- **Deploy safety:** Code mới có ảnh hưởng đến Docker rolling deploy không?

### Self-Review Checklist (chạy trước khi bàn giao)

| # | Kiểm tra | Kết quả |
|---|---|---|
| 1 | Input validation đầy đủ chưa? | ✓ / ✗ |
| 2 | Có lỗ hổng injection / auth bypass không? | ✓ / ✗ |
| 3 | RBAC dynamic role check đúng chưa? | ✓ / ✗ |
| 4 | Tenant isolation — không leak data giữa các Siêu thị? TenantId luôn được scope? | ✓ / ✗ |
| 5 | Có dùng in-memory state không an toàn không? | ✓ / ✗ |
| 6 | N+1 query? Cache cần thiết chưa? (target 2000 CCU) | ✓ / ✗ |
| 7 | Rate limiting và CORS policy được áp dụng chưa? | ✓ / ✗ |
| 8 | Multi-language / multi-currency handled đúng chưa? | ✓ / ✗ |
| 9 | Theo đúng pattern của codebase chưa? | ✓ / ✗ |
| 10 | Thay đổi có break flow nào khác không? | ✓ / ✗ |
| 11 | Compatible với Docker deploy? | ✓ / ✗ |
| 12 | Tests pass? | ✓ / ✗ |

### Bàn giao

Sau khi hoàn thành, tóm tắt cho user:
- Đã làm gì
- File nào thay đổi
- Test results
- Kết quả self-review checklist
- Điều gì cần user biết (nếu có)

---

## Project Structure

```
dCMS/
├── src/
│   ├── backend/                  # Umbraco CMS (ASP.NET Core)
│   │   ├── dCMS.Core/            # Domain models, interfaces, business logic
│   │   ├── dCMS.Infrastructure/  # Repositories, external services, DB
│   │   ├── dCMS.Web/             # Umbraco web host, Content Delivery API
│   │   └── dCMS.Tests/           # Unit & integration tests
│   └── frontend/                 # Next.js storefront
│       ├── app/                  # App Router pages & layouts
│       ├── components/           # UI components
│       ├── lib/                  # API clients, utilities
│       └── types/                # TypeScript types
├── infra/
│   ├── docker/                   # Dockerfiles cho backend & frontend
│   └── docker-compose.yml        # Local dev stack
├── .github/
│   └── workflows/                # GitHub Actions CI/CD
├── docs/
│   ├── architecture.md           # System architecture
│   └── api-overview.md           # API endpoints
└── .claude/
    ├── memory/                   # Claude memory files
    └── skills/                   # Claude skill files
```

> **Lưu ý:** Structure trên là proposed architecture — project đang trong planning phase.

---

## Key Commands

| Command | Mô tả |
|---|---|
| `docker compose up` | Start toàn bộ local stack |
| `docker compose up backend` | Start chỉ Umbraco backend |
| `docker compose up frontend` | Start chỉ Next.js frontend |
| `dotnet test` | Run backend unit tests |
| `npm run dev` (trong `src/frontend/`) | Start Next.js dev server |
| `npm run test` (trong `src/frontend/`) | Run frontend tests |
| `npm run build` (trong `src/frontend/`) | Build Next.js production |

---

## Skills

| Skill | Khi nào dùng |
|---|---|
| `.claude/skills/testing.md` | Chạy tests, viết tests |
| `.claude/skills/ui-review.md` | Sau khi thay đổi UI storefront |
| `.claude/skills/parallel-agents.md` | Task lớn có nhiều phần độc lập |
| `.claude/skills/compress-context.md` | Context quá dài |
| `.claude/skills/umbraco-workflow.md` | Làm việc với Umbraco content types, backoffice |
| `.claude/skills/docker-workflow.md` | Build, run, debug Docker containers |
| `.claude/skills/nextjs-patterns.md` | Server vs client components, data fetching |
| `.claude/skills/elasticsearch-workflow.md` | Index, query, mapping Elasticsearch |

---

## Memory System

Đọc trước khi bắt đầu task:
- `.claude/memory/MEMORY.md` — project state hiện tại (< 200 lines)
- `.claude/memory/project.md` — stable facts về project
- `.claude/memory/decisions.md` — architectural decisions đã được đưa ra

Cập nhật sau khi hoàn thành task:
- Update `MEMORY.md` nếu project state thay đổi
- Thêm vào `decisions.md` nếu có architectural decision mới

---

## Testing

- **Backend:** xUnit (ASP.NET Core standard)
- **Frontend:** Jest + React Testing Library
- **E2E:** Playwright
- **Run backend:** `dotnet test`
- **Run frontend:** `npm run test` (trong `src/frontend/`)
- **Run E2E:** `npx playwright test`
- **Pattern backend:** Test theo layer — Unit test services/domain logic, Integration test API endpoints
- **Pattern frontend:** Test behavior, không test implementation details

---

## Git & GitHub

- Branches: `feat/<task>`, `fix/<task>`, `chore/<task>` (kebab-case, max 4 từ)
- Commits: nhỏ, thường xuyên, descriptive
- PR: tạo khi task xong, bao gồm change summary + test results
- CI: GitHub Actions tự động run tests và build Docker images khi push

---

## Code Conventions

- **Backend (C#):** PascalCase cho classes/methods, camelCase cho local variables, prefix interface với `I`
- **Frontend (TypeScript):** PascalCase cho components, camelCase cho functions/variables, kebab-case cho file names
- **API responses:** Consistent JSON shape — `{ data, meta, error }`
- **Multi-tenant:** Mọi DB query phải include `TenantId` (= Siêu thị). Trong tenant, filter thêm `BrandId` hoặc `StoreId` khi cần scoping sâu hơn
- **Error handling:** Không expose internal error details ra client — log internally, return generic message

---

## Context Management

Khi context quá dài (nhiều messages, conversation cũ):
Run compress-context skill → summarize → archive → rewrite MEMORY.md

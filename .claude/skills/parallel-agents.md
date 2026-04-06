# Parallel Agents Skill

Dùng khi task có nhiều phần lớn, độc lập với nhau (ví dụ: implement backend API song song với frontend component).

## Luôn Hỏi Trước

KHÔNG dispatch agents mà không có confirmation. Present:
- Agent nào làm gì
- File nào mỗi agent owns
- Tradeoffs của parallel vs sequential

Chờ explicit approval.

## Dispatching

Mỗi agent prompt phải include:
1. Mô tả task chính xác
2. File paths agent owns (ví dụ: `src/backend/dCMS.Web/Controllers/ProductController.cs`)
3. File paths agent KHÔNG được touch
4. Cách run tests (`dotnet test` hoặc `npm run test`)
5. Definition of done (tests pass + self-review checklist)

## Sau khi Hoàn Thành

1. Review tất cả changes cùng nhau
2. Run full test suite (`dotnet test` + `npm run test`)
3. Resolve conflicts
4. Commit cùng nhau

## Ví dụ Split tốt cho dCMS

| Agent 1 | Agent 2 |
|---|---|
| Backend API endpoint | Frontend page fetch + render |
| Umbraco content type setup | Elasticsearch index mapping |
| Auth middleware | RBAC permission service |
| Order processing logic | Order management UI |

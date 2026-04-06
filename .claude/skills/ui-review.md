# UI Review Skill

Chạy sau khi implement bất kỳ UI changes nào trên Next.js storefront.

## Step 1: Start Dev Server

```bash
# Start Next.js storefront
cd src/frontend && npm run dev

# Hoặc start toàn bộ stack với Docker
docker compose up
```

Chờ "Ready" hoặc "Local: http://localhost:3000" trong output.

## Step 2: Open Browser với Playwright

Dùng Playwright MCP tool để:
1. Navigate đến `http://localhost:3000`
2. Login nếu cần (kiểm tra `.env` hoặc `docker-compose.yml` cho test credentials)
3. Navigate đến page/feature đã thay đổi

## Step 3: Dừng lại và Chờ

Báo user:
- "Tôi đã mở [URL] trong browser"
- "Đang ở trang [page name / route]"
- "Bạn review UI và cho tôi biết cần điều chỉnh gì"

**DỪNG TẠI ĐÂY. Chờ user response.**

## Step 4: Iterate

Nếu user yêu cầu thay đổi: apply → reload → hỏi review lại.
Nếu user approve: tiếp tục tạo PR.

## Checklist UI Review

Trước khi báo user review, tự kiểm tra:
- [ ] Responsive? (mobile, tablet, desktop)
- [ ] Multi-language text hiển thị đúng không? Có hardcode text không?
- [ ] Currency format đúng theo locale không?
- [ ] Loading states có không?
- [ ] Error states có không?
- [ ] Accessibility cơ bản (alt text, aria labels)?

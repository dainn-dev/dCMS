# Orders backoffice — React SPA (Light DOM)

## Mục tiêu

- Thay UI mock hiện tại của section `dcmsOrders` bằng một SPA React.
- Vẫn giữ cơ chế Umbraco v16: `extensions[type=section]` trỏ tới 1 file JS trong `App_Plugins`.
- Mount vào **Light DOM** để dễ dùng UI libraries (MUI/AntD) và tránh các vấn đề Shadow DOM.

## Phạm vi

- Chỉ scaffold nền tảng (Vite + React) + bridge element mount/unmount.
- UI React ban đầu chỉ hiển thị “Orders (React) is running” để xác nhận pipeline hoạt động.
- Chưa nối API thật; dữ liệu, auth, routing chi tiết sẽ làm sau.

## Kiến trúc đề xuất

### 1) Bridge element (Umbraco extension)

- File: `src/backend/dCMS.Web/App_Plugins/DcmsV16/dcms-orders-section.js`
- Nhiệm vụ:
  - Tạo container mount (ví dụ `<div data-react-root>`).
  - Load CSS build (`/App_Plugins/DcmsV16/dist/orders-spa.css`) 1 lần (idempotent).
  - Dynamic import module build (`/App_Plugins/DcmsV16/dist/orders-spa.js`).
  - Gọi `mount(el)` khi `connectedCallback()`.
  - Gọi `unmount(el)` khi `disconnectedCallback()`.

### 2) React SPA (source)

- Thư mục: `src/backoffice/dcms-backoffice-spa/`
- Build output:
  - JS: `src/backend/dCMS.Web/App_Plugins/DcmsV16/dist/orders-spa.js`
  - CSS: `src/backend/dCMS.Web/App_Plugins/DcmsV16/dist/orders-spa.css`
- Entry exports:
  - `mount(hostElement: HTMLElement): void`
  - `unmount(hostElement: HTMLElement): void`

## Build & run

- `npm install` trong `src/backoffice/dcms-backoffice-spa/`
- `npm run build` để phát sinh bundle vào `App_Plugins/DcmsV16/dist/`
- Khi Umbraco backoffice load section Orders (`dcmsOrders`), bridge sẽ mount React app.

## Rủi ro / lưu ý

- Module caching: dynamic import sẽ cache module; mount/unmount phải idempotent.
- Nếu backoffice navigation tạo/destroy DOM nhiều lần, `disconnectedCallback()` cần clean up đúng.
- Vấn đề base path được fix bằng cách dùng absolute URL `/App_Plugins/...` cho JS/CSS.


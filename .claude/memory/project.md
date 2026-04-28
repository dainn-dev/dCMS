# Project Facts — dCMS

_Stable facts. Chỉ update khi project thay đổi căn bản._

## Description

dCMS là nền tảng eCommerce CMS headless theo **mô hình siêu thị**. Platform quản lý nhiều **Siêu thị** (chuỗi bán lẻ/tập đoàn) — mỗi Siêu thị là 1 tenant độc lập. Mỗi Siêu thị sở hữu nhiều **Brands** và mỗi Brand vận hành nhiều **Stores**.

Hệ thống được thiết kế dựa trên pattern enterprise eCommerce CMS, hỗ trợ đầy đủ:

- Product & Catalog Management
- Order Processing Workflow
- Promotions / Campaign Engine
- Fulfillment & Delivery Slot Management
- Content / Banner / Landing Page CMS
- Approval Workflow
- Reporting & Analytics
- Multi-level RBAC
- Multi-store / Multi-brand storefront

## Tech Stack

| Layer | Technology |
|---|---|
| CMS / Backend | Umbraco CMS (ASP.NET Core) |
| Storefront | Next.js |
| Search | Elasticsearch |
| Catalog / Inventory | PostgreSQL |
| Tenant CMS DB | SQL Server / PostgreSQL tùy deployment |
| Auth | JWT + RBAC |
| Infra | Docker |
| CI/CD | GitHub Actions |

---

## Core Business Modules

## 1. Orders

- Order Processing Workspace
- Search / Filter Orders
- View Details / Take Action
- Update Order Status
- Item-level fulfillment status
- Print Packing Slip
- Print Delivery Receipt
- Resend Confirmation
- Partial Fulfillment
- Refund Cases
- Pending Cancellation

### Supported Order Status

- Open Order
- Ready for Delivery / Pickup
- Processing
- Picked Up
- Delivered
- Returned
- Admin Cancelled
- User Cancelled
- Pending Cancellation
- Partially Fulfilled

---

## 2. Catalog / eStore

### Brands
- Manage Brands
- Brand metadata
- Export Brands

### Categories
- Multi-level Categories
- Reclassify category
- Hide / Unhide expired categories

### Products
- CRUD Products
- Product Images
- Product Attributes
- Category Assignment
- Variant Pricing
- SEO Metadata
- Related Products

### Bulk Operations

- Bulk Product Import
- Bulk Image Import
- Bulk Inventory Import
- Export Catalog
- Bulk Orders Export (async)

---

## 3. Promotions

### Promo Codes

- Standard Promo Code
- Shareable Codes
- Account-bound Codes
- Grouped Promo Codes
- Exclusion Lists

### Campaign Engine

- Purchase With Purchase (PWP Item)
- PWP Discount
- Mix & Match
- Product Discount
- After Sales Promotion

---

## 4. Fulfillment

- Delivery Options
- Pickup / Collection
- Delivery Slots
- Collection Locations
- Logistic Partners
- Tracking Numbers
- Dynamic Checkout Fields

---

## 5. Umbraco Content CMS

- Homepage Main Banner
- Homepage Sub Banner
- Homepage Product Blocks
- Navigation Menu
- Landing Pages
- WYSIWYG Pages
- Content Templates
- Reusable Sections
- Embedded Video

---

## 6. Approval Workflow

- Product Approval
- Content Approval
- Campaign Approval
- Promo Code Approval
- Bulk Approvals

---

## 7. Reports

- Transaction Summary
- Transaction Details
- Payment Reports
- Sales by Category
- Sales by Brand
- Sales by Product
- Sales by Store
- Delivery Slot Reports
- Abandon Cart Report
- Restock Subscription Report

---

## 8. Access Control

### Roles

- SuperAdmin
- ChainAdmin
- BrandManager
- StoreManager
- StoreStaff
- Finance
- Marketing
- ContentEditor

### Scope

- Tenant
- Brand
- Store

---

## Multi-tenant Rules

- Tenant isolated DB / CMS
- Brand shared within tenant
- Store scoped storefront + data visibility

---

## Architecture Direction

dCMS là enterprise composable commerce CMS:
- Headless CMS + Commerce APIs
- Search-first catalog
- Workflow-driven backoffice
- Multi-brand multi-store governance
- Extensible promotions engine
- Fulfillment-ready architecture
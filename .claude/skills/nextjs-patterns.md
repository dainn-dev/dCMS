# Next.js Patterns Skill

Dùng khi implement storefront features với Next.js App Router.

## Server vs Client Components

**Server Components (default — ưu tiên dùng):**
- Fetch data trực tiếp từ Umbraco API hoặc custom API
- Static content, product listings, product detail
- SEO-critical pages
- Không có: `useState`, `useEffect`, event handlers, browser APIs

```tsx
// app/products/page.tsx — Server Component
async function ProductsPage({ searchParams }: { searchParams: { q?: string } }) {
  const products = await fetchProducts(searchParams.q);
  return <ProductGrid products={products} />;
}
```

**Client Components (chỉ khi cần):**
- Interactive UI: cart, wishlist toggle, search input, quantity picker
- Browser APIs: geolocation, localStorage
- Real-time updates: stock status, price changes

```tsx
"use client";
// components/AddToCartButton.tsx
export function AddToCartButton({ productId }: { productId: string }) {
  const { addItem } = useCart();
  return <button onClick={() => addItem(productId)}>Add to Cart</button>;
}
```

## Data Fetching từ Umbraco

```typescript
// lib/umbraco.ts
const UMBRACO_URL = process.env.UMBRACO_API_URL;
const UMBRACO_KEY = process.env.UMBRACO_API_KEY;

export async function fetchContent<T>(path: string): Promise<T> {
  const res = await fetch(`${UMBRACO_URL}/umbraco/delivery/api/v2${path}`, {
    headers: { "Api-Key": UMBRACO_KEY! },
    next: { revalidate: 60 }, // ISR — revalidate mỗi 60 giây
  });
  if (!res.ok) throw new Error(`Umbraco API error: ${res.status}`);
  return res.json();
}

// Fetch product từ custom Commerce API
export async function fetchProduct(slug: string) {
  const res = await fetch(`${process.env.API_URL}/api/v1/products/${slug}`, {
    next: { tags: [`product-${slug}`] }, // On-demand revalidation
  });
  if (!res.ok) throw new Error("Product not found");
  return res.json();
}
```

## Localization với next-intl

```typescript
// middleware.ts
import createMiddleware from "next-intl/middleware";

export default createMiddleware({
  locales: ["en", "vi", "fr"],
  defaultLocale: "en",
});
```

```tsx
// app/[locale]/layout.tsx
import { NextIntlClientProvider } from "next-intl";

export default async function LocaleLayout({ children, params: { locale } }) {
  const messages = await getMessages();
  return (
    <NextIntlClientProvider locale={locale} messages={messages}>
      {children}
    </NextIntlClientProvider>
  );
}
```

## Currency Display

```typescript
// lib/currency.ts
export function formatCurrency(amountInCents: number, currency: string, locale: string): string {
  return new Intl.NumberFormat(locale, {
    style: "currency",
    currency,
  }).format(amountInCents / 100);
}
// Ví dụ: formatCurrency(9999, "USD", "en-US") → "$99.99"
// Ví dụ: formatCurrency(100000, "VND", "vi-VN") → "100.000 ₫"
```

## Route Structure (App Router)

```
app/
├── [locale]/
│   ├── layout.tsx                # Root layout với locale + providers
│   ├── page.tsx                  # Homepage
│   ├── products/
│   │   ├── page.tsx              # Product listing
│   │   └── [slug]/
│   │       └── page.tsx          # Product detail
│   ├── cart/
│   │   └── page.tsx
│   ├── checkout/
│   │   └── page.tsx
│   └── account/
│       ├── layout.tsx            # Auth-protected layout
│       └── orders/
│           └── page.tsx
└── api/                          # Route handlers (API routes)
    └── revalidate/
        └── route.ts              # On-demand revalidation webhook
```

## Caching Strategy

- **Static pages** (homepage, category pages): `revalidate: 3600` (1 giờ)
- **Product pages**: Tag-based revalidation — revalidate khi product update
- **Cart, checkout, account**: No cache — `cache: "no-store"`
- **Search results**: `revalidate: 300` (5 phút)

## SEO

```tsx
// app/[locale]/products/[slug]/page.tsx
export async function generateMetadata({ params }) {
  const product = await fetchProduct(params.slug);
  return {
    title: product.name,
    description: product.description,
    openGraph: {
      images: [product.image],
    },
  };
}
```

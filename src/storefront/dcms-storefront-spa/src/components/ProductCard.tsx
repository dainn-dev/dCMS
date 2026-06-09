import { Link } from "react-router-dom";
import type { ProductListItem } from "../lib/api/catalogApi";
import { productDisplayName } from "../lib/api/catalogApi";

function formatMoney(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat(undefined, { style: "currency", currency }).format(amount);
  } catch {
    return `${amount} ${currency}`;
  }
}

export function ProductCard({ product }: { product: ProductListItem }) {
  const title = productDisplayName(product.name);
  return (
    <article className="sf-product-card">
      <Link to={`/p/${encodeURIComponent(product.slug)}`} className="sf-product-card__link">
        <h2 className="sf-product-card__title">{title}</h2>
        <p className="sf-product-card__price">
          {formatMoney(product.minBasePrice.amount, product.minBasePrice.currency)}
        </p>
        <p className="sf-product-card__stock">
          {product.hasInStockVariant ? "In stock" : "Out of stock"}
        </p>
      </Link>
    </article>
  );
}

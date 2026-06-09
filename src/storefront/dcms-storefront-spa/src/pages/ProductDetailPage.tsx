import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { VariantPicker } from "../components/VariantPicker";
import {
  getProductBySlug,
  productDisplayName,
  type ProductDetail,
  type VariantCombination,
} from "../lib/api/catalogApi";
import { useCart } from "../lib/cart/CartProvider";
import { useBranch } from "../lib/branch/BranchProvider";
import { useOptionalStoreScope } from "../lib/commerce/StoreContextProvider";

export function ProductDetailPage() {
  const { slug = "" } = useParams();
  const { bootstrap } = useBranch();
  const scope = useOptionalStoreScope();
  const { addLine } = useCart();
  const [product, setProduct] = useState<ProductDetail | null>(null);
  const [selected, setSelected] = useState<{ key: string; variant: VariantCombination } | null>(null);
  const [qty, setQty] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [added, setAdded] = useState(false);

  const tenantId = scope?.tenantId ?? "";
  const storeId = scope?.storeId ?? "";
  const warehouseId = scope?.warehouseId ?? "";

  useEffect(() => {
    if (!scope) return;
    let cancelled = false;
    setLoading(true);
    setError(null);
    setProduct(null);
    void getProductBySlug(slug, tenantId, storeId)
      .then(data => {
        if (!cancelled) setProduct(data);
      })
      .catch(e => {
        if (!cancelled) setError((e as Error).message);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, [slug, tenantId, storeId, scope]);

  useEffect(() => {
    if (!product) return;
    const first = Object.entries(product.variantMatrix.combinations).find(([, v]) => v.inStock);
    if (first) setSelected({ key: first[0], variant: first[1] });
  }, [product]);

  function addToCart() {
    if (!product || !selected || !scope) return;
    const currency = "VND";
    addLine({
      productId: product.id,
      variantId: selected.variant.variantId,
      sku: selected.variant.sku,
      name: productDisplayName(product.name),
      unitPrice: selected.variant.basePriceAmount,
      currency,
      quantity: qty,
      warehouseId,
    });
    setAdded(true);
  }

  if (bootstrap === "resolving" || !scope) {
    return <p className="sf-skeleton" role="status">Preparing your store…</p>;
  }
  if (loading) return <p className="sf-skeleton" role="status">Loading product…</p>;
  if (error) return <p className="sf-alert" role="alert">{error}</p>;
  if (!product) return <p className="sf-empty">Product not found.</p>;

  return (
    <section className="sf-page sf-product-detail">
      <p><Link to="/">← Back to products</Link></p>
      <h1>{productDisplayName(product.name)}</h1>
      <VariantPicker
        combinations={product.variantMatrix.combinations}
        onSelect={(key, variant) => setSelected({ key, variant })}
      />
      <div className="sf-add-to-cart">
        <label>
          Quantity
          <input
            type="number"
            min={1}
            value={qty}
            onChange={e => setQty(Math.max(1, Number(e.target.value) || 1))}
          />
        </label>
        <button
          type="button"
          className="sf-btn"
          disabled={!selected}
          onClick={addToCart}
        >
          Add to cart
        </button>
        {added && (
          <p role="status" className="sf-success">
            Added to cart. <Link to="/cart">View cart</Link>
          </p>
        )}
      </div>
    </section>
  );
}

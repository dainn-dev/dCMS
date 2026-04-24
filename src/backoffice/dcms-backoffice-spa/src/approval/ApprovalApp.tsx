import { useEffect, useState } from "react";
import {
  fetchPendingCampaigns,
  fetchPendingProducts,
  fetchPendingPromoCodes,
} from "./api/approvalApi";
import { approvalHashForPage, parseApprovalPageFromHash } from "./approvalHashRouting";
import { ApprovalLayout, type ApprovalPageId } from "./layout/ApprovalLayout";
import { CampaignApprovalPage } from "./pages/CampaignApprovalPage";
import { ProductApprovalPage } from "./pages/ProductApprovalPage";
import { PromoCodeApprovalPage } from "./pages/PromoCodeApprovalPage";

type Props = {
  tenantId?: string;
  storeId?: string;
  authToken?: string;
};

export function ApprovalApp({ tenantId, storeId, authToken }: Props) {
  const [page, setPage] = useState<ApprovalPageId>(() => parseApprovalPageFromHash() ?? "product-approval");
  const [productApprovalPendingCount, setProductApprovalPendingCount] = useState(0);
  const [campaignApprovalPendingCount, setCampaignApprovalPendingCount] = useState(0);
  const [promoApprovalPendingCount, setPromoApprovalPendingCount] = useState(0);

  function handlePageChange(id: ApprovalPageId) {
    setPage(id);
    const next = approvalHashForPage(id);
    if (window.location.hash !== next) {
      window.location.hash = next;
    }
  }

  useEffect(() => {
    function onHashChange() {
      const p = parseApprovalPageFromHash();
      if (!p) return;
      setPage(p);
    }
    window.addEventListener("hashchange", onHashChange);
    return () => window.removeEventListener("hashchange", onHashChange);
  }, []);

  useEffect(() => {
    const p = parseApprovalPageFromHash();
    if (!p) {
      window.location.hash = approvalHashForPage("product-approval");
      return;
    }
    const preferred = approvalHashForPage(p);
    if (window.location.hash !== preferred) {
      window.location.hash = preferred;
    }
  }, []);

  useEffect(() => {
    let cancelled = false;

    async function loadSidebarCounts() {
      if (!tenantId) {
        setProductApprovalPendingCount(0);
        setCampaignApprovalPendingCount(0);
        setPromoApprovalPendingCount(0);
        return;
      }

      try {
        const [camp, promo] = await Promise.all([
          fetchPendingCampaigns(tenantId, { page: 1, pageSize: 1 }, authToken),
          fetchPendingPromoCodes(tenantId, { page: 1, pageSize: 1 }, authToken),
        ]);
        if (cancelled) return;
        setCampaignApprovalPendingCount(camp.total);
        setPromoApprovalPendingCount(promo.total);
      } catch {
        if (!cancelled) {
          setCampaignApprovalPendingCount(0);
          setPromoApprovalPendingCount(0);
        }
      }

      if (!storeId) {
        if (!cancelled) setProductApprovalPendingCount(0);
        return;
      }

      try {
        const prod = await fetchPendingProducts(tenantId, storeId, { limit: 1 }, authToken);
        if (!cancelled) setProductApprovalPendingCount(prod.total);
      } catch {
        if (!cancelled) setProductApprovalPendingCount(0);
      }
    }

    void loadSidebarCounts();
    return () => {
      cancelled = true;
    };
  }, [tenantId, storeId, authToken]);

  return (
    <ApprovalLayout
      page={page}
      onPageChange={handlePageChange}
      productApprovalPendingCount={productApprovalPendingCount}
      campaignApprovalPendingCount={campaignApprovalPendingCount}
      promoApprovalPendingCount={promoApprovalPendingCount}
    >
      {page === "product-approval" && (
        <ProductApprovalPage
          tenantId={tenantId}
          storeId={storeId}
          authToken={authToken}
          onPendingCountChange={setProductApprovalPendingCount}
        />
      )}
      {page === "campaign-approval" && (
        <CampaignApprovalPage
          tenantId={tenantId}
          authToken={authToken}
          onPendingCountChange={setCampaignApprovalPendingCount}
        />
      )}
      {page === "promo-approval" && (
        <PromoCodeApprovalPage
          tenantId={tenantId}
          authToken={authToken}
          onPendingCountChange={setPromoApprovalPendingCount}
        />
      )}
    </ApprovalLayout>
  );
}

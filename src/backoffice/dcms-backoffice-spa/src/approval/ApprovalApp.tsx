import { useEffect, useState } from "react";
import { approvalHashForPage, parseApprovalPageFromHash } from "./approvalHashRouting";
import { ApprovalLayout, type ApprovalPageId } from "./layout/ApprovalLayout";
import { CampaignApprovalPage } from "./pages/CampaignApprovalPage";
import { ContentApprovalPage } from "./pages/ContentApprovalPage";
import { ProductApprovalPage } from "./pages/ProductApprovalPage";
import { PromoCodeApprovalPage } from "./pages/PromoCodeApprovalPage";

export function ApprovalApp() {
  const [page, setPage] = useState<ApprovalPageId>(() => parseApprovalPageFromHash() ?? "product-approval");
  const [productApprovalPendingCount, setProductApprovalPendingCount] = useState(5);
  const [contentApprovalPendingCount, setContentApprovalPendingCount] = useState(2);
  const [campaignApprovalPendingCount, setCampaignApprovalPendingCount] = useState(3);
  const [promoApprovalPendingCount, setPromoApprovalPendingCount] = useState(4);

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

  return (
    <ApprovalLayout
      page={page}
      onPageChange={handlePageChange}
      productApprovalPendingCount={productApprovalPendingCount}
      contentApprovalPendingCount={contentApprovalPendingCount}
      campaignApprovalPendingCount={campaignApprovalPendingCount}
      promoApprovalPendingCount={promoApprovalPendingCount}
    >
      {page === "product-approval" && (
        <ProductApprovalPage onPendingCountChange={setProductApprovalPendingCount} />
      )}
      {page === "content-approval" && (
        <ContentApprovalPage onPendingCountChange={setContentApprovalPendingCount} />
      )}
      {page === "campaign-approval" && (
        <CampaignApprovalPage onPendingCountChange={setCampaignApprovalPendingCount} />
      )}
      {page === "promo-approval" && (
        <PromoCodeApprovalPage onPendingCountChange={setPromoApprovalPendingCount} />
      )}
    </ApprovalLayout>
  );
}

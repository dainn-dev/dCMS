import { useState } from "react";
import { ApprovalLayout, type ApprovalPageId } from "./layout/ApprovalLayout";
import { CampaignApprovalPage } from "./pages/CampaignApprovalPage";
import { ContentApprovalPage } from "./pages/ContentApprovalPage";
import { ProductApprovalPage } from "./pages/ProductApprovalPage";
import { PromoCodeApprovalPage } from "./pages/PromoCodeApprovalPage";

export function ApprovalApp() {
  const [page, setPage] = useState<ApprovalPageId>("product-approval");
  const [productApprovalPendingCount, setProductApprovalPendingCount] = useState(5);
  const [contentApprovalPendingCount, setContentApprovalPendingCount] = useState(2);
  const [campaignApprovalPendingCount, setCampaignApprovalPendingCount] = useState(3);
  const [promoApprovalPendingCount, setPromoApprovalPendingCount] = useState(4);

  return (
    <ApprovalLayout
      page={page}
      onPageChange={setPage}
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

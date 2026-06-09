import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { AppShell } from "./components/AppShell";
import { RequireAuth } from "./components/RequireAuth";
import { BranchProvider } from "./lib/branch/BranchProvider";
import { CartProvider } from "./lib/cart/CartProvider";
import { StoreContextProvider } from "./lib/commerce/StoreContextProvider";
import { CustomerSessionProvider } from "./lib/session/CustomerSessionProvider";
import { CartPage } from "./pages/CartPage";
import { CheckoutPage } from "./pages/CheckoutPage";
import { CheckoutReturnPage } from "./pages/CheckoutReturnPage";
import { LoginPage } from "./pages/LoginPage";
import { OrderDetailPage } from "./pages/OrderDetailPage";
import { OrdersPage } from "./pages/OrdersPage";
import { PaymentPage } from "./pages/PaymentPage";
import { ProductDetailPage } from "./pages/ProductDetailPage";
import { ProductListPage } from "./pages/ProductListPage";

export function App() {
  return (
    <BranchProvider>
      <StoreContextProvider>
        <CustomerSessionProvider>
          <CartProvider>
            <BrowserRouter>
              <Routes>
                <Route element={<AppShell />}>
                  <Route index element={<ProductListPage />} />
                  <Route path="p/:slug" element={<ProductDetailPage />} />
                  <Route path="cart" element={<CartPage />} />
                  <Route path="login" element={<LoginPage />} />
                  <Route
                    path="checkout"
                    element={
                      <RequireAuth>
                        <CheckoutPage />
                      </RequireAuth>
                    }
                  />
                  <Route
                    path="checkout/pay/:orderId"
                    element={
                      <RequireAuth>
                        <PaymentPage />
                      </RequireAuth>
                    }
                  />
                  <Route
                    path="checkout/return"
                    element={
                      <RequireAuth>
                        <CheckoutReturnPage />
                      </RequireAuth>
                    }
                  />
                  <Route
                    path="orders"
                    element={
                      <RequireAuth>
                        <OrdersPage />
                      </RequireAuth>
                    }
                  />
                  <Route
                    path="orders/:orderId"
                    element={
                      <RequireAuth>
                        <OrderDetailPage />
                      </RequireAuth>
                    }
                  />
                  <Route path="*" element={<Navigate to="/" replace />} />
                </Route>
              </Routes>
            </BrowserRouter>
          </CartProvider>
        </CustomerSessionProvider>
      </StoreContextProvider>
    </BranchProvider>
  );
}

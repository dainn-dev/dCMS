import { GATEWAY } from "./gateway";
import { callEnvelopeJson } from "./commerceFetch";

export interface LoginResult {
  accessToken: string;
  expiresAt: string;
  role: string;
  tenantId: string | null;
  displayName: string;
  userId: string;
}

export async function login(email: string, password: string): Promise<LoginResult> {
  const { data } = await callEnvelopeJson<{
    accessToken: string;
    expiresAt: string;
    role: string;
    tenantId: string | null;
    displayName: string;
    userId: string;
  }>(`${GATEWAY.identity}/login`, {
    method: "POST",
    body: JSON.stringify({ email, password }),
  });
  return {
    accessToken: data.accessToken,
    expiresAt: data.expiresAt,
    role: data.role,
    tenantId: data.tenantId,
    displayName: data.displayName,
    userId: data.userId,
  };
}

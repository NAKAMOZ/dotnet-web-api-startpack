import { Effect } from "effect";
import { afterEach, describe, expect, it, vi } from "vitest";
import { endpointById } from "./catalog";
import { EMPTY_VARIABLES } from "./constants";
import {
	buildHeaders,
	buildPath,
	createCurl,
	createDraft,
	parseRequestBody,
	requestEndpoint,
} from "./domain";

const requireEndpoint = (id: string) => {
	const endpoint = endpointById(id);
	if (!endpoint) throw new Error(`Missing test endpoint: ${id}`);
	return endpoint;
};

afterEach(() => {
	vi.unstubAllGlobals();
});

describe("request materialization", () => {
	it("encodes path values and omits empty query parameters", () => {
		const endpoint = requireEndpoint("admin-audit");
		const draft = createDraft(endpoint, EMPTY_VARIABLES);
		draft.queryValues.userId = "a user/id";
		draft.queryValues.eventType = "";

		expect(buildPath(endpoint, draft, EMPTY_VARIABLES)).toBe(
			"/api/v1/admin/audit-logs?page=1&pageSize=20&sort=occurredAt%3Adesc&userId=a+user%2Fid",
		);
	});

	it("resolves vault templates inside nested JSON", async () => {
		const endpoint = requireEndpoint("auth-login-mfa");
		const variables = {
			...EMPTY_VARIABLES,
			mfaTicket: "ticket-1",
			totpCode: "123456",
		};
		const body = await Effect.runPromise(
			parseRequestBody(endpoint, JSON.stringify(endpoint.body), variables),
		);

		expect(body).toEqual({ mfaTicket: "ticket-1", code: "123456" });
	});

	it("builds transport-specific authorization and CSRF headers", () => {
		const endpoint = requireEndpoint("users-update");
		const variables = {
			...EMPTY_VARIABLES,
			accessToken: "access-token",
			apiKey: "api-key",
			csrfToken: "csrf-from-vault",
		};

		expect(buildHeaders(endpoint, "bearer", variables).Authorization).toBe(
			"Bearer access-token",
		);
		expect(buildHeaders(endpoint, "apiKey", variables).Authorization).toBe(
			"ApiKey api-key",
		);
		expect(buildHeaders(endpoint, "cookie", variables)["X-CSRF-Token"]).toBe(
			"csrf-from-vault",
		);
	});

	it("creates a reproducible cURL command", () => {
		const endpoint = requireEndpoint("auth-login");
		const command = createCurl({
			apiBase: "https://localhost:5001",
			endpoint,
			path: endpoint.path,
			body: { email: "user@example.test" },
			headers: { Accept: "application/json" },
		});

		expect(command).toContain("curl -i -X POST");
		expect(command).toContain("https://localhost:5001/api/v1/auth/login");
		expect(command).toContain("--data");
	});
});

describe("Effect HTTP service", () => {
	it("returns normalized response metadata", async () => {
		vi.stubGlobal(
			"fetch",
			vi.fn(
				async () =>
					new Response(JSON.stringify({ status: "Healthy" }), {
						status: 200,
						headers: { "x-correlation-id": "correlation-1" },
					}),
			),
		);
		const endpoint = requireEndpoint("health-live");

		const result = await Effect.runPromise(
			requestEndpoint({
				apiBase: "https://localhost:5001",
				endpoint,
				path: endpoint.path,
				headers: { Accept: "application/json" },
				authMode: "bearer",
			}),
		);

		expect(result.ok).toBe(true);
		expect(result.status).toBe(200);
		expect(result.data).toEqual({ status: "Healthy" });
		expect(result.headers["x-correlation-id"]).toBe("correlation-1");
	});
});

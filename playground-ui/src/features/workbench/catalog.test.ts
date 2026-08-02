import { describe, expect, it } from "vitest";
import { ENDPOINTS, endpointCopy } from "./catalog";

describe("endpoint catalog", () => {
	it("covers all 43 OpenAPI operations and two health probes", () => {
		expect(ENDPOINTS).toHaveLength(45);
		expect(
			ENDPOINTS.filter((item) => item.path.startsWith("/health/")),
		).toHaveLength(2);
	});

	it("uses stable, unique endpoint identifiers", () => {
		const ids = ENDPOINTS.map((item) => item.id);
		expect(new Set(ids).size).toBe(ids.length);
	});

	it("provides title and description copy in every supported locale", () => {
		for (const endpoint of ENDPOINTS) {
			for (const locale of ["en", "de", "tr"] as const) {
				const localized = endpointCopy(endpoint, locale);
				expect(localized.title.length).toBeGreaterThan(2);
				expect(localized.description.length).toBeGreaterThan(10);
			}
		}
	});
});

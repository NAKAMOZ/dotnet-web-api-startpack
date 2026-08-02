import { Effect } from "effect";
import { describe, expect, it } from "vitest";
import { computeTotp } from "./browser-effects";

describe("TOTP Effect", () => {
	it("matches the RFC 6238 SHA-1 vector at 59 seconds", async () => {
		const code = await Effect.runPromise(
			computeTotp("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", 59_000),
		);
		expect(code).toBe("287082");
	});
});

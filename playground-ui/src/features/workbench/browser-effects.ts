import { Data, Effect } from "effect";

export class TotpError extends Data.TaggedError("TotpError")<{
	readonly cause: unknown;
}> {}

export class WebAuthnError extends Data.TaggedError("WebAuthnError")<{
	readonly reason: string;
	readonly cause?: unknown;
}> {}

export const computeTotp = (
	secret: string,
	now = Date.now(),
): Effect.Effect<string, TotpError> =>
	Effect.tryPromise({
		try: async () => {
			const keyBytes = decodeBase32(secret);
			const counter = Math.floor(now / 1000 / 30);
			const counterBytes = new Uint8Array(8);
			let remaining = counter;
			for (let index = 7; index >= 0; index -= 1) {
				counterBytes[index] = remaining & 0xff;
				remaining = Math.floor(remaining / 256);
			}
			const key = await crypto.subtle.importKey(
				"raw",
				keyBytes,
				{ name: "HMAC", hash: "SHA-1" },
				false,
				["sign"],
			);
			const digest = new Uint8Array(
				await crypto.subtle.sign("HMAC", key, counterBytes),
			);
			const offset = (digest.at(-1) ?? 0) & 0x0f;
			const binary =
				(((digest[offset] ?? 0) & 0x7f) << 24) |
				((digest[offset + 1] ?? 0) << 16) |
				((digest[offset + 2] ?? 0) << 8) |
				(digest[offset + 3] ?? 0);
			return String(binary % 1_000_000).padStart(6, "0");
		},
		catch: (cause) => new TotpError({ cause }),
	});

export const createPasskey = (
	options: unknown,
): Effect.Effect<Credential, WebAuthnError> =>
	Effect.tryPromise({
		try: async () => {
			if (!globalThis.PublicKeyCredential || !navigator.credentials) {
				throw new Error("WebAuthn is unavailable");
			}
			const credential = await navigator.credentials.create({
				publicKey: decodeCreationOptions(options),
			});
			if (!credential)
				throw new Error("The authenticator returned no credential");
			return credential;
		},
		catch: (cause) =>
			new WebAuthnError({
				reason: cause instanceof Error ? cause.message : String(cause),
				cause,
			}),
	});

export const getPasskey = (
	options: unknown,
): Effect.Effect<Credential, WebAuthnError> =>
	Effect.tryPromise({
		try: async () => {
			if (!globalThis.PublicKeyCredential || !navigator.credentials) {
				throw new Error("WebAuthn is unavailable");
			}
			const credential = await navigator.credentials.get({
				publicKey: decodeRequestOptions(options),
			});
			if (!credential)
				throw new Error("The authenticator returned no assertion");
			return credential;
		},
		catch: (cause) =>
			new WebAuthnError({
				reason: cause instanceof Error ? cause.message : String(cause),
				cause,
			}),
	});

export const credentialToJson = (credential: Credential): unknown => {
	const publicKeyCredential = credential as PublicKeyCredential & {
		toJSON?: () => unknown;
		authenticatorAttachment?: string | null;
	};
	if (typeof publicKeyCredential.toJSON === "function") {
		return publicKeyCredential.toJSON();
	}

	const response = publicKeyCredential.response;
	const json: Record<string, unknown> = {
		id: publicKeyCredential.id,
		rawId: bytesToBase64Url(publicKeyCredential.rawId),
		type: publicKeyCredential.type,
		clientExtensionResults: publicKeyCredential.getClientExtensionResults(),
		authenticatorAttachment: publicKeyCredential.authenticatorAttachment,
		response: {
			clientDataJSON: bytesToBase64Url(response.clientDataJSON),
		},
	};
	const responseJson = json.response as Record<string, unknown>;
	if (response instanceof AuthenticatorAttestationResponse) {
		responseJson.attestationObject = bytesToBase64Url(
			response.attestationObject,
		);
		responseJson.transports = response.getTransports?.() ?? [];
	} else if (response instanceof AuthenticatorAssertionResponse) {
		responseJson.authenticatorData = bytesToBase64Url(
			response.authenticatorData,
		);
		responseJson.signature = bytesToBase64Url(response.signature);
		responseJson.userHandle = response.userHandle
			? bytesToBase64Url(response.userHandle)
			: null;
	}
	return json;
};

export const isWebAuthnAvailable = (): boolean =>
	typeof globalThis.PublicKeyCredential !== "undefined" &&
	typeof navigator !== "undefined" &&
	Boolean(navigator.credentials);

const decodeCreationOptions = (
	options: unknown,
): PublicKeyCredentialCreationOptions => {
	const copy = structuredClone(
		options,
	) as PublicKeyCredentialCreationOptions & {
		challenge: string | BufferSource;
		user: PublicKeyCredentialUserEntity & { id: string | BufferSource };
		excludeCredentials?: Array<
			PublicKeyCredentialDescriptor & { id: string | BufferSource }
		>;
	};
	if (typeof copy.challenge === "string")
		copy.challenge = base64UrlToBytes(copy.challenge);
	if (typeof copy.user.id === "string")
		copy.user.id = base64UrlToBytes(copy.user.id);
	if (Array.isArray(copy.excludeCredentials)) {
		copy.excludeCredentials = copy.excludeCredentials.map((credential) => ({
			...credential,
			id:
				typeof credential.id === "string"
					? base64UrlToBytes(credential.id)
					: credential.id,
		}));
	}
	return copy;
};

const decodeRequestOptions = (
	options: unknown,
): PublicKeyCredentialRequestOptions => {
	const copy = structuredClone(options) as PublicKeyCredentialRequestOptions & {
		challenge: string | BufferSource;
		allowCredentials?: Array<
			PublicKeyCredentialDescriptor & { id: string | BufferSource }
		>;
	};
	if (typeof copy.challenge === "string")
		copy.challenge = base64UrlToBytes(copy.challenge);
	if (Array.isArray(copy.allowCredentials)) {
		copy.allowCredentials = copy.allowCredentials.map((credential) => ({
			...credential,
			id:
				typeof credential.id === "string"
					? base64UrlToBytes(credential.id)
					: credential.id,
		}));
	}
	return copy;
};

const decodeBase32 = (value: string): Uint8Array<ArrayBuffer> => {
	const alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
	const clean = value.toUpperCase().replace(/=+$/g, "").replace(/\s+/g, "");
	let bits = "";
	for (const character of clean) {
		const index = alphabet.indexOf(character);
		if (index < 0) throw new Error("Invalid Base32");
		bits += index.toString(2).padStart(5, "0");
	}
	const bytes: Array<number> = [];
	for (let index = 0; index + 8 <= bits.length; index += 8) {
		bytes.push(Number.parseInt(bits.slice(index, index + 8), 2));
	}
	return new Uint8Array(bytes);
};

const base64UrlToBytes = (value: string): Uint8Array<ArrayBuffer> => {
	const base64 = value
		.replace(/-/g, "+")
		.replace(/_/g, "/")
		.padEnd(Math.ceil(value.length / 4) * 4, "=");
	return Uint8Array.from(atob(base64), (character) => character.charCodeAt(0));
};

const bytesToBase64Url = (buffer: ArrayBuffer): string => {
	const bytes = new Uint8Array(buffer);
	let binary = "";
	for (const byte of bytes) binary += String.fromCharCode(byte);
	return btoa(binary)
		.replace(/\+/g, "-")
		.replace(/\//g, "_")
		.replace(/=+$/g, "");
};

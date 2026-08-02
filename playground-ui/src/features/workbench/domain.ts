import { Data, Effect } from "effect";
import { SESSION_ISSUING_ENDPOINTS, STORAGE_KEYS } from "./constants";
import type {
	ApiResult,
	AuthMode,
	EndpointDefinition,
	RequestDraft,
	WorkbenchVariables,
} from "./types";

export class NetworkRequestError extends Data.TaggedError(
	"NetworkRequestError",
)<{
	readonly reason: string;
	readonly cause: unknown;
}> {}

export class InvalidJsonError extends Data.TaggedError("InvalidJsonError")<{
	readonly cause: unknown;
}> {}

export class BrowserStorageError extends Data.TaggedError(
	"BrowserStorageError",
)<{
	readonly cause: unknown;
}> {}

export class ClipboardError extends Data.TaggedError("ClipboardError")<{
	readonly cause: unknown;
}> {}

export interface ApiRequestInput {
	apiBase: string;
	endpoint: EndpointDefinition;
	path: string;
	body?: unknown;
	headers: Record<string, string>;
	authMode: AuthMode;
}

export interface ProbeResult {
	ok: boolean;
	operationCount?: number;
}

const now = () => globalThis.performance?.now() ?? Date.now();

export const requestEndpoint = (
	input: ApiRequestInput,
): Effect.Effect<ApiResult, NetworkRequestError> =>
	Effect.gen(function* () {
		const started = now();
		const response = yield* Effect.tryPromise({
			try: (signal) =>
				fetch(`${input.apiBase}${input.path}`, {
					method: input.endpoint.method,
					headers: input.headers,
					body:
						input.body === undefined ? undefined : JSON.stringify(input.body),
					credentials: input.authMode === "cookie" ? "include" : "omit",
					signal,
				}),
			catch: (cause) =>
				new NetworkRequestError({
					reason: cause instanceof Error ? cause.message : String(cause),
					cause,
				}),
		});

		const raw = yield* Effect.tryPromise({
			try: () => response.text(),
			catch: (cause) =>
				new NetworkRequestError({
					reason: cause instanceof Error ? cause.message : String(cause),
					cause,
				}),
		});

		return {
			status: response.status,
			statusText: response.statusText,
			ok: response.ok,
			raw,
			data: parseResponse(raw),
			elapsedMs: Math.round(now() - started),
			headers: Object.fromEntries(response.headers.entries()),
		};
	});

export const probeService = (
	url: string,
	isOpenApi = false,
): Effect.Effect<ProbeResult, NetworkRequestError> =>
	Effect.tryPromise({
		try: (signal) =>
			fetch(url, {
				headers: { Accept: isOpenApi ? "application/json" : "text/plain" },
				credentials: "include",
				signal,
			}),
		catch: (cause) =>
			new NetworkRequestError({
				reason: cause instanceof Error ? cause.message : String(cause),
				cause,
			}),
	}).pipe(
		Effect.flatMap((response) => {
			if (!response.ok) {
				return Effect.fail(
					new NetworkRequestError({
						reason: `HTTP ${response.status}`,
						cause: response,
					}),
				);
			}
			if (!isOpenApi) return Effect.succeed({ ok: true });
			return Effect.tryPromise({
				try: () => response.json() as Promise<unknown>,
				catch: (cause) =>
					new NetworkRequestError({
						reason: "Invalid OpenAPI document",
						cause,
					}),
			}).pipe(
				Effect.map((document) => ({
					ok: true,
					operationCount: countOpenApiOperations(document),
				})),
			);
		}),
	);

export const parseRequestBody = (
	endpoint: EndpointDefinition,
	bodyText: string,
	variables: WorkbenchVariables,
): Effect.Effect<unknown | undefined, InvalidJsonError> => {
	if (endpoint.body === null) return Effect.succeed(undefined);

	return Effect.try({
		try: () => materialize(JSON.parse(bodyText || "{}"), variables),
		catch: (cause) => new InvalidJsonError({ cause }),
	});
};

export const parseResponse = (raw: string): unknown => {
	if (!raw) return null;
	try {
		return JSON.parse(raw);
	} catch {
		return raw;
	}
};

export const formatResponse = (
	result: ApiResult,
	emptyLabel: string,
): string => {
	if (!result.raw) return emptyLabel;
	return typeof result.data === "object" && result.data !== null
		? JSON.stringify(result.data, null, 2)
		: result.raw;
};

export const resolveTemplateString = (
	value: string,
	variables: WorkbenchVariables,
): string =>
	value.replace(/\{\{([a-zA-Z0-9]+)\}\}/g, (match, name: string) => {
		const replacement = variables[name as keyof WorkbenchVariables];
		return replacement || match;
	});

export const materialize = (
	value: unknown,
	variables: WorkbenchVariables,
): unknown => {
	if (Array.isArray(value)) {
		return value.map((item) => materialize(item, variables));
	}
	if (value && typeof value === "object") {
		return Object.fromEntries(
			Object.entries(value).map(([key, child]) => [
				key,
				materialize(child, variables),
			]),
		);
	}
	return typeof value === "string"
		? resolveTemplateString(value, variables)
		: value;
};

export const createDraft = (
	endpoint: EndpointDefinition,
	variables: WorkbenchVariables,
): RequestDraft => ({
	pathValues: Object.fromEntries(
		endpoint.pathParams.map((parameter) => [
			parameter.name,
			resolveTemplateString(parameter.value, variables),
		]),
	),
	queryValues: Object.fromEntries(
		endpoint.query.map((parameter) => [
			parameter.name,
			resolveTemplateString(parameter.value, variables),
		]),
	),
	bodyText:
		endpoint.body === null
			? ""
			: JSON.stringify(materialize(endpoint.body, variables), null, 2),
});

export const buildPath = (
	endpoint: EndpointDefinition,
	draft: Pick<RequestDraft, "pathValues" | "queryValues">,
	variables: WorkbenchVariables,
): string => {
	let path = endpoint.path;
	for (const parameter of endpoint.pathParams) {
		const value = resolveTemplateString(
			draft.pathValues[parameter.name] ?? parameter.value,
			variables,
		);
		path = path.replace(`{${parameter.name}}`, encodeURIComponent(value));
	}

	const query = new URLSearchParams();
	for (const parameter of endpoint.query) {
		const value = resolveTemplateString(
			draft.queryValues[parameter.name] ?? parameter.value,
			variables,
		);
		if (value && !value.startsWith("{{")) query.set(parameter.name, value);
	}

	const encoded = query.toString();
	return encoded ? `${path}?${encoded}` : path;
};

export const buildPathFromDefinition = (
	endpoint: EndpointDefinition,
	variables: WorkbenchVariables,
): string => buildPath(endpoint, createDraft(endpoint, variables), variables);

export const buildHeaders = (
	endpoint: EndpointDefinition,
	authMode: AuthMode,
	variables: WorkbenchVariables,
	cookieValue = "",
): Record<string, string> => {
	const headers: Record<string, string> = { Accept: "application/json" };
	if (endpoint.body !== null) headers["Content-Type"] = "application/json";

	if (endpoint.auth !== "public") {
		if (authMode === "bearer" && variables.accessToken) {
			headers.Authorization = `Bearer ${variables.accessToken}`;
		}
		if (authMode === "apiKey" && variables.apiKey) {
			headers.Authorization = `ApiKey ${variables.apiKey}`;
		}
	}

	if (authMode === "cookie" && isUnsafe(endpoint.method)) {
		const csrf =
			readCookie("__Host-auth.csrf", cookieValue) || variables.csrfToken;
		if (csrf) headers["X-CSRF-Token"] = csrf;
	}

	if (authMode === "cookie" && SESSION_ISSUING_ENDPOINTS.has(endpoint.id)) {
		headers["X-Auth-Transport"] = "cookie";
	}

	return headers;
};

export const redactHeader = (name: string, value: string): string => {
	if (name.toLowerCase() !== "authorization") return value;
	const [scheme, token = ""] = value.split(" ", 2);
	if (token.length < 20) return `${scheme} ••••••••`;
	return `${scheme} ${token.slice(0, 12)}…${token.slice(-5)}`;
};

export const createCurl = (
	input: Omit<ApiRequestInput, "authMode">,
): string => {
	const url = `${input.apiBase}${input.path}`;
	const headerArguments = Object.entries(input.headers)
		.map(([name, value]) => `-H '${shellEscape(`${name}: ${value}`)}'`)
		.join(" \\\n  ");
	const bodyArgument =
		input.body === undefined
			? ""
			: ` \\\n  --data '${shellEscape(JSON.stringify(input.body))}'`;
	return `curl -i -X ${input.endpoint.method} '${shellEscape(url)}'${
		headerArguments ? ` \\\n  ${headerArguments}` : ""
	}${bodyArgument}`;
};

export const readVault = (): Effect.Effect<
	Partial<WorkbenchVariables> & { authMode?: AuthMode },
	BrowserStorageError
> =>
	Effect.try({
		try: () => {
			const authModeValue = sessionStorage.getItem(STORAGE_KEYS.authMode);
			const authMode =
				authModeValue === "cookie" || authModeValue === "apiKey"
					? authModeValue
					: "bearer";
			return {
				authMode,
				accessToken: sessionStorage.getItem(STORAGE_KEYS.accessToken) || "",
				refreshToken: sessionStorage.getItem(STORAGE_KEYS.refreshToken) || "",
				apiKey: sessionStorage.getItem(STORAGE_KEYS.apiKey) || "",
				csrfToken: sessionStorage.getItem(STORAGE_KEYS.csrfToken) || "",
				totpSecret: sessionStorage.getItem(STORAGE_KEYS.totpSecret) || "",
				mfaTicket: sessionStorage.getItem(STORAGE_KEYS.mfaTicket) || "",
				credentialId: sessionStorage.getItem(STORAGE_KEYS.credentialId) || "",
				socialCode: sessionStorage.getItem(STORAGE_KEYS.socialCode) || "",
				socialState: sessionStorage.getItem(STORAGE_KEYS.socialState) || "",
			};
		},
		catch: (cause) => new BrowserStorageError({ cause }),
	});

export const writeVaultValue = (
	name: keyof typeof STORAGE_KEYS,
	value: string,
): Effect.Effect<void, BrowserStorageError> =>
	Effect.try({
		try: () => {
			if (value) sessionStorage.setItem(STORAGE_KEYS[name], value);
			else sessionStorage.removeItem(STORAGE_KEYS[name]);
		},
		catch: (cause) => new BrowserStorageError({ cause }),
	});

export const clearVaultStorage = (): Effect.Effect<void, BrowserStorageError> =>
	Effect.try({
		try: () => {
			for (const [name, key] of Object.entries(STORAGE_KEYS)) {
				if (name !== "authMode") sessionStorage.removeItem(key);
			}
		},
		catch: (cause) => new BrowserStorageError({ cause }),
	});

export const copyText = (value: string): Effect.Effect<void, ClipboardError> =>
	Effect.tryPromise({
		try: () => navigator.clipboard.writeText(value),
		catch: (cause) => new ClipboardError({ cause }),
	});

export const readCookie = (name: string, cookieValue: string): string => {
	const prefix = `${encodeURIComponent(name)}=`;
	const pair = cookieValue
		.split("; ")
		.find((candidate) => candidate.startsWith(prefix));
	return pair ? decodeURIComponent(pair.slice(prefix.length)) : "";
};

export const isUnsafe = (method: string): boolean =>
	!["GET", "HEAD", "OPTIONS", "TRACE"].includes(method);

const shellEscape = (value: string): string => value.replace(/'/g, `'"'"'`);

const countOpenApiOperations = (document: unknown): number => {
	if (!document || typeof document !== "object" || !("paths" in document))
		return 0;
	const paths = document.paths;
	if (!paths || typeof paths !== "object") return 0;
	const methods = new Set(["get", "post", "put", "patch", "delete"]);
	return Object.values(paths).reduce((total, pathItem) => {
		if (!pathItem || typeof pathItem !== "object") return total;
		return (
			total +
			Object.keys(pathItem).filter((method) => methods.has(method)).length
		);
	}, 0);
};

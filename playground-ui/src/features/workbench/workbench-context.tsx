import { Cause, Effect, Exit, Option } from "effect";
import {
	createContext,
	type ReactNode,
	useCallback,
	useContext,
	useEffect,
	useMemo,
	useRef,
	useState,
} from "react";
import { m } from "#/paraglide/messages";
import { computeTotp } from "./browser-effects";
import {
	EMPTY_VARIABLES,
	SESSION_ISSUING_ENDPOINTS,
	STORAGE_KEYS,
} from "./constants";
import {
	buildHeaders,
	buildPathFromDefinition,
	clearVaultStorage,
	probeService,
	readCookie,
	readVault,
	requestEndpoint,
	writeVaultValue,
} from "./domain";
import type {
	ApiResult,
	AuthMode,
	EndpointDefinition,
	ServiceStatus,
	ToastMessage,
	WorkbenchResponse,
	WorkbenchVariables,
} from "./types";

interface RunRequestOptions {
	endpoint: EndpointDefinition;
	path?: string;
	body?: unknown;
	headers?: Record<string, string>;
	display?: boolean;
	reportError?: boolean;
	authMode?: AuthMode;
}

interface WorkbenchContextValue {
	apiBase: string;
	setApiBase: (value: string) => void;
	authMode: AuthMode;
	setAuthMode: (mode: AuthMode) => void;
	variables: WorkbenchVariables;
	setVariable: (name: keyof WorkbenchVariables, value: string) => void;
	clearVault: () => void;
	response: WorkbenchResponse | null;
	clearResponse: () => void;
	isRunning: boolean;
	runRequest: (options: RunRequestOptions) => Promise<ApiResult | null>;
	services: Record<"live" | "ready" | "openapi", ServiceStatus>;
	checkServices: () => void;
	toasts: ReadonlyArray<ToastMessage>;
	pushToast: (
		title: string,
		description: string,
		tone?: ToastMessage["tone"],
	) => void;
	dismissToast: (id: number) => void;
}

const WorkbenchContext = createContext<WorkbenchContextValue | null>(null);

const INITIAL_SERVICES: WorkbenchContextValue["services"] = {
	live: { state: "checking" },
	ready: { state: "checking" },
	openapi: { state: "checking" },
};

export function WorkbenchProvider({ children }: { children: ReactNode }) {
	const [apiBase, setApiBaseState] = useState("");
	const [authMode, setAuthModeState] = useState<AuthMode>("bearer");
	const [variables, setVariables] =
		useState<WorkbenchVariables>(EMPTY_VARIABLES);
	const [response, setResponse] = useState<WorkbenchResponse | null>(null);
	const [isRunning, setIsRunning] = useState(false);
	const [services, setServices] = useState(INITIAL_SERVICES);
	const [toasts, setToasts] = useState<Array<ToastMessage>>([]);
	const abortRef = useRef<AbortController | null>(null);
	const toastId = useRef(0);

	const dismissToast = useCallback((id: number) => {
		setToasts((current) => current.filter((toast) => toast.id !== id));
	}, []);

	const pushToast = useCallback(
		(
			title: string,
			description: string,
			tone: ToastMessage["tone"] = "neutral",
		) => {
			const id = ++toastId.current;
			setToasts((current) => [...current, { id, title, description, tone }]);
			if (tone === "neutral") {
				window.setTimeout(() => dismissToast(id), 4200);
			}
		},
		[dismissToast],
	);

	const setVariable = useCallback(
		(name: keyof WorkbenchVariables, value: string) => {
			setVariables((current) => ({ ...current, [name]: value }));
			if (name in STORAGE_KEYS) {
				Effect.runFork(
					writeVaultValue(name as keyof typeof STORAGE_KEYS, value).pipe(
						Effect.catchAll(() => Effect.void),
					),
				);
			}
		},
		[],
	);

	const setAuthMode = useCallback((mode: AuthMode) => {
		setAuthModeState(mode);
		Effect.runFork(
			writeVaultValue("authMode", mode).pipe(
				Effect.catchAll(() => Effect.void),
			),
		);
	}, []);

	const clearVault = useCallback(() => {
		setVariables(EMPTY_VARIABLES);
		Effect.runFork(
			clearVaultStorage().pipe(Effect.catchAll(() => Effect.void)),
		);
		pushToast(m.vault_cleared(), m.vault_cleared_description());
	}, [pushToast]);

	const captureResponseData = useCallback(
		(endpoint: EndpointDefinition, result: ApiResult): string | undefined => {
			const data = asRecord(result.data);
			if (!data) return undefined;

			if (typeof data.accessToken === "string") {
				setVariable("accessToken", data.accessToken);
			}
			if (typeof data.refreshToken === "string") {
				setVariable("refreshToken", data.refreshToken);
			}
			if (typeof data.key === "string") setVariable("apiKey", data.key);
			if (typeof data.mfaTicket === "string") {
				setVariable("mfaTicket", data.mfaTicket);
			}
			if (typeof data.secret === "string") {
				setVariable("totpSecret", data.secret);
			}
			if (endpoint.id === "auth-csrf" && typeof data.token === "string") {
				setVariable("csrfToken", data.token);
			}
			if (
				endpoint.id === "social-authorize" &&
				typeof data.authorizationUrl === "string"
			) {
				const url = new URL(data.authorizationUrl, apiBase);
				setVariable("socialCode", url.searchParams.get("code") || "");
				setVariable("socialState", url.searchParams.get("state") || "");
			}
			if (endpoint.id === "passkeys-list" && Array.isArray(result.data)) {
				const first = asRecord(result.data[0]);
				if (typeof first?.credentialId === "string") {
					setVariable("credentialId", first.credentialId);
				}
			}

			if (endpoint.id === "auth-login" && result.status === 202) {
				return m.response_notice_mfa();
			}
			if (
				SESSION_ISSUING_ENDPOINTS.has(endpoint.id) &&
				result.ok &&
				authMode === "cookie"
			) {
				const csrf = readCookie("__Host-auth.csrf", document.cookie);
				if (csrf) setVariable("csrfToken", csrf);
				return m.response_notice_cookie();
			}
			if (typeof data.accessToken === "string")
				return m.response_notice_tokens();
			if (typeof data.key === "string") return m.response_notice_key();
			if (typeof data.secret === "string") return m.response_notice_totp();
			if (Array.isArray(data.codes)) return m.response_notice_recovery();
			return undefined;
		},
		[apiBase, authMode, setVariable],
	);

	const runRequest = useCallback(
		async (options: RunRequestOptions): Promise<ApiResult | null> => {
			abortRef.current?.abort();
			const controller = new AbortController();
			abortRef.current = controller;
			setIsRunning(true);
			const cookieValue =
				typeof document === "undefined" ? "" : document.cookie;
			const effectiveAuthMode = options.authMode ?? authMode;
			const effect = requestEndpoint({
				apiBase,
				endpoint: options.endpoint,
				path:
					options.path ?? buildPathFromDefinition(options.endpoint, variables),
				body: options.body,
				headers: {
					...buildHeaders(
						options.endpoint,
						effectiveAuthMode,
						variables,
						cookieValue,
					),
					...options.headers,
				},
				authMode: effectiveAuthMode,
			});

			const exit = await Effect.runPromiseExit(effect, {
				signal: controller.signal,
			});
			if (abortRef.current === controller) {
				abortRef.current = null;
				setIsRunning(false);
			}

			if (Exit.isFailure(exit)) {
				const failure = Cause.failureOption(exit.cause);
				if (Option.isSome(failure) && options.reportError !== false) {
					pushToast(
						m.request_failed(),
						m.request_failed_description({ reason: failure.value.reason }),
						"error",
					);
				}
				return null;
			}

			const notice = captureResponseData(options.endpoint, exit.value);
			if (options.display !== false) {
				setResponse({
					...exit.value,
					endpointId: options.endpoint.id,
					notice,
				});
			}
			return exit.value;
		},
		[apiBase, authMode, captureResponseData, pushToast, variables],
	);

	const checkServices = useCallback(() => {
		if (!apiBase) return;
		setServices(INITIAL_SERVICES);
		const checks = [
			["live", "/health/live", false],
			["ready", "/health/ready", false],
			["openapi", "/openapi/v1.json", true],
		] as const;
		for (const [name, path, isOpenApi] of checks) {
			Effect.runPromiseExit(probeService(`${apiBase}${path}`, isOpenApi)).then(
				(exit) => {
					setServices((current) => ({
						...current,
						[name]: Exit.isSuccess(exit)
							? {
									state: "healthy",
									operationCount: exit.value.operationCount,
								}
							: { state: "unhealthy" },
					}));
				},
			);
		}
	}, [apiBase]);

	useEffect(() => {
		setApiBaseState(window.location.origin);
		Effect.runPromiseExit(readVault()).then((exit) => {
			if (!Exit.isSuccess(exit)) return;
			const { authMode: storedMode, ...storedVariables } = exit.value;
			if (storedMode) setAuthModeState(storedMode);
			setVariables((current) => ({ ...current, ...storedVariables }));
		});
		return () => abortRef.current?.abort();
	}, []);

	useEffect(() => {
		if (!apiBase) return;
		checkServices();
	}, [apiBase, checkServices]);

	useEffect(() => {
		if (!variables.totpSecret) return;
		let active = true;
		const update = () => {
			Effect.runPromiseExit(computeTotp(variables.totpSecret)).then((exit) => {
				if (active && Exit.isSuccess(exit)) setVariable("totpCode", exit.value);
			});
		};
		update();
		const interval = window.setInterval(update, 1000);
		return () => {
			active = false;
			window.clearInterval(interval);
		};
	}, [setVariable, variables.totpSecret]);

	const value = useMemo<WorkbenchContextValue>(
		() => ({
			apiBase,
			setApiBase: (value) => setApiBaseState(value.replace(/\/+$/, "")),
			authMode,
			setAuthMode,
			variables,
			setVariable,
			clearVault,
			response,
			clearResponse: () => setResponse(null),
			isRunning,
			runRequest,
			services,
			checkServices,
			toasts,
			pushToast,
			dismissToast,
		}),
		[
			apiBase,
			authMode,
			checkServices,
			clearVault,
			dismissToast,
			isRunning,
			pushToast,
			response,
			runRequest,
			services,
			setAuthMode,
			setVariable,
			toasts,
			variables,
		],
	);

	return (
		<WorkbenchContext.Provider value={value}>
			{children}
		</WorkbenchContext.Provider>
	);
}

export const useWorkbench = (): WorkbenchContextValue => {
	const context = useContext(WorkbenchContext);
	if (!context)
		throw new Error("useWorkbench must be used inside WorkbenchProvider");
	return context;
};

const asRecord = (value: unknown): Record<string, unknown> | null =>
	value !== null && typeof value === "object"
		? (value as Record<string, unknown>)
		: null;

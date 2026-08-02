import { useEffect, useRef } from "react";
import { m } from "#/paraglide/messages";
import { AppHeader } from "./app-header";
import { endpointById } from "./catalog";
import { DEMO } from "./constants";
import { EndpointCatalog } from "./endpoint-catalog";
import { EndpointDetail } from "./endpoint-detail";
import { IdentityPanel, type ScenarioActions } from "./identity-panel";
import { ToastRegion } from "./toast-region";
import type { AuthMode, WorkbenchSearch } from "./types";
import { useWorkbench } from "./workbench-context";

interface WorkbenchScreenProps {
	endpointId: string | null;
	search: WorkbenchSearch;
	onSearchChange: (next: Partial<WorkbenchSearch>) => void;
	onNavigateEndpoint: (endpointId: string) => void;
	onBack: () => void;
}

export function WorkbenchScreen({
	endpointId,
	search,
	onSearchChange,
	onNavigateEndpoint,
	onBack,
}: WorkbenchScreenProps) {
	const { authMode, setAuthMode, setVariable, runRequest, pushToast } =
		useWorkbench();
	const searchRef = useRef<HTMLInputElement>(null);
	const endpoint = endpointId ? endpointById(endpointId) : undefined;

	const ensureCsrf = async () => {
		const csrf = endpointById("auth-csrf");
		if (!csrf) return;
		await runRequest({
			endpoint: csrf,
			display: false,
			reportError: false,
			authMode: "cookie",
		});
	};

	const demoLogin: ScenarioActions["demoLogin"] = async (accountName) => {
		const login = endpointById("auth-login");
		if (!login) return;
		const account = DEMO[accountName];
		const effectiveMode: AuthMode = authMode === "apiKey" ? "bearer" : authMode;
		setAuthMode(effectiveMode);
		onNavigateEndpoint(login.id);
		const result = await runRequest({
			endpoint: login,
			body: { email: account.email, password: account.password },
			authMode: effectiveMode,
		});
		if (result?.ok && result.status !== 202) {
			pushToast(
				m.login_ready({
					account: accountName === "admin" ? "Admin" : account.email,
				}),
				account.email,
			);
			if (effectiveMode === "cookie") await ensureCsrf();
		} else if (result?.status === 202) {
			pushToast(m.mfa_required(), m.mfa_required_description());
		}
	};

	const prepareSessions: ScenarioActions["prepareSessions"] = async () => {
		const login = endpointById("auth-login");
		if (!login) return;
		setAuthMode("bearer");
		const options = {
			endpoint: login,
			body: { email: DEMO.user.email, password: DEMO.user.password },
			display: false,
			reportError: false,
			authMode: "bearer" as const,
		};
		const first = await runRequest(options);
		if (!first?.ok) {
			pushToast(m.scenario_failed(), m.scenario_failed_description(), "error");
			return;
		}
		const second = await runRequest(options);
		if (second?.ok) {
			pushToast(m.scenario_ready(), m.scenario_ready_description());
			onNavigateEndpoint("sessions-list");
		} else {
			pushToast(m.scenario_failed(), m.scenario_failed_description(), "error");
		}
	};

	const useDemoApiKey: ScenarioActions["useDemoApiKey"] = () => {
		setVariable("apiKey", DEMO.apiKey);
		setAuthMode("apiKey");
		pushToast(m.demo_key_ready(), m.demo_key_ready_description());
	};

	const runSocial: ScenarioActions["runSocial"] = async (provider) => {
		const authorize = endpointById("social-authorize");
		const callback = endpointById("social-callback");
		if (!authorize || !callback) return;
		setAuthMode("bearer");
		pushToast(m.oauth_started(), m.oauth_started_description({ provider }));
		const first = await runRequest({
			endpoint: authorize,
			path: `/api/v1/auth/social/${provider}/authorize`,
			display: false,
			reportError: false,
			authMode: "bearer",
		});
		const data = asRecord(first?.data);
		if (!first?.ok || typeof data?.authorizationUrl !== "string") {
			pushToast(m.oauth_failed(), m.oauth_failed_description(), "error");
			return;
		}
		const callbackUrl = new URL(data.authorizationUrl, window.location.origin);
		setVariable("socialCode", callbackUrl.searchParams.get("code") || "");
		setVariable("socialState", callbackUrl.searchParams.get("state") || "");
		onNavigateEndpoint(callback.id);
		const second = await runRequest({
			endpoint: callback,
			path: `${callbackUrl.pathname}${callbackUrl.search}`,
			authMode: "bearer",
		});
		if (second?.ok) {
			pushToast(m.oauth_ready({ provider }), m.oauth_ready_description());
		}
	};

	const actions: ScenarioActions = {
		demoLogin,
		prepareSessions,
		useDemoApiKey,
		runSocial,
	};

	useEffect(() => {
		const handleShortcut = (event: KeyboardEvent) => {
			const editable =
				document.activeElement instanceof HTMLInputElement ||
				document.activeElement instanceof HTMLTextAreaElement;
			if (event.key === "/" && !editable) {
				event.preventDefault();
				searchRef.current?.focus();
			}
		};
		document.addEventListener("keydown", handleShortcut);
		return () => document.removeEventListener("keydown", handleShortcut);
	}, []);

	return (
		<div className="min-h-screen bg-background text-foreground">
			<a href="#endpoint-catalog" className="skip-link">
				{m.skip_to_content()}
			</a>
			<AppHeader />
			<p className="sr-only">{m.keyboard_shortcuts()}</p>
			<div
				className={`mx-auto grid max-w-[1920px] ${endpoint ? "has-selection" : ""} lg:grid-cols-[17rem_minmax(0,1fr)] xl:grid-cols-[17rem_minmax(28rem,1fr)_minmax(27rem,34rem)]`}
			>
				<IdentityPanel actions={actions} search={search} />
				<div className={endpoint ? "hidden lg:block" : "block"}>
					<EndpointCatalog
						selectedId={endpointId}
						search={search}
						onSearchChange={onSearchChange}
						searchRef={searchRef}
					/>
				</div>
				{endpoint ? (
					<div className="lg:col-start-2 xl:col-start-3 xl:row-start-1">
						<EndpointDetail
							key={endpoint.id}
							endpoint={endpoint}
							onBack={onBack}
							onNavigateEndpoint={onNavigateEndpoint}
							onRunSocial={runSocial}
						/>
					</div>
				) : null}
			</div>
			<ToastRegion />
		</div>
	);
}

const asRecord = (value: unknown): Record<string, unknown> | null =>
	value !== null && typeof value === "object"
		? (value as Record<string, unknown>)
		: null;

import {
	CheckCircleIcon,
	CircleNotchIcon,
	CopyIcon,
	DatabaseIcon,
	GithubLogoIcon,
	GoogleLogoIcon,
	KeyIcon,
	ShieldCheckIcon,
	SignInIcon,
	TrashIcon,
	UsersThreeIcon,
	WarningCircleIcon,
} from "@phosphor-icons/react";
import { Link } from "@tanstack/react-router";
import { Effect, Exit } from "effect";
import { Button, buttonVariants } from "#/components/ui/button";
import { Input } from "#/components/ui/input";
import { Label } from "#/components/ui/label";
import {
	Select,
	SelectContent,
	SelectItem,
	SelectTrigger,
	SelectValue,
} from "#/components/ui/select";
import { cn } from "#/lib/utils";
import { m } from "#/paraglide/messages";
import { DEMO } from "./constants";
import { copyText } from "./domain";
import type { AuthMode, WorkbenchSearch } from "./types";
import { useWorkbench } from "./workbench-context";

export interface ScenarioActions {
	demoLogin: (account: "admin" | "user") => void;
	prepareSessions: () => void;
	useDemoApiKey: () => void;
	runSocial: (provider: "google" | "github") => void;
}

interface IdentityPanelProps {
	actions: ScenarioActions;
	search: WorkbenchSearch;
}

export function IdentityPanel({ actions, search }: IdentityPanelProps) {
	return (
		<>
			<details className="border border-border bg-card lg:hidden">
				<summary className="cursor-pointer px-4 py-3 font-heading text-sm font-bold uppercase tracking-widest">
					{m.identity_vault()}
				</summary>
				<div className="border-t border-border p-4">
					<IdentityPanelContent actions={actions} search={search} />
				</div>
			</details>
			<aside className="hidden min-w-0 border-r border-border bg-card lg:block">
				<div className="sticky top-0 max-h-screen overflow-y-auto p-5">
					<IdentityPanelContent actions={actions} search={search} />
				</div>
			</aside>
		</>
	);
}

function IdentityPanelContent({ actions, search }: IdentityPanelProps) {
	const {
		authMode,
		setAuthMode,
		variables,
		setVariable,
		clearVault,
		services,
		pushToast,
		apiBase,
	} = useWorkbench();
	const ready =
		authMode === "cookie"
			? Boolean(variables.csrfToken)
			: authMode === "apiKey"
				? Boolean(variables.apiKey)
				: Boolean(variables.accessToken);

	const copyValue = async (value: string) => {
		if (!value) return;
		const exit = await Effect.runPromiseExit(copyText(value));
		if (Exit.isSuccess(exit)) {
			pushToast(m.copied(), m.copied_description());
		} else {
			pushToast(
				m.clipboard_denied(),
				m.clipboard_denied_description(),
				"error",
			);
		}
	};

	return (
		<div className="space-y-7">
			<section aria-labelledby="vault-heading">
				<div className="flex items-start gap-3">
					<div
						className={`mt-1 size-2.5 shrink-0 rounded-full ${ready ? "bg-primary shadow-[0_0_0_5px_color-mix(in_oklch,var(--primary)_20%,transparent)]" : "bg-muted-foreground/40"}`}
					/>
					<div>
						<h2
							id="vault-heading"
							className="font-heading text-base font-bold text-foreground"
						>
							{ready
								? authMode === "cookie"
									? m.vault_ready_cookie()
									: authMode === "apiKey"
										? m.vault_ready_api_key()
										: m.vault_ready_bearer()
								: m.vault_idle()}
						</h2>
						<p className="mt-1 text-sm leading-5 text-muted-foreground">
							{ready
								? authMode === "cookie"
									? m.vault_ready_cookie_note()
									: m.vault_ready_tab_note()
								: authMode === "cookie"
									? m.vault_idle_cookie_note()
									: m.vault_idle_note()}
						</p>
					</div>
				</div>

				<div className="mt-5">
					<Label htmlFor="auth-mode">{m.auth_transport()}</Label>
					<Select
						value={authMode}
						onValueChange={(value) => setAuthMode(value as AuthMode)}
					>
						<SelectTrigger id="auth-mode" className="mt-1 w-full">
							<SelectValue />
						</SelectTrigger>
						<SelectContent>
							<SelectItem value="bearer">{m.bearer()}</SelectItem>
							<SelectItem value="cookie">{m.cookie()}</SelectItem>
							<SelectItem value="apiKey">{m.api_key()}</SelectItem>
						</SelectContent>
					</Select>
				</div>

				<div className="mt-5 space-y-4">
					<VaultField
						id="access-token"
						label={m.access_token()}
						value={variables.accessToken}
						onChange={(value) => setVariable("accessToken", value)}
						onCopy={copyValue}
					/>
					<VaultField
						id="refresh-token"
						label={m.refresh_token()}
						value={variables.refreshToken}
						onChange={(value) => setVariable("refreshToken", value)}
						onCopy={copyValue}
					/>
					<VaultField
						id="api-key"
						label={m.api_key()}
						value={variables.apiKey}
						onChange={(value) => setVariable("apiKey", value)}
						onCopy={copyValue}
					/>
					<VaultField
						id="csrf-token"
						label={m.csrf_token()}
						value={variables.csrfToken}
						onChange={(value) => setVariable("csrfToken", value)}
						onCopy={copyValue}
					/>
				</div>

				<Button
					className="mt-4 w-full"
					type="button"
					variant="ghost"
					onClick={clearVault}
				>
					<TrashIcon data-icon="inline-start" />
					{m.clear_vault()}
				</Button>
			</section>

			<section
				aria-labelledby="scenarios-heading"
				className="border-t border-border pt-6"
			>
				<h2 id="scenarios-heading" className="section-kicker">
					{m.demo_scenarios()}
				</h2>
				<div className="mt-3 grid gap-2">
					<ScenarioButton
						icon={<ShieldCheckIcon />}
						onClick={() => actions.demoLogin("admin")}
					>
						{m.login_admin()}
					</ScenarioButton>
					<ScenarioButton
						icon={<SignInIcon />}
						onClick={() => actions.demoLogin("user")}
					>
						{m.login_user()}
					</ScenarioButton>
					<ScenarioButton
						icon={<UsersThreeIcon />}
						onClick={actions.prepareSessions}
					>
						{m.prepare_sessions()}
					</ScenarioButton>
					<ScenarioButton icon={<KeyIcon />} onClick={actions.useDemoApiKey}>
						{m.use_demo_api_key()}
					</ScenarioButton>
					<div className="grid grid-cols-2 gap-2">
						<ScenarioButton
							icon={<GoogleLogoIcon />}
							onClick={() => actions.runSocial("google")}
						>
							Google
						</ScenarioButton>
						<ScenarioButton
							icon={<GithubLogoIcon />}
							onClick={() => actions.runSocial("github")}
						>
							GitHub
						</ScenarioButton>
					</div>
				</div>
			</section>

			<section
				aria-labelledby="fixtures-heading"
				className="border-t border-border pt-6"
			>
				<h2 id="fixtures-heading" className="section-kicker">
					{m.fixture_ids()}
				</h2>
				<dl className="mt-3 space-y-3">
					<Fixture
						label={m.admin_user_id()}
						value={DEMO.admin.id}
						onCopy={copyValue}
					/>
					<Fixture
						label={m.regular_user_id()}
						value={DEMO.user.id}
						onCopy={copyValue}
					/>
					<Fixture
						label={m.admin_role_id()}
						value={DEMO.adminRoleId}
						onCopy={copyValue}
					/>
					<Fixture
						label={m.session_id()}
						value={DEMO.sessionId}
						onCopy={copyValue}
					/>
					<Fixture
						label={m.api_key_id()}
						value={DEMO.apiKeyId}
						onCopy={copyValue}
					/>
				</dl>
			</section>

			<section
				aria-labelledby="services-heading"
				className="border-t border-border pt-6"
			>
				<h2 id="services-heading" className="section-kicker">
					{m.services()}
				</h2>
				<div className="mt-3 grid gap-2">
					<Button
						render={
							<Link
								to="/endpoints/$endpointId"
								params={{ endpointId: "health-live" }}
								search={search}
							/>
						}
						className="h-auto justify-start normal-case tracking-normal"
						variant="outline"
					>
						<ServiceIcon state={services.live.state} />
						<span className="text-left">
							{m.liveness()} · {serviceLabel(services.live)}
						</span>
					</Button>
					<Button
						render={
							<Link
								to="/endpoints/$endpointId"
								params={{ endpointId: "health-ready" }}
								search={search}
							/>
						}
						className="h-auto justify-start normal-case tracking-normal"
						variant="outline"
					>
						<ServiceIcon state={services.ready.state} />
						<span className="text-left">
							{m.readiness()} · {serviceLabel(services.ready)}
						</span>
					</Button>
					<a
						href={`${apiBase}/openapi/v1.json`}
						target="_blank"
						rel="noreferrer"
						className={cn(
							buttonVariants({ variant: "outline" }),
							"h-auto justify-start normal-case tracking-normal",
						)}
					>
						<DatabaseIcon />
						<span className="text-left">
							{m.openapi_coverage()} · {serviceLabel(services.openapi)}
						</span>
					</a>
				</div>
			</section>
		</div>
	);
}

function VaultField({
	id,
	label,
	value,
	onChange,
	onCopy,
}: {
	id: string;
	label: string;
	value: string;
	onChange: (value: string) => void;
	onCopy: (value: string) => void;
}) {
	return (
		<div>
			<Label htmlFor={id}>{label}</Label>
			<div className="mt-1 flex items-end gap-1">
				<Input
					id={id}
					type="password"
					value={value}
					onChange={(event) => onChange(event.target.value.trim())}
					autoComplete="off"
					spellCheck={false}
				/>
				<Button
					type="button"
					variant="ghost"
					size="icon-sm"
					disabled={!value}
					onClick={() => onCopy(value)}
					aria-label={`${m.copy()} ${label}`}
				>
					<CopyIcon />
				</Button>
			</div>
		</div>
	);
}

function ScenarioButton({
	icon,
	children,
	onClick,
}: {
	icon: React.ReactNode;
	children: React.ReactNode;
	onClick: () => void;
}) {
	return (
		<Button
			type="button"
			variant="outline"
			className="h-auto min-h-10 justify-start normal-case tracking-normal"
			onClick={onClick}
		>
			{icon}
			{children}
		</Button>
	);
}

function Fixture({
	label,
	value,
	onCopy,
}: {
	label: string;
	value: string;
	onCopy: (value: string) => void;
}) {
	return (
		<div>
			<dt className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
				{label}
			</dt>
			<dd className="mt-1 flex items-center gap-2">
				<code className="min-w-0 flex-1 truncate text-[0.75rem]">{value}</code>
				<Button
					type="button"
					variant="ghost"
					size="icon-xs"
					onClick={() => onCopy(value)}
					aria-label={`${m.copy()} ${label}`}
				>
					<CopyIcon />
				</Button>
			</dd>
		</div>
	);
}

function ServiceIcon({
	state,
}: {
	state: "checking" | "healthy" | "unhealthy";
}) {
	if (state === "checking") return <CircleNotchIcon className="animate-spin" />;
	if (state === "healthy")
		return <CheckCircleIcon className="text-primary" weight="fill" />;
	return <WarningCircleIcon className="text-destructive" weight="fill" />;
}

function serviceLabel(status: {
	state: string;
	operationCount?: number;
}): string {
	if (status.state === "checking") return m.checking();
	if (status.state === "unhealthy") return m.unreachable();
	return status.operationCount === undefined
		? m.healthy()
		: m.operations_ready({ count: status.operationCount });
}

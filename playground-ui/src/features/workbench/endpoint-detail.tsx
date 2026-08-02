import { ArrowLeftIcon } from "@phosphor-icons/react";
import { Effect, Exit } from "effect";
import { useEffect, useMemo, useRef, useState } from "react";
import { Button } from "#/components/ui/button";
import { m } from "#/paraglide/messages";
import { getLocale } from "#/paraglide/runtime";
import { endpointCopy, normalizeLocale } from "./catalog";
import {
	buildHeaders,
	buildPath,
	copyText,
	createCurl,
	createDraft,
	parseRequestBody,
} from "./domain";
import { EndpointGuidedAction } from "./endpoint-guided-action";
import { EndpointRequestPanel } from "./endpoint-request-panel";
import { EndpointResponsePanel } from "./endpoint-response-panel";
import type {
	EndpointDefinition,
	PanelTab,
	RequestDraft,
	WorkbenchResponse,
} from "./types";
import { useWorkbench } from "./workbench-context";

interface EndpointDetailProps {
	endpoint: EndpointDefinition;
	onBack: () => void;
	onNavigateEndpoint: (endpointId: string) => void;
	onRunSocial: (provider: "google" | "github") => void;
}

export function EndpointDetail({
	endpoint,
	onBack,
	onNavigateEndpoint,
	onRunSocial,
}: EndpointDetailProps) {
	const {
		apiBase,
		authMode,
		variables,
		response,
		isRunning,
		runRequest,
		pushToast,
	} = useWorkbench();
	const locale = normalizeLocale(getLocale());
	const itemCopy = endpointCopy(endpoint, locale);
	const result = response?.endpointId === endpoint.id ? response : null;
	const [draft, setDraft] = useState<RequestDraft>(() =>
		createDraft(endpoint, variables),
	);
	const [pathOverride, setPathOverride] = useState<string | null>(null);
	const [tabChoice, setTabChoice] = useState<{
		tab: PanelTab;
		acknowledgedResponse: WorkbenchResponse | null;
	}>({ tab: "request", acknowledgedResponse: null });
	const [confirming, setConfirming] = useState(false);
	const responseHeadingRef = useRef<HTMLHeadingElement>(null);
	const bodyRef = useRef<HTMLTextAreaElement>(null);
	const activeTab =
		result && tabChoice.acknowledgedResponse !== result
			? "response"
			: tabChoice.tab;
	const requestPath = pathOverride ?? buildPath(endpoint, draft, variables);
	const requestHeaders = useMemo(
		() =>
			buildHeaders(
				endpoint,
				authMode,
				variables,
				typeof document === "undefined" ? "" : document.cookie,
			),
		[authMode, endpoint, variables],
	);

	useEffect(() => {
		if (!result) return;
		window.requestAnimationFrame(() => responseHeadingRef.current?.focus());
	}, [result]);

	const chooseTab = (tab: PanelTab) => {
		setTabChoice({ tab, acknowledgedResponse: result });
	};

	useEffect(() => {
		const handleKeyDown = (event: KeyboardEvent) => {
			if ((event.metaKey || event.ctrlKey) && event.key === "Enter") {
				event.preventDefault();
				void attemptSend();
			}
			if (event.key === "Escape") onBack();
		};
		document.addEventListener("keydown", handleKeyDown);
		return () => document.removeEventListener("keydown", handleKeyDown);
	});

	const setPathValue = (name: string, value: string) => {
		setPathOverride(null);
		setDraft((current) => ({
			...current,
			pathValues: { ...current.pathValues, [name]: value },
		}));
	};

	const setQueryValue = (name: string, value: string) => {
		setPathOverride(null);
		setDraft((current) => ({
			...current,
			queryValues: { ...current.queryValues, [name]: value },
		}));
	};

	const readBody = async (): Promise<unknown | typeof INVALID_BODY> => {
		const exit = await Effect.runPromiseExit(
			parseRequestBody(endpoint, draft.bodyText, variables),
		);
		if (Exit.isSuccess(exit)) return exit.value;
		pushToast(m.invalid_json(), m.invalid_json_description(), "error");
		bodyRef.current?.focus();
		return INVALID_BODY;
	};

	const execute = async () => {
		setConfirming(false);
		const body = await readBody();
		if (body === INVALID_BODY) return;
		await runRequest({ endpoint, path: requestPath, body });
	};

	const attemptSend = async () => {
		if (endpoint.destructive && !confirming) {
			setConfirming(true);
			return;
		}
		await execute();
	};

	const formatJson = () => {
		try {
			setDraft((current) => ({
				...current,
				bodyText: JSON.stringify(JSON.parse(current.bodyText), null, 2),
			}));
		} catch {
			pushToast(m.invalid_json(), m.invalid_json_description(), "error");
			bodyRef.current?.focus();
		}
	};

	const runCopy = async (value: string) => {
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

	const copyCurl = async () => {
		const body = await readBody();
		if (body === INVALID_BODY) return;
		await runCopy(
			createCurl({
				apiBase,
				endpoint,
				path: requestPath,
				body,
				headers: requestHeaders,
			}),
		);
	};

	return (
		<aside
			className="min-w-0 border-l border-border bg-card"
			aria-labelledby="endpoint-title"
		>
			<div className="sticky top-0 max-h-screen overflow-y-auto">
				<div className="border-b border-border px-4 py-5 sm:px-6">
					<Button
						type="button"
						variant="ghost"
						size="sm"
						onClick={onBack}
						className="mb-4 xl:hidden"
					>
						<ArrowLeftIcon data-icon="inline-start" />
						{m.back_to_catalog()}
					</Button>
					<div className="flex flex-wrap items-center gap-2">
						<span className={`method method-${endpoint.method.toLowerCase()}`}>
							{endpoint.method}
						</span>
						<span className={`auth-badge auth-${endpoint.auth}`}>
							{endpoint.auth}
						</span>
					</div>
					<h2
						id="endpoint-title"
						className="mt-4 font-heading text-xl font-bold text-foreground"
					>
						{itemCopy.title}
					</h2>
					<p className="mt-2 text-sm leading-6 text-muted-foreground">
						{itemCopy.description}
					</p>
				</div>

				<div
					className="grid grid-cols-2 border-b border-border"
					role="tablist"
					aria-label={itemCopy.title}
				>
					<TabButton
						active={activeTab === "request"}
						onClick={() => chooseTab("request")}
					>
						{m.request()}
					</TabButton>
					<TabButton
						active={activeTab === "response"}
						onClick={() => chooseTab("response")}
					>
						{m.response()}
						{result ? (
							<span className={result.ok ? "text-primary" : "text-destructive"}>
								· {result.status}
							</span>
						) : null}
					</TabButton>
				</div>

				{activeTab === "request" ? (
					<EndpointRequestPanel
						endpoint={endpoint}
						itemTitle={itemCopy.title}
						draft={draft}
						requestPath={requestPath}
						requestHeaders={requestHeaders}
						confirming={confirming}
						isRunning={isRunning}
						bodyRef={bodyRef}
						specialAction={
							<EndpointGuidedAction
								endpoint={endpoint}
								draft={draft}
								variables={variables}
								onBodyChange={(bodyText) =>
									setDraft((current) => ({ ...current, bodyText }))
								}
								onNavigateEndpoint={onNavigateEndpoint}
								onRunSocial={onRunSocial}
							/>
						}
						onPathChange={setPathValue}
						onQueryChange={setQueryValue}
						onBodyChange={(bodyText) =>
							setDraft((current) => ({ ...current, bodyText }))
						}
						onPathOverride={setPathOverride}
						onFormatJson={formatJson}
						onExecute={() => void execute()}
						onCancelConfirmation={() => setConfirming(false)}
						onSend={() => void attemptSend()}
						onCopyCurl={() => void copyCurl()}
					/>
				) : (
					<EndpointResponsePanel
						result={result}
						headingRef={responseHeadingRef}
						onCopy={(value) => void runCopy(value)}
					/>
				)}
			</div>
		</aside>
	);
}

const INVALID_BODY = Symbol("invalid-body");

function TabButton({
	active,
	children,
	onClick,
}: {
	active: boolean;
	children: React.ReactNode;
	onClick: () => void;
}) {
	return (
		<Button
			type="button"
			role="tab"
			aria-selected={active}
			tabIndex={active ? 0 : -1}
			variant="ghost"
			className={`h-12 rounded-none border-b-2 ${active ? "border-primary text-foreground" : "border-transparent text-muted-foreground"}`}
			onClick={onClick}
		>
			{children}
		</Button>
	);
}

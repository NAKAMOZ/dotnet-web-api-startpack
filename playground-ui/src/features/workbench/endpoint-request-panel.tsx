import {
	BracketsCurlyIcon,
	CheckIcon,
	CopyIcon,
	PaperPlaneTiltIcon,
	ShieldWarningIcon,
} from "@phosphor-icons/react";
import type { ReactNode, RefObject } from "react";
import { Button } from "#/components/ui/button";
import { Input } from "#/components/ui/input";
import { Label } from "#/components/ui/label";
import { Textarea } from "#/components/ui/textarea";
import { m } from "#/paraglide/messages";
import { redactHeader } from "./domain";
import type { EndpointDefinition, RequestDraft } from "./types";

interface EndpointRequestPanelProps {
	endpoint: EndpointDefinition;
	itemTitle: string;
	draft: RequestDraft;
	requestPath: string;
	requestHeaders: Record<string, string>;
	confirming: boolean;
	isRunning: boolean;
	bodyRef: RefObject<HTMLTextAreaElement | null>;
	specialAction: ReactNode;
	onPathChange: (name: string, value: string) => void;
	onQueryChange: (name: string, value: string) => void;
	onBodyChange: (value: string) => void;
	onPathOverride: (value: string) => void;
	onFormatJson: () => void;
	onExecute: () => void;
	onCancelConfirmation: () => void;
	onSend: () => void;
	onCopyCurl: () => void;
}

export function EndpointRequestPanel({
	endpoint,
	itemTitle,
	draft,
	requestPath,
	requestHeaders,
	confirming,
	isRunning,
	bodyRef,
	specialAction,
	onPathChange,
	onQueryChange,
	onBodyChange,
	onPathOverride,
	onFormatJson,
	onExecute,
	onCancelConfirmation,
	onSend,
	onCopyCurl,
}: EndpointRequestPanelProps) {
	return (
		<div className="space-y-6 p-4 sm:p-6" role="tabpanel">
			{endpoint.pathParams.length > 0 ? (
				<ParameterFields
					legend={m.path_parameters()}
					parameters={endpoint.pathParams}
					values={draft.pathValues}
					onChange={onPathChange}
				/>
			) : null}
			{endpoint.query.length > 0 ? (
				<ParameterFields
					legend={m.query_parameters()}
					parameters={endpoint.query}
					values={draft.queryValues}
					onChange={onQueryChange}
				/>
			) : null}

			{endpoint.body !== null ? (
				<div>
					<div className="mb-2 flex items-center justify-between gap-3">
						<Label htmlFor="request-body">{m.request_body()}</Label>
						<Button
							type="button"
							size="xs"
							variant="ghost"
							onClick={onFormatJson}
						>
							<BracketsCurlyIcon data-icon="inline-start" />
							{m.format_json()}
						</Button>
					</div>
					<Textarea
						ref={bodyRef}
						id="request-body"
						value={draft.bodyText}
						onChange={(event) => onBodyChange(event.target.value)}
						className="min-h-52 font-mono text-xs leading-5"
						spellCheck={false}
					/>
				</div>
			) : null}

			{specialAction ? (
				<section aria-labelledby="special-action-heading">
					<h3 id="special-action-heading" className="section-kicker mb-2">
						{m.special_actions()}
					</h3>
					{specialAction}
				</section>
			) : null}

			<section
				aria-labelledby="request-preview-heading"
				className="border border-border bg-muted/40 p-4"
			>
				<h3 id="request-preview-heading" className="section-kicker">
					{m.request_preview()}
				</h3>
				<div className="mt-3 flex items-center gap-2">
					<span className={`method method-${endpoint.method.toLowerCase()}`}>
						{endpoint.method}
					</span>
					<Input
						value={requestPath}
						onChange={(event) => onPathOverride(event.target.value)}
						aria-label={m.request_preview()}
						className="font-mono text-xs"
						spellCheck={false}
					/>
				</div>
				<h4 className="mt-4 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
					{m.request_headers()}
				</h4>
				<pre className="mt-2 overflow-x-auto whitespace-pre-wrap font-mono text-xs leading-5 text-foreground">
					{Object.entries(requestHeaders).length
						? Object.entries(requestHeaders)
								.map(([name, value]) => `${name}: ${redactHeader(name, value)}`)
								.join("\n")
						: m.no_custom_headers()}
				</pre>
			</section>

			{endpoint.destructive ? (
				<div className="border border-destructive/40 bg-destructive/5 p-4">
					<div className="flex items-start gap-3">
						<ShieldWarningIcon
							aria-hidden="true"
							className="mt-0.5 size-5 shrink-0 text-destructive"
						/>
						<div>
							<p className="font-semibold text-sm text-foreground">
								{m.destructive_title()}
							</p>
							<p className="mt-1 text-sm leading-5 text-muted-foreground">
								{m.destructive_description()}
							</p>
						</div>
					</div>
					{confirming ? (
						<div className="mt-4 flex flex-wrap gap-2">
							<Button
								type="button"
								variant="destructive"
								onClick={onExecute}
								disabled={isRunning}
							>
								<CheckIcon data-icon="inline-start" />
								{m.confirm_operation({ operation: itemTitle })}
							</Button>
							<Button
								type="button"
								variant="outline"
								onClick={onCancelConfirmation}
							>
								{m.cancel()}
							</Button>
						</div>
					) : null}
				</div>
			) : null}

			<div className="flex flex-col gap-2 sm:flex-row">
				<Button
					type="button"
					className="flex-1"
					onClick={onSend}
					disabled={isRunning || confirming}
				>
					<PaperPlaneTiltIcon data-icon="inline-start" />
					{isRunning ? m.sending_request() : m.send_request()}
				</Button>
				<Button type="button" variant="outline" onClick={onCopyCurl}>
					<CopyIcon data-icon="inline-start" />
					{m.copy_curl()}
				</Button>
			</div>
		</div>
	);
}

function ParameterFields({
	legend,
	parameters,
	values,
	onChange,
}: {
	legend: string;
	parameters: EndpointDefinition["pathParams"];
	values: Record<string, string>;
	onChange: (name: string, value: string) => void;
}) {
	return (
		<fieldset>
			<legend className="section-kicker mb-3">{legend}</legend>
			<div className="grid gap-4 sm:grid-cols-2">
				{parameters.map((parameter) => (
					<div key={parameter.name}>
						<Label htmlFor={`${legend}-${parameter.name}`}>
							{parameter.name}
						</Label>
						<Input
							id={`${legend}-${parameter.name}`}
							value={values[parameter.name] ?? ""}
							onChange={(event) => onChange(parameter.name, event.target.value)}
							className="mt-1 font-mono text-xs"
							autoComplete="off"
							spellCheck={false}
						/>
					</div>
				))}
			</div>
		</fieldset>
	);
}

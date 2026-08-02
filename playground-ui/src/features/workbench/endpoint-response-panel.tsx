import { CopyIcon, PaperPlaneTiltIcon } from "@phosphor-icons/react";
import type { RefObject } from "react";
import { Button } from "#/components/ui/button";
import { m } from "#/paraglide/messages";
import { formatResponse } from "./domain";
import type { WorkbenchResponse } from "./types";

interface EndpointResponsePanelProps {
	result: WorkbenchResponse | null;
	headingRef: RefObject<HTMLHeadingElement | null>;
	onCopy: (value: string) => void;
}

export function EndpointResponsePanel({
	result,
	headingRef,
	onCopy,
}: EndpointResponsePanelProps) {
	return (
		<div className="p-4 sm:p-6" role="tabpanel" aria-live="polite">
			{result ? (
				<div>
					<div className="flex flex-wrap items-start justify-between gap-3">
						<div>
							<p
								className={`font-mono text-3xl font-bold ${result.ok ? "text-primary" : "text-destructive"}`}
							>
								{result.status}
							</p>
							<h3
								ref={headingRef}
								tabIndex={-1}
								className="mt-1 font-heading text-lg font-bold outline-none focus-visible:ring-2 focus-visible:ring-ring"
							>
								{result.statusText || (result.ok ? "OK" : "Error")}
							</h3>
							<p className="mt-1 text-sm text-muted-foreground">
								{result.elapsedMs} ms
							</p>
						</div>
						<Button
							type="button"
							variant="outline"
							size="sm"
							onClick={() => onCopy(result.raw)}
							disabled={!result.raw}
						>
							<CopyIcon data-icon="inline-start" />
							{m.copy_response()}
						</Button>
					</div>
					{result.notice ? (
						<p className="mt-5 border border-primary/30 bg-primary/10 p-4 text-sm leading-6 text-foreground">
							{result.notice}
						</p>
					) : null}
					<pre className="response-viewer mt-5 max-h-[32rem] overflow-auto border border-border bg-foreground p-4 font-mono text-xs leading-5 text-background">
						{formatResponse(result, m.empty_response())}
					</pre>
					<ResponseHeaders headers={result.headers} />
				</div>
			) : (
				<div className="border border-dashed border-border px-6 py-12 text-center">
					<PaperPlaneTiltIcon
						aria-hidden="true"
						className="mx-auto size-7 text-muted-foreground"
					/>
					<h3
						ref={headingRef}
						tabIndex={-1}
						className="mt-4 font-heading text-lg font-bold outline-none"
					>
						{m.no_response()}
					</h3>
					<p className="mx-auto mt-2 max-w-sm text-sm text-muted-foreground">
						{m.no_response_description()}
					</p>
				</div>
			)}
		</div>
	);
}

function ResponseHeaders({ headers }: { headers: Record<string, string> }) {
	const visible = [
		"content-type",
		"x-correlation-id",
		"retry-after",
		"api-supported-versions",
	].flatMap((name) => (headers[name] ? [[name, headers[name]] as const] : []));
	if (visible.length === 0) return null;

	return (
		<dl className="mt-5 divide-y divide-border border-y border-border text-xs">
			{visible.map(([name, value]) => (
				<div
					key={name}
					className="grid grid-cols-[9rem_minmax(0,1fr)] gap-3 py-3"
				>
					<dt className="font-mono font-semibold text-muted-foreground">
						{name}
					</dt>
					<dd className="break-all font-mono text-foreground">{value}</dd>
				</div>
			))}
		</dl>
	);
}

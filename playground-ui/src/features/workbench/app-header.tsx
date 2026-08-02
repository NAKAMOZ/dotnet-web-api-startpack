import {
	ArrowSquareOutIcon,
	BracketsCurlyIcon,
	EnvelopeOpenIcon,
} from "@phosphor-icons/react";
import ParaglideLocaleSwitcher from "#/components/LocaleSwitcher";
import { buttonVariants } from "#/components/ui/button";
import { Input } from "#/components/ui/input";
import { Label } from "#/components/ui/label";
import { cn } from "#/lib/utils";
import { m } from "#/paraglide/messages";
import { useWorkbench } from "./workbench-context";

export function AppHeader() {
	const { apiBase, setApiBase } = useWorkbench();
	const mailpitUrl = getMailpitUrl();

	return (
		<header className="border-b border-border/80 bg-background/90 backdrop-blur-xl">
			<div className="mx-auto flex max-w-[1920px] flex-col gap-4 px-4 py-4 sm:px-6 xl:flex-row xl:items-center xl:justify-between">
				<div className="flex min-w-0 items-center gap-3">
					<div className="grid size-11 shrink-0 place-items-center border border-primary/40 bg-primary text-primary-foreground shadow-[4px_4px_0_var(--foreground)]">
						<BracketsCurlyIcon
							aria-hidden="true"
							className="size-5"
							weight="bold"
						/>
					</div>
					<div className="min-w-0">
						<p className="font-heading text-lg font-bold tracking-tight text-foreground">
							{m.app_title()}
						</p>
						<p className="max-w-xl text-sm leading-5 text-muted-foreground">
							{m.app_description()}
						</p>
					</div>
				</div>

				<div className="flex flex-col gap-3 sm:flex-row sm:items-end">
					<div className="min-w-0 flex-1 sm:w-72 sm:flex-none">
						<Label
							htmlFor="api-origin"
							className="mb-1.5 block text-xs uppercase tracking-widest"
						>
							{m.api_origin()}
						</Label>
						<Input
							id="api-origin"
							value={apiBase}
							onChange={(event) => setApiBase(event.target.value)}
							onBlur={(event) => setApiBase(event.target.value)}
							placeholder="https://localhost:5001"
							spellCheck={false}
						/>
					</div>
					<div className="flex flex-wrap items-center gap-2">
						<a
							href={mailpitUrl}
							target="_blank"
							rel="noreferrer"
							className={cn(buttonVariants({ size: "sm", variant: "outline" }))}
						>
							<EnvelopeOpenIcon data-icon="inline-start" />
							{m.open_mailpit()}
						</a>
						<a
							href={`${apiBase}/openapi/v1.json`}
							target="_blank"
							rel="noreferrer"
							className={cn(buttonVariants({ size: "sm", variant: "outline" }))}
						>
							<ArrowSquareOutIcon data-icon="inline-start" />
							{m.open_openapi()}
						</a>
						<ParaglideLocaleSwitcher />
					</div>
				</div>
			</div>
		</header>
	);
}

const getMailpitUrl = (): string => {
	if (typeof window === "undefined") return "http://localhost:8025/";
	const url = new URL(window.location.href);
	url.protocol = "http:";
	url.port = "8025";
	url.pathname = "/";
	url.search = "";
	url.hash = "";
	return url.toString();
};

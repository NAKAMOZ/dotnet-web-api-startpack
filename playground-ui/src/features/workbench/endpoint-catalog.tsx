import {
	ArrowRightIcon,
	ListBulletsIcon,
	MagnifyingGlassIcon,
	RowsIcon,
	XIcon,
} from "@phosphor-icons/react";
import { Link } from "@tanstack/react-router";
import type { RefObject } from "react";
import { Button } from "#/components/ui/button";
import { Input } from "#/components/ui/input";
import { m } from "#/paraglide/messages";
import { getLocale } from "#/paraglide/runtime";
import { ENDPOINTS, endpointCopy, normalizeLocale } from "./catalog";
import { GROUPS } from "./constants";
import type {
	AuthRequirement,
	EndpointGroupFilter,
	WorkbenchSearch,
} from "./types";

interface EndpointCatalogProps {
	selectedId: string | null;
	search: WorkbenchSearch;
	onSearchChange: (next: Partial<WorkbenchSearch>) => void;
	searchRef: RefObject<HTMLInputElement | null>;
}

export function EndpointCatalog({
	selectedId,
	search,
	onSearchChange,
	searchRef,
}: EndpointCatalogProps) {
	const locale = normalizeLocale(getLocale());
	const query = search.q.trim().toLocaleLowerCase(locale);
	const visible = ENDPOINTS.filter((item) => {
		if (search.group !== "all" && item.group !== search.group) return false;
		if (!query) return true;
		const itemCopy = endpointCopy(item, locale);
		return `${item.method} ${item.path} ${itemCopy.title} ${itemCopy.description}`
			.toLocaleLowerCase(locale)
			.includes(query);
	});

	return (
		<main
			id="endpoint-catalog"
			className="min-w-0 bg-background px-4 py-6 sm:px-6 xl:px-8"
			tabIndex={-1}
		>
			<div className="mx-auto max-w-5xl">
				<div className="flex flex-col gap-4 border-b border-border pb-5 sm:flex-row sm:items-end sm:justify-between">
					<div>
						<p className="section-kicker">{m.catalog()}</p>
						<h1 className="mt-1 font-heading text-2xl font-bold tracking-tight text-foreground sm:text-3xl">
							{groupLabel(search.group)}
						</h1>
						<p className="mt-2 text-sm text-muted-foreground">
							{m.visible_endpoint_count({ count: visible.length })}
						</p>
					</div>
					<fieldset className="flex items-center gap-1">
						<legend className="sr-only">{m.catalog()}</legend>
						<Button
							type="button"
							variant={search.view === "comfortable" ? "secondary" : "ghost"}
							size="icon-sm"
							onClick={() => onSearchChange({ view: "comfortable" })}
							aria-label={m.comfortable_view()}
							aria-pressed={search.view === "comfortable"}
						>
							<RowsIcon />
						</Button>
						<Button
							type="button"
							variant={search.view === "compact" ? "secondary" : "ghost"}
							size="icon-sm"
							onClick={() => onSearchChange({ view: "compact" })}
							aria-label={m.compact_view()}
							aria-pressed={search.view === "compact"}
						>
							<ListBulletsIcon />
						</Button>
					</fieldset>
				</div>

				<div className="sticky top-0 z-20 -mx-4 border-b border-border bg-background/95 px-4 py-4 backdrop-blur-lg sm:-mx-6 sm:px-6 xl:-mx-8 xl:px-8">
					<div className="relative">
						<MagnifyingGlassIcon
							aria-hidden="true"
							className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground"
						/>
						<Input
							ref={searchRef}
							type="search"
							value={search.q}
							onChange={(event) => onSearchChange({ q: event.target.value })}
							placeholder={m.search_endpoints()}
							aria-label={m.search_label()}
							className="pl-10"
						/>
					</div>
					<fieldset className="mt-3 flex gap-2 overflow-x-auto pb-1">
						<legend className="sr-only">{m.catalog()}</legend>
						{GROUPS.map((group) => {
							const count =
								group === "all"
									? ENDPOINTS.length
									: ENDPOINTS.filter((item) => item.group === group).length;
							return (
								<Button
									key={group}
									type="button"
									size="xs"
									variant={search.group === group ? "default" : "outline"}
									onClick={() => onSearchChange({ group })}
									aria-pressed={search.group === group}
								>
									{groupLabel(group)}
									<span className="font-mono text-[0.7rem] opacity-70">
										{count}
									</span>
								</Button>
							);
						})}
					</fieldset>
				</div>

				{visible.length > 0 ? (
					<div className="mt-5 grid gap-2" data-view={search.view}>
						{visible.map((item) => {
							const itemCopy = endpointCopy(item, locale);
							return (
								<Button
									key={item.id}
									render={
										<Link
											to="/endpoints/$endpointId"
											params={{ endpointId: item.id }}
											search={search}
											aria-current={selectedId === item.id ? "page" : undefined}
										/>
									}
									variant="outline"
									className={`endpoint-card h-auto w-full justify-start whitespace-normal px-0 py-0 text-left normal-case tracking-normal ${selectedId === item.id ? "border-foreground bg-muted" : ""}`}
								>
									<span
										className={`method method-${item.method.toLowerCase()}`}
									>
										{item.method}
									</span>
									<span className="min-w-0 flex-1 py-4 pr-3">
										<code className="block truncate border-0 bg-transparent p-0 font-mono text-xs text-muted-foreground">
											{item.path}
										</code>
										<strong className="mt-2 block font-heading text-sm font-bold text-foreground">
											{itemCopy.title}
										</strong>
										{search.view === "comfortable" ? (
											<span className="mt-1 block text-sm leading-5 text-muted-foreground">
												{itemCopy.description}
											</span>
										) : null}
									</span>
									<span className={`auth-badge auth-${item.auth}`}>
										{authLabel(item.auth)}
									</span>
									<ArrowRightIcon
										aria-hidden="true"
										className="mr-4 size-4 shrink-0 text-muted-foreground"
									/>
								</Button>
							);
						})}
					</div>
				) : (
					<div className="mt-10 border border-dashed border-border px-6 py-12 text-center">
						<MagnifyingGlassIcon
							aria-hidden="true"
							className="mx-auto size-7 text-muted-foreground"
						/>
						<h2 className="mt-4 font-heading text-lg font-bold text-foreground">
							{m.no_endpoints_title()}
						</h2>
						<p className="mx-auto mt-2 max-w-sm text-sm text-muted-foreground">
							{m.no_endpoints_description()}
						</p>
						<Button
							type="button"
							variant="outline"
							className="mt-5"
							onClick={() => onSearchChange({ q: "", group: "all" })}
						>
							<XIcon data-icon="inline-start" />
							{m.clear_filters()}
						</Button>
					</div>
				)}
			</div>
		</main>
	);
}

const groupLabel = (group: EndpointGroupFilter): string => {
	switch (group) {
		case "operations":
			return m.group_operations();
		case "authentication":
			return m.group_authentication();
		case "account":
			return m.group_account();
		case "sessions":
			return m.group_sessions();
		case "security":
			return m.group_security();
		case "admin":
			return m.group_admin();
		default:
			return m.all_operations();
	}
};

const authLabel = (auth: AuthRequirement): string => {
	switch (auth) {
		case "auth":
			return m.auth_authenticated();
		case "recent":
			return m.auth_recent();
		case "admin":
			return m.auth_admin();
		default:
			return m.auth_public();
	}
};

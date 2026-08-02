export type HttpMethod = "GET" | "POST" | "PUT" | "PATCH" | "DELETE";

export type EndpointGroup =
	| "operations"
	| "authentication"
	| "account"
	| "sessions"
	| "security"
	| "admin";

export type EndpointGroupFilter = "all" | EndpointGroup;
export type AuthRequirement = "public" | "auth" | "recent" | "admin";
export type AuthMode = "bearer" | "cookie" | "apiKey";
export type CatalogView = "comfortable" | "compact";
export type PanelTab = "request" | "response";
export type SpecialAction =
	| "social"
	| "totp"
	| "passkey-register"
	| "passkey-auth";

export type AppLocale = "en" | "de" | "tr";

export interface LocalizedCopy {
	title: string;
	description: string;
}

export type EndpointCopy = Record<AppLocale, LocalizedCopy>;

export interface EndpointParameter {
	name: string;
	value: string;
}

export interface EndpointDefinition {
	id: string;
	method: HttpMethod;
	path: string;
	copy: EndpointCopy;
	group: EndpointGroup;
	auth: AuthRequirement;
	body: unknown | null;
	pathParams: ReadonlyArray<EndpointParameter>;
	query: ReadonlyArray<EndpointParameter>;
	destructive: boolean;
	special: SpecialAction | null;
}

export interface WorkbenchVariables {
	accessToken: string;
	refreshToken: string;
	apiKey: string;
	csrfToken: string;
	totpSecret: string;
	totpCode: string;
	mfaTicket: string;
	credentialId: string;
	socialCode: string;
	socialState: string;
	verificationToken: string;
	passwordResetToken: string;
}

export interface RequestDraft {
	pathValues: Record<string, string>;
	queryValues: Record<string, string>;
	bodyText: string;
}

export interface ApiResult {
	status: number;
	statusText: string;
	ok: boolean;
	raw: string;
	data: unknown;
	elapsedMs: number;
	headers: Record<string, string>;
}

export interface WorkbenchResponse extends ApiResult {
	endpointId: string;
	notice?: string;
}

export interface ToastMessage {
	id: number;
	title: string;
	description: string;
	tone: "neutral" | "error";
}

export interface ServiceStatus {
	state: "checking" | "healthy" | "unhealthy";
	operationCount?: number;
}

export interface WorkbenchSearch {
	q: string;
	group: EndpointGroupFilter;
	view: CatalogView;
}

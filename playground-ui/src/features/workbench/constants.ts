import type { EndpointGroupFilter, WorkbenchVariables } from "./types";

export const DEMO = {
	admin: {
		id: "0198f3a0-0000-7000-8001-000000000001",
		email: "admin@localhost.dev",
		password: "Dev_Admin_Password_1!",
	},
	user: {
		id: "0198f3a0-0000-7000-8001-000000000002",
		email: "user@localhost.dev",
		password: "Dev_User_Password_1!",
	},
	adminRoleId: "0198f3a0-0000-7000-8000-000000000001",
	userRoleId: "0198f3a0-0000-7000-8000-000000000002",
	sessionId: "0198f3a0-0000-7000-8001-000000000101",
	apiKeyId: "0198f3a0-0000-7000-8001-000000000301",
	accountId: "0198f3a0-0000-7000-8001-000000000401",
	apiKey: "ak_demoAdmin01_Dev_Demo_Api_Key_Only_Local_2026",
} as const;

export const GROUPS: ReadonlyArray<EndpointGroupFilter> = [
	"all",
	"operations",
	"authentication",
	"account",
	"sessions",
	"security",
	"admin",
];

export const SESSION_ISSUING_ENDPOINTS = new Set([
	"auth-login",
	"auth-login-mfa",
	"social-callback",
	"passkey-auth-complete",
]);

export const STORAGE_KEYS = {
	accessToken: "startpack.workbench.accessToken",
	refreshToken: "startpack.workbench.refreshToken",
	apiKey: "startpack.workbench.apiKey",
	csrfToken: "startpack.workbench.csrfToken",
	authMode: "startpack.workbench.authMode",
	totpSecret: "startpack.workbench.totpSecret",
	mfaTicket: "startpack.workbench.mfaTicket",
	credentialId: "startpack.workbench.credentialId",
	socialCode: "startpack.workbench.socialCode",
	socialState: "startpack.workbench.socialState",
} as const;

export const EMPTY_VARIABLES: WorkbenchVariables = {
	accessToken: "",
	refreshToken: "",
	apiKey: "",
	csrfToken: "",
	totpSecret: "",
	totpCode: "",
	mfaTicket: "",
	credentialId: "",
	socialCode: "",
	socialState: "",
	verificationToken: "",
	passwordResetToken: "",
};

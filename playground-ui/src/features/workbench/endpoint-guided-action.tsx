import {
	ArrowRightIcon,
	FingerprintIcon,
	LightningIcon,
	SparkleIcon,
} from "@phosphor-icons/react";
import { Effect, Exit } from "effect";
import { Button } from "#/components/ui/button";
import { m } from "#/paraglide/messages";
import {
	createPasskey,
	credentialToJson,
	getPasskey,
	isWebAuthnAvailable,
} from "./browser-effects";
import { endpointById } from "./catalog";
import type {
	EndpointDefinition,
	RequestDraft,
	WorkbenchVariables,
} from "./types";
import { useWorkbench } from "./workbench-context";

interface EndpointGuidedActionProps {
	endpoint: EndpointDefinition;
	draft: RequestDraft;
	variables: WorkbenchVariables;
	onBodyChange: (value: string) => void;
	onNavigateEndpoint: (endpointId: string) => void;
	onRunSocial: (provider: "google" | "github") => void;
}

export function EndpointGuidedAction({
	endpoint,
	draft,
	variables,
	onBodyChange,
	onNavigateEndpoint,
	onRunSocial,
}: EndpointGuidedActionProps) {
	const { runRequest, pushToast } = useWorkbench();

	const fillTotp = () => {
		if (!variables.totpCode) {
			pushToast(m.totp_missing(), m.totp_missing_description(), "error");
			return;
		}
		try {
			const body = JSON.parse(draft.bodyText || "{}") as Record<
				string,
				unknown
			>;
			body.code = variables.totpCode;
			onBodyChange(JSON.stringify(body, null, 2));
			pushToast(m.totp_filled(), variables.totpCode);
		} catch {
			pushToast(m.invalid_json(), m.invalid_json_description(), "error");
		}
	};

	const runPasskeyRegistration = async () => {
		if (!isWebAuthnAvailable()) {
			pushToast(
				m.webauthn_unsupported(),
				m.webauthn_unsupported_description(),
				"error",
			);
			return;
		}
		const complete = endpointById("passkey-registration-complete");
		const optionsResult = await runRequest({
			endpoint,
			body: { label: "Workbench passkey" },
			display: false,
		});
		const options = getRecord(getRecord(optionsResult?.data)?.options);
		if (!optionsResult?.ok || !options || !complete) {
			pushToast(m.passkey_failed(), responseReason(optionsResult), "error");
			return;
		}
		const credentialExit = await Effect.runPromiseExit(createPasskey(options));
		if (!Exit.isSuccess(credentialExit)) {
			pushToast(m.passkey_failed(), credentialExit.cause._tag, "error");
			return;
		}
		const body = {
			attestationResponse: credentialToJson(credentialExit.value),
			label: "Workbench passkey",
		};
		onNavigateEndpoint(complete.id);
		const completed = await runRequest({ endpoint: complete, body });
		if (completed?.ok) {
			pushToast(m.passkey_registered(), m.passkey_registered_description());
		}
	};

	const runPasskeyAuthentication = async () => {
		if (!isWebAuthnAvailable()) {
			pushToast(
				m.webauthn_unsupported(),
				m.webauthn_unsupported_description(),
				"error",
			);
			return;
		}
		const complete = endpointById("passkey-auth-complete");
		const optionsResult = await runRequest({
			endpoint,
			body: {},
			display: false,
		});
		const options = getRecord(getRecord(optionsResult?.data)?.options);
		if (!optionsResult?.ok || !options || !complete) {
			pushToast(m.passkey_failed(), responseReason(optionsResult), "error");
			return;
		}
		const credentialExit = await Effect.runPromiseExit(getPasskey(options));
		if (!Exit.isSuccess(credentialExit)) {
			pushToast(m.passkey_failed(), credentialExit.cause._tag, "error");
			return;
		}
		const body = { assertionResponse: credentialToJson(credentialExit.value) };
		onNavigateEndpoint(complete.id);
		const completed = await runRequest({ endpoint: complete, body });
		if (completed?.ok) {
			pushToast(m.passkey_login_ready(), m.passkey_login_ready_description());
		}
	};

	if (endpoint.special === "passkey-register") {
		return (
			<GuidedAction
				icon={<FingerprintIcon />}
				title={m.run_webauthn_registration()}
				description={m.run_webauthn_registration_note()}
				onClick={() => void runPasskeyRegistration()}
			/>
		);
	}
	if (endpoint.special === "passkey-auth") {
		return (
			<GuidedAction
				icon={<FingerprintIcon />}
				title={m.run_webauthn_authentication()}
				description={m.run_webauthn_authentication_note()}
				onClick={() => void runPasskeyAuthentication()}
			/>
		);
	}
	if (endpoint.special === "social") {
		const provider =
			draft.pathValues.provider === "github" ? "github" : "google";
		return (
			<GuidedAction
				icon={<SparkleIcon />}
				title={m.run_social_flow()}
				description={m.run_social_flow_note()}
				onClick={() => onRunSocial(provider)}
			/>
		);
	}
	if (
		endpoint.special === "totp" ||
		(endpoint.id === "auth-login-mfa" && variables.totpSecret)
	) {
		return (
			<GuidedAction
				icon={<LightningIcon />}
				title={m.fill_totp()}
				description={
					variables.totpCode
						? m.totp_live_code({ code: variables.totpCode })
						: m.totp_enroll_first()
				}
				onClick={fillTotp}
			/>
		);
	}
	return null;
}

function GuidedAction({
	icon,
	title,
	description,
	onClick,
}: {
	icon: React.ReactNode;
	title: string;
	description: string;
	onClick: () => void;
}) {
	return (
		<Button
			type="button"
			variant="outline"
			className="h-auto w-full justify-start whitespace-normal px-4 py-3 text-left normal-case tracking-normal"
			onClick={onClick}
		>
			<span className="grid size-9 shrink-0 place-items-center bg-primary/15 text-primary">
				{icon}
			</span>
			<span className="min-w-0 flex-1">
				<strong className="block text-sm text-foreground">{title}</strong>
				<span className="mt-1 block text-sm leading-5 text-muted-foreground">
					{description}
				</span>
			</span>
			<ArrowRightIcon className="shrink-0" />
		</Button>
	);
}

const getRecord = (value: unknown): Record<string, unknown> | null =>
	value !== null && typeof value === "object"
		? (value as Record<string, unknown>)
		: null;

const responseReason = (
	result: { status: number; data: unknown } | null | undefined,
): string => {
	const data = getRecord(result?.data);
	if (typeof data?.detail === "string") return data.detail;
	if (typeof data?.title === "string") return data.title;
	return result ? `HTTP ${result.status}` : m.request_failed();
};

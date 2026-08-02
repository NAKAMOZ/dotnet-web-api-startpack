import {
	CheckCircleIcon,
	WarningCircleIcon,
	XIcon,
} from "@phosphor-icons/react";
import { Button } from "#/components/ui/button";
import { m } from "#/paraglide/messages";
import { useWorkbench } from "./workbench-context";

export function ToastRegion() {
	const { toasts, dismissToast } = useWorkbench();

	return (
		<div
			aria-live="polite"
			aria-relevant="additions"
			className="pointer-events-none fixed inset-x-4 bottom-4 z-50 flex flex-col items-end gap-2 sm:left-auto sm:w-[25rem]"
		>
			{toasts.map((toast) => (
				<div
					key={toast.id}
					className={`pointer-events-auto flex w-full items-start gap-3 border bg-popover p-4 text-popover-foreground shadow-xl ${
						toast.tone === "error" ? "border-destructive/50" : "border-border"
					}`}
					role={toast.tone === "error" ? "alert" : "status"}
				>
					{toast.tone === "error" ? (
						<WarningCircleIcon
							aria-hidden="true"
							className="mt-0.5 size-5 shrink-0 text-destructive"
							weight="fill"
						/>
					) : (
						<CheckCircleIcon
							aria-hidden="true"
							className="mt-0.5 size-5 shrink-0 text-primary"
							weight="fill"
						/>
					)}
					<div className="min-w-0 flex-1">
						<p className="font-semibold text-sm">{toast.title}</p>
						<p className="mt-1 text-sm leading-5 text-muted-foreground">
							{toast.description}
						</p>
					</div>
					<Button
						type="button"
						variant="ghost"
						size="icon-xs"
						onClick={() => dismissToast(toast.id)}
						aria-label={m.toast_dismiss()}
					>
						<XIcon />
					</Button>
				</div>
			))}
		</div>
	);
}

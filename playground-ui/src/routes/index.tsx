import { createFileRoute } from "@tanstack/react-router";
import { workbenchSearchSchema } from "#/features/workbench/search-schema";
import { WorkbenchScreen } from "#/features/workbench/workbench-screen";

export const Route = createFileRoute("/")({
	validateSearch: workbenchSearchSchema,
	component: WorkbenchIndex,
});

function WorkbenchIndex() {
	const search = Route.useSearch();
	const navigate = Route.useNavigate();

	return (
		<WorkbenchScreen
			endpointId={null}
			search={search}
			onSearchChange={(next) =>
				void navigate({
					search: (current) => ({ ...current, ...next }),
					replace: true,
				})
			}
			onNavigateEndpoint={(endpointId) =>
				void navigate({
					to: "/endpoints/$endpointId",
					params: { endpointId },
					search,
				})
			}
			onBack={() => undefined}
		/>
	);
}

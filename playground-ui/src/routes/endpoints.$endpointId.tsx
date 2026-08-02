import { createFileRoute, notFound } from "@tanstack/react-router";
import { endpointById } from "#/features/workbench/catalog";
import { workbenchSearchSchema } from "#/features/workbench/search-schema";
import { WorkbenchScreen } from "#/features/workbench/workbench-screen";

export const Route = createFileRoute("/endpoints/$endpointId")({
	validateSearch: workbenchSearchSchema,
	loader: ({ params }) => {
		if (!endpointById(params.endpointId)) throw notFound();
		return params.endpointId;
	},
	component: EndpointRoute,
});

function EndpointRoute() {
	const endpointId = Route.useLoaderData();
	const search = Route.useSearch();
	const navigate = Route.useNavigate();

	return (
		<WorkbenchScreen
			endpointId={endpointId}
			search={search}
			onSearchChange={(next) =>
				void navigate({
					search: (current) => ({ ...current, ...next }),
					replace: true,
				})
			}
			onNavigateEndpoint={(nextEndpointId) =>
				void navigate({
					to: "/endpoints/$endpointId",
					params: { endpointId: nextEndpointId },
					search,
				})
			}
			onBack={() => void navigate({ to: "/", search })}
		/>
	);
}

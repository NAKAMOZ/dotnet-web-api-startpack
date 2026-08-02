import { z } from "zod";

export const workbenchSearchSchema = z.object({
	q: z.string().catch("").default(""),
	group: z
		.enum([
			"all",
			"operations",
			"authentication",
			"account",
			"sessions",
			"security",
			"admin",
		])
		.catch("all")
		.default("all"),
	view: z
		.enum(["comfortable", "compact"])
		.catch("comfortable")
		.default("comfortable"),
});

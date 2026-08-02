import { paraglideVitePlugin } from "@inlang/paraglide-js";
import tailwindcss from "@tailwindcss/vite";
import { devtools } from "@tanstack/devtools-vite";

import { tanstackStart } from "@tanstack/react-start/plugin/vite";

import viteReact from "@vitejs/plugin-react";
import { defineConfig } from "vite";

const config = defineConfig({
	base: "/playground/",
	resolve: { tsconfigPaths: true },
	plugins: [
		devtools(),
		paraglideVitePlugin({
			project: "./project.inlang",
			outdir: "./src/paraglide",
			strategy: ["localStorage", "preferredLanguage", "baseLocale"],
		}),
		tailwindcss(),
		tanstackStart({
			router: { basepath: "/playground" },
			client: { base: "/playground/assets" },
			spa: {
				enabled: true,
				maskPath: "/",
				prerender: { outputPath: "/playground/index" },
			},
		}),
		viteReact(),
	],
});

export default config;

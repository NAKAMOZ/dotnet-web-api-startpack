import { cp, mkdir, readFile, rm, stat, writeFile } from "node:fs/promises"
import path from "node:path"
import { fileURLToPath } from "node:url"

const projectDirectory = path.dirname(path.dirname(fileURLToPath(import.meta.url)))
const sourceDirectory = path.join(projectDirectory, "dist", "client")
const targetDirectory = path.resolve(projectDirectory, "..", "wwwroot", "playground")

await stat(path.join(sourceDirectory, "index.html"))
await rm(targetDirectory, { recursive: true, force: true })
await mkdir(targetDirectory, { recursive: true })
await cp(sourceDirectory, targetDirectory, { recursive: true })

// TanStack serializes the prerender instant into each successful route match. It is not
// application data, but it otherwise changes the checked-in artifact on every build. Keep
// the hydration shape intact while making identical source trees produce identical files.
const targetIndex = path.join(targetDirectory, "index.html")
const html = await readFile(targetIndex, "utf8")
const routeTimestamp = /(u:)\d+(,s:"success",ssr:!0)/g
const timestamps = [...html.matchAll(routeTimestamp)]
if (timestamps.length === 0) {
	throw new Error("Expected at least one prerendered route timestamp in index.html")
}

const normalizedHtml = html.replace(
	routeTimestamp,
	(_match, prefix, suffix) => `${prefix}0${suffix}`,
)
await writeFile(targetIndex, normalizedHtml)

console.log(`Static playground synchronized to ${targetDirectory}`)

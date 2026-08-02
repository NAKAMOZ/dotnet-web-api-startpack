import { cp, mkdir, rm, stat } from "node:fs/promises"
import path from "node:path"
import { fileURLToPath } from "node:url"

const projectDirectory = path.dirname(path.dirname(fileURLToPath(import.meta.url)))
const sourceDirectory = path.join(projectDirectory, "dist", "client")
const targetDirectory = path.resolve(projectDirectory, "..", "wwwroot", "playground")

await stat(path.join(sourceDirectory, "index.html"))
await rm(targetDirectory, { recursive: true, force: true })
await mkdir(targetDirectory, { recursive: true })
await cp(sourceDirectory, targetDirectory, { recursive: true })

console.log(`Static playground synchronized to ${targetDirectory}`)

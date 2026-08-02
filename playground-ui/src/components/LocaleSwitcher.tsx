import { GlobeIcon } from "@phosphor-icons/react";
import {
	Select,
	SelectContent,
	SelectItem,
	SelectTrigger,
	SelectValue,
} from "#/components/ui/select";
import { m } from "#/paraglide/messages";
import { getLocale, locales, setLocale } from "#/paraglide/runtime";

const LANGUAGE_NAMES: Record<string, string> = {
	en: "English",
	de: "Deutsch",
	tr: "Türkçe",
};

export default function ParaglideLocaleSwitcher() {
	const currentLocale = getLocale();

	return (
		<div className="flex items-center gap-2">
			<GlobeIcon aria-hidden="true" className="size-4 text-muted-foreground" />
			<span className="sr-only" id="language-label">
				{m.language_label()}
			</span>
			<Select
				value={currentLocale}
				onValueChange={(locale) => {
					if (locale) setLocale(locale);
				}}
			>
				<SelectTrigger
					aria-labelledby="language-label"
					className="w-28"
					size="sm"
				>
					<SelectValue />
				</SelectTrigger>
				<SelectContent align="end">
					{locales.map((locale) => (
						<SelectItem key={locale} value={locale}>
							{LANGUAGE_NAMES[locale] ?? locale.toUpperCase()}
						</SelectItem>
					))}
				</SelectContent>
			</Select>
		</div>
	);
}

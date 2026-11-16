import { convertFileSrc } from "@tauri-apps/api/core";
import { useEffect } from "react";
import { useTranslation } from "react-i18next";

import { named } from "@/characters/names.ts";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select.tsx";
import { useWorld } from "@/world/world-store.ts";

interface JobSelectProps {
  /** The profession chosen, by the game's job id; `null` while none is. */
  value: number | null;
  onChange: (job: number) => void;
  label: string;
}

/** A profession setting: the harvesting professions the game data names, each with its icon, by name. */
export function JobSelect({ value, onChange, label }: JobSelectProps) {
  const { t, i18n } = useTranslation();
  const read = useWorld((store) => store.read);
  const load = useWorld((store) => store.load);
  useEffect(() => {
    void load();
  }, [load]);
  const jobs = read?.state === "read" ? read.atlas.jobs : [];
  const sorted = jobs
    .map((job) => ({ ...job, text: named(job.name, i18n.language) ?? String(job.id) }))
    .sort((a, b) => a.text.localeCompare(b.text, i18n.language));
  const chosen = sorted.find((job) => job.id === value);
  return (
    <Select value={value === null ? "" : String(value)} onValueChange={(job) => onChange(Number(job))}>
      <SelectTrigger
        size="sm"
        aria-label={label}
        className="h-[26px] w-full gap-1.5 rounded border-[#3c3c3c] bg-[#181818] px-2 text-xs data-[size=sm]:h-[26px]"
      >
        <SelectValue placeholder={t("zone.toChoose")}>
          {value !== null && chosen === undefined ? t("settings.job", { id: value }) : undefined}
        </SelectValue>
      </SelectTrigger>
      <SelectContent>
        {sorted.map((job) => (
          <SelectItem key={job.id} value={String(job.id)} className="text-xs">
            {job.icon !== null && <img src={convertFileSrc(job.icon)} alt="" className="size-4" />}
            {job.text}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

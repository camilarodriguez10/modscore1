import type { UploadFileView } from './api/types';

/**
 * "file 2 of 3" para cada archivo de una fuente que llegó en varios; nada si llegó en uno.
 * Los archivos rechazados antes de reconocer su fuente no cuentan.
 */
export function partLabels(files: readonly UploadFileView[]): (string | null)[] {
  const totals = new Map<string, number>();

  for (const file of files) {
    if (file.canonicalFileName) totals.set(file.canonicalFileName, (totals.get(file.canonicalFileName) ?? 0) + 1);
  }

  const seen = new Map<string, number>();

  return files.map((file) => {
    const total = file.canonicalFileName ? (totals.get(file.canonicalFileName) ?? 0) : 0;
    if (!file.canonicalFileName || total < 2) return null;

    const position = (seen.get(file.canonicalFileName) ?? 0) + 1;
    seen.set(file.canonicalFileName, position);
    return `file ${position} of ${total}`;
  });
}

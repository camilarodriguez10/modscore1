import type { MappingFieldView, MappingFileView, MappingMatch, MappingReviewView } from './api/types';

/**
 * El mapping de columnas previo a la carga, del lado del navegador.
 *
 * La capa System propone qué columna carga cada campo y valida al publicar. Aquí vive lo
 * que la persona decide mientras revisa: el estado de cada archivo, lo que se avisa antes
 * de cargar y lo que se envía. Sin React, para poder probarlo.
 */

/** Lo decidido para un archivo: su fuente y la columna de cada campo (null: no la trae). */
export interface MappingDecision {
  source: string | null;
  columns: Readonly<Record<string, string | null>>;
}

export type MappingDecisions = Readonly<Record<string, MappingDecision>>;

/** Cuánto del archivo se lee para encontrar el encabezado. */
const HEADER_BYTES = 64 * 1024;

/**
 * La primera línea del archivo, leída en el navegador. Es lo único que viaja para revisar
 * el mapping: las filas de datos no salen del equipo hasta que se carga.
 */
export async function readHeaderLine(file: Blob): Promise<string> {
  const text = await file.slice(0, HEADER_BYTES).text();
  const withoutBom = text.charCodeAt(0) === 0xfeff ? text.slice(1) : text;
  const end = withoutBom.search(/\r?\n/);

  return end < 0 ? withoutBom : withoutBom.slice(0, end);
}

/** El punto de partida: lo que propuso la capa System, campo por campo. */
export function initialDecisions(review: MappingReviewView): MappingDecisions {
  const decisions: Record<string, MappingDecision> = {};

  for (const file of review.files) {
    decisions[file.fileName] = {
      source: file.canonicalFileName,
      columns: Object.fromEntries(file.fields.map((field) => [field.column, field.header])),
    };
  }

  return decisions;
}

/** Cómo queda decidido un campo con lo que eligió la persona. */
export function effectiveMatch(field: MappingFieldView, chosen: string | null | undefined): MappingMatch {
  const header = chosen === undefined ? field.header : chosen;

  if (header === field.header) return field.match;
  return header === null ? 'excluded' : 'manual';
}

const MATCH_LABELS: Record<MappingMatch, string> = {
  name: 'Same name',
  alias: 'Known alias',
  manual: 'Your choice',
  remembered: 'As in the last upload',
  excluded: 'Not in this file',
  missing: 'Not in this file',
};

/** Cómo se decidió la columna, en palabras de quien carga. */
export function matchLabel(match: MappingMatch, required: boolean): string {
  if (required && (match === 'missing' || match === 'excluded')) return 'Required: choose a column';
  return MATCH_LABELS[match];
}

/** Qué estado visual lleva cada forma de decidir. */
export function matchTone(match: MappingMatch, required: boolean): 'good' | 'accent' | 'muted' | 'critical' {
  if (required && (match === 'missing' || match === 'excluded')) return 'critical';
  if (match === 'name' || match === 'alias') return 'good';
  if (match === 'manual' || match === 'remembered') return 'accent';
  return 'muted';
}

const TYPE_LABELS: Record<string, string> = {
  text: 'text',
  identifier: 'identifier',
  integer: 'whole number',
  long: 'whole number',
  number: 'number',
  boolean: 'yes / no',
  date: 'date',
};

export function typeLabel(type: string): string {
  return TYPE_LABELS[type] ?? type;
}

/**
 * Lo que impide cargar este archivo así, dicho antes de cargar. La capa System vuelve a
 * validar todo al publicar; esto solo evita el viaje.
 */
export function decisionProblems(file: MappingFileView, decision: MappingDecision | undefined): string[] {
  if (!file.canonicalFileName && !decision?.source) {
    return ['Choose the source of this file.'];
  }

  if (!decision) return [];

  const problems: string[] = [];
  const used = new Map<string, string>();

  for (const field of file.fields) {
    const header = decision.columns[field.column] ?? null;

    if (header === null) {
      if (field.required) problems.push(`'${field.column}' is required: choose the column of the file that holds it.`);
      continue;
    }

    const previous = used.get(header);

    if (previous) {
      problems.push(`The column '${header}' is chosen for both '${previous}' and '${field.column}'.`);
    } else {
      used.set(header, field.column);
    }
  }

  return problems;
}

/** Columnas del archivo que no cargan ningún campo con las decisiones actuales. */
export function unusedHeaders(file: MappingFileView, decision: MappingDecision | undefined): string[] {
  const used = new Set(Object.values(decision?.columns ?? {}).filter((header): header is string => header !== null));
  return file.headers.filter((header) => !used.has(header));
}

/**
 * Lo que se envía con la carga: solo los campos que no salen del nombre o de un alias —lo
 * que eligió la persona, lo que se recordó de la carga anterior y lo que el archivo no
 * trae—. Así lo registrado distingue una decisión de un reconocimiento automático.
 */
export function columnMapToSend(file: MappingFileView, decision: MappingDecision | undefined): Record<string, string | null> {
  const map: Record<string, string | null> = {};

  if (!decision) return map;

  for (const field of file.fields) {
    const chosen = decision.columns[field.column] ?? null;
    const match = effectiveMatch(field, chosen);

    if (match === 'manual' || match === 'remembered' || (match === 'excluded' && !field.required)) {
      map[field.column] = chosen;
    }
  }

  return map;
}

/** Cuántos campos quedan cargados y cuántos fuera, para el resumen de cada archivo. */
export function decisionSummary(file: MappingFileView, decision: MappingDecision | undefined) {
  const loaded = file.fields.filter((field) => (decision?.columns[field.column] ?? null) !== null).length;
  return { loaded, notLoaded: file.fields.length - loaded };
}

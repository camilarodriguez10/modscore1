import type { UploadResultView } from './api/types';

/** Lo que se manda en una carga: los archivos y, alineados con ellos, su fuente y su mapping. */
export interface UploadPayload {
  files: readonly File[];
  label: string;
  assignments?: readonly string[] | undefined;
  columnMaps?: readonly (Readonly<Record<string, string | null>> | null)[] | undefined;
}

export interface LeftOutFile {
  fileName: string;
  /** La fuente que se reconoció, o null si el archivo no es de ninguna. */
  source: string | null;
}

export interface LeaveOutPlan {
  leftOut: readonly LeftOutFile[];
  /** La misma carga sin esos archivos; la fuente y el mapping de los que quedan siguen alineados. */
  payload: UploadPayload;
}

const MASTER = 'eai_master.csv';

/**
 * Qué queda de una carga rechazada si se dejan fuera los archivos con problemas.
 *
 * La capa System publica todo o nada, y así sigue: dejar fuera un archivo es una decisión de
 * quien carga, no algo que pase solo. Una fuente que queda fuera es una fuente ausente: sus
 * métricas quedan sin dato y salen del score (ADR 0003). Solo hay plan cuando eso basta: el
 * resultado trae un veredicto por archivo enviado, alguno falló, y el EAI Master pasó, porque
 * sin él no hay portafolio.
 */
export function planLeaveOut(result: UploadResultView, sent: UploadPayload): LeaveOutPlan | null {
  if (result.accepted || result.files.length === 0 || result.files.length !== sent.files.length) {
    return null;
  }

  // `!error` y no `=== null`: la capa System omite los nulos.
  const failed = result.files.map((file) => Boolean(file.error));

  if (!failed.some(Boolean)) {
    return null;
  }

  const masterPassed = result.files.some((file, index) => !failed[index] && file.canonicalFileName === MASTER);

  if (!masterPassed) {
    return null;
  }

  const keep = (_: unknown, index: number) => !failed[index];

  return {
    leftOut: result.files
      .filter((_, index) => failed[index])
      .map((file) => ({ fileName: file.fileName, source: file.canonicalFileName ? file.source : null })),
    payload: {
      files: sent.files.filter(keep),
      label: sent.label,
      assignments: sent.assignments?.filter(keep),
      columnMaps: sent.columnMaps?.filter(keep),
    },
  };
}

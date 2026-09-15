import { useState } from 'react';
import { Link } from 'react-router';
import { useRulesTimeline } from '../api/hooks';
import { countOf } from '../i18n/notices';

const SEEN_KEY = 'modscore.rulesSeen';
const RECENT_DAYS = 14;
const DAY_MS = 24 * 60 * 60 * 1000;

const dateFormat = new Intl.DateTimeFormat('en-US', { dateStyle: 'medium' });

function readSeen(): string | null {
  try {
    return localStorage.getItem(SEEN_KEY);
  } catch {
    return null;
  }
}

function writeSeen(id: string) {
  try {
    localStorage.setItem(SEEN_KEY, id);
  } catch {
    // Almacenamiento bloqueado: el aviso vuelve a aparecer al recargar, nada mas.
  }
}

/**
 * Aviso de reglas nuevas.
 *
 * Publicar reglas cambia el score de todo el portafolio, y quien consulta tiene que
 * enterarse antes de sacar conclusiones de un salto. El aviso aparece una vez por version,
 * solo si es reciente, y se descarta en este navegador. A quien administra reglas le
 * ofrece ver que cambio.
 */
export function RulesChangedNotice({ canAdministerRules }: { canAdministerRules: boolean }) {
  const timeline = useRulesTimeline();
  const [seen, setSeen] = useState(readSeen);
  const active = timeline.data?.active;

  if (!active || active.isBuiltIn || !active.publishedAt || seen === active.id) return null;

  const publishedAt = new Date(active.publishedAt);
  if (Number.isNaN(publishedAt.getTime()) || Date.now() - publishedAt.getTime() > RECENT_DAYS * DAY_MS) {
    return null;
  }

  const dismiss = () => {
    writeSeen(active.id);
    setSeen(active.id);
  };

  return (
    <div
      role="status"
      className="flex flex-wrap items-center gap-x-4 gap-y-2 rounded-md border border-rule bg-accent-wash px-4 py-3 text-[13px]"
    >
      <p className="min-w-0 flex-1">
        <span className="font-semibold">Scoring rules changed to {active.id}</span>
        <span className="text-ink-2">
          {' '}
          on {dateFormat.format(publishedAt)} ({countOf(active.changeCount, 'change')}): “{active.reason}”. Every
          score and trend now uses them.
        </span>
      </p>

      {canAdministerRules && active.basedOn && (
        <Link
          to={`/admin/rules/compare?from=${encodeURIComponent(active.basedOn)}&to=${encodeURIComponent(active.id)}`}
          className="text-accent underline-offset-2 hover:underline"
        >
          See what changed
        </Link>
      )}

      <button
        type="button"
        onClick={dismiss}
        className="rounded-sm border border-rule bg-surface px-3 py-1 hover:bg-surface-2"
      >
        Dismiss
      </button>
    </div>
  );
}

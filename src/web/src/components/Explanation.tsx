import { createContext, useContext } from 'react';
import { Link } from 'react-router';
import type { AnalyticsDocument } from '../api/types';
import { areaName, statusLabel, type Explanation } from '../i18n/analytics';

/**
 * Las definiciones vigentes para quien las explique. El dashboard e Insights las
 * reciben con sus datos y las ponen aqui: cada panel arma su explicacion sin que haya
 * que pasarlas de mano en mano.
 */
export const DefinitionsContext = createContext<AnalyticsDocument | null>(null);

export const useDefinitions = () => useContext(DefinitionsContext);

/** El estado de un area como chip: provisional o acordado, con su dueno si lo tiene. */
export function AreaStatus({ area, definitions }: { area: string; definitions: AnalyticsDocument }) {
  const value = definitions.areas.find((a) => a.key === area);
  if (!value) return null;

  const agreed = value.status === 'agreed';

  return (
    <span className="inline-flex flex-wrap items-center gap-x-2 gap-y-1 text-[11.5px]">
      <span
        className="rounded-sm px-1.5 py-0.5 font-mono text-[10.5px] font-semibold tracking-wide uppercase"
        style={
          agreed
            ? { background: 'var(--app-good-wash)', color: 'var(--app-good-text)' }
            : { background: 'var(--app-warning-wash)', color: 'var(--app-warning-text)' }
        }
      >
        {statusLabel(value.status)}
      </span>
      {value.owner && <span className="text-ink-2">Owner: {value.owner}</span>}
    </span>
  );
}

/**
 * Como se calcula lo que se ve: los datos que entran, las reglas con los valores
 * vigentes y lo que todavia no se mide. Plegado por defecto: esta para quien pregunta,
 * sin tapar la respuesta.
 */
export function ExplanationBlock({
  explanation,
  definitions,
  title = 'How this is calculated',
}: {
  explanation: Explanation;
  definitions: AnalyticsDocument;
  title?: string;
}) {
  if (explanation.rules.length === 0 && explanation.considered.length === 0) return null;

  return (
    <details className="group border-t border-rule">
      <summary className="flex cursor-pointer list-none flex-wrap items-center gap-2 px-4 py-2.5 text-[12.5px] font-medium text-accent hover:bg-surface-2 [&::-webkit-details-marker]:hidden">
        <span aria-hidden="true" className="font-mono transition-transform group-open:rotate-90">
          ›
        </span>
        {title}
        {explanation.area && (
          <span className="ml-auto">
            <AreaStatus area={explanation.area} definitions={definitions} />
          </span>
        )}
      </summary>

      <div className="grid gap-4 px-4 pt-1 pb-4 text-[12.5px] leading-relaxed text-ink md:grid-cols-2">
        <Section title="How it is calculated" items={explanation.rules} />
        <div className="flex flex-col gap-4">
          <Section title="What is considered" items={explanation.considered} />
          {explanation.missing.length > 0 && <Section title="Not measured yet" items={explanation.missing} muted />}
        </div>
        <p className="text-[12px] text-ink-2 md:col-span-2">
          {explanation.area ? (
            <>
              These values belong to the {areaName(explanation.area)} definitions, published with the rules and
              editable by a rules administrator.{' '}
            </>
          ) : (
            <>This follows the score model and its bands. </>
          )}
          <Link
            to={`/definitions${explanation.area ? `#${explanation.area}` : ''}`}
            className="text-accent underline underline-offset-2"
          >
            See every definition
          </Link>
        </p>
      </div>
    </details>
  );
}

function Section({ title, items, muted = false }: { title: string; items: readonly string[]; muted?: boolean }) {
  if (items.length === 0) return null;

  return (
    <div>
      <p className="font-mono text-[10.5px] font-semibold tracking-[0.1em] text-ink-2 uppercase">{title}</p>
      <ul className={`mt-1.5 flex list-disc flex-col gap-1 pl-4 ${muted ? 'text-ink-2' : ''}`}>
        {items.map((item) => (
          <li key={item}>{item}</li>
        ))}
      </ul>
    </div>
  );
}

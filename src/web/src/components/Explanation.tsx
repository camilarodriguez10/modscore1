import { createContext, useContext } from 'react';
import { Link } from 'react-router';
import type { AnalyticsDocument } from '../api/types';
import { areaName, statusLabel, type Explanation, type Formula, type SourceRef } from '../i18n/analytics';

/**
 * Las definiciones vigentes para quien las explique. El dashboard las
 * recibe con sus datos y las pone aqui: cada panel arma su explicacion sin que haya
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
 * Como se calcula lo que se ve: que responde, los pasos con los valores vigentes, las
 * formulas, de que fuente y columna sale cada dato, el calculo con los datos en pantalla y lo
 * que todavia no se mide. Plegado por defecto: esta para quien pregunta, sin tapar la respuesta.
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

      <div className="@container flex flex-col gap-4 px-4 pt-1 pb-4 text-[12.5px] leading-relaxed text-ink">
        {explanation.summary && <p className="max-w-[90ch] text-[13px]">{explanation.summary}</p>}

        <div className="grid gap-4 @3xl:grid-cols-2">
          <div className="flex min-w-0 flex-col gap-4">
            <ExplanationList title="How it is calculated" items={explanation.rules} />
            <FormulaList formulas={explanation.formulas ?? []} />
          </div>
          <div className="flex min-w-0 flex-col gap-4">
            <SourceTable sources={explanation.sources ?? []} />
            <ExplanationList title="What is considered" items={explanation.considered} />
            <ExplanationList title="With the current data" items={explanation.current ?? []} />
            <ExplanationList title="Not measured yet" items={explanation.missing} muted />
          </div>
        </div>

        <p className="text-[12px] text-ink-2">
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

function SectionTitle({ children }: { children: string }) {
  return <p className="font-mono text-[10.5px] font-semibold tracking-[0.1em] text-ink-2 uppercase">{children}</p>;
}

/** Una lista con su titulo; vacia, no se muestra. */
export function ExplanationList({ title, items, muted = false }: { title: string; items: readonly string[]; muted?: boolean }) {
  if (items.length === 0) return null;

  return (
    <div>
      <SectionTitle>{title}</SectionTitle>
      <ul className={`mt-1.5 flex list-disc flex-col gap-1 pl-4 ${muted ? 'text-ink-2' : ''}`}>
        {items.map((item) => (
          <li key={item}>{item}</li>
        ))}
      </ul>
    </div>
  );
}

/** Las formulas: el nombre, la expresion en monoespaciado y que es cada termino. */
export function FormulaList({ formulas }: { formulas: readonly Formula[] }) {
  if (formulas.length === 0) return null;

  return (
    <div>
      <SectionTitle>Formulas</SectionTitle>
      <dl className="mt-1.5 flex flex-col gap-3">
        {formulas.map((formula) => (
          <div key={formula.name} className="min-w-0">
            <dt className="font-medium">{formula.name}</dt>
            <dd className="mt-1">
              <code className="block overflow-x-auto rounded-sm border border-rule bg-surface-2 px-2.5 py-1.5 font-mono text-[12px] whitespace-pre-wrap text-ink">
                {formula.expression}
              </code>
              {formula.where && formula.where.length > 0 && (
                <ul className="mt-1 flex flex-col gap-0.5 text-[12px] text-ink-2">
                  {formula.where.map((line) => (
                    <li key={line}>{line}</li>
                  ))}
                </ul>
              )}
            </dd>
          </div>
        ))}
      </dl>
    </div>
  );
}

/** De donde sale cada dato: el sistema y el reporte o archivo, y las columnas que se leen. */
export function SourceTable({ sources }: { sources: readonly SourceRef[] }) {
  if (sources.length === 0) return null;

  return (
    <div>
      <SectionTitle>Sources and fields</SectionTitle>
      <div className="mt-1.5 overflow-x-auto">
        <table className="w-full min-w-[360px] text-left text-[12px]">
          <thead>
            <tr className="border-b border-rule text-ink-2">
              <th scope="col" className="py-1 pr-3 font-medium">
                Data
              </th>
              <th scope="col" className="py-1 pr-3 font-medium">
                Source
              </th>
              <th scope="col" className="py-1 font-medium">
                Fields
              </th>
            </tr>
          </thead>
          <tbody>
            {sources.map((source) => (
              <tr key={`${source.data}-${source.origin}`} className="border-b border-rule align-top last:border-0">
                <th scope="row" className="py-1.5 pr-3 font-medium">
                  {source.data}
                </th>
                <td className="py-1.5 pr-3 text-ink-2">{source.origin}</td>
                <td className="py-1.5 font-mono text-[11.5px] text-ink-2">{source.fields}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

import { useSearchParams } from 'react-router';
import { useIngestion, useSession } from '../api/hooks';
import { Label, QueryError, Spinner } from '../components/primitives';
import { IngestionPanel } from './IngestionPage';
import { LineagePanel } from './LineagePage';
import { MappingPanel } from './MappingPanel';

/**
 * Pantalla de Datos: cargas y linaje, juntos.
 *
 * Estaban separados y era una separacion artificial: el linaje describe una carga,
 * asi que mirarlo obligaba a recordar cual estaba mirando uno. Ahora comparten el
 * selector de version y responden los dos a la misma pregunta —que entro y con que
 * calidad—, cada uno desde su lado.
 */
export function DataPage() {
  const [params, setParams] = useSearchParams();
  const session = useSession();
  const ingestion = useIngestion();

  // El linaje expone estadisticas de archivos completos y los EAI de las filas
  // rechazadas: es de quien ve todo el portafolio. Sin ese permiso la pestana no
  // existe, y el servidor lo niega igual si alguien la fuerza por la URL.
  const canSeeLineage = session.data?.permissions.fullPortfolio ?? false;
  const requested = params.get('tab');
  // "linaje" es el valor que usaban los enlaces de la Fase 10. El mapping describe cargas
  // completas como el linaje, y tiene la misma condicion.
  const tab =
    (requested === 'lineage' || requested === 'linaje') && canSeeLineage
      ? 'lineage'
      : requested === 'mapping' && canSeeLineage
        ? 'mapping'
        : 'uploads';

  if (ingestion.isPending) return <Spinner label="Loading source status…" />;

  if (ingestion.error) {
    return <QueryError error={ingestion.error} title="The screen could not be loaded" />;
  }

  const versions = ingestion.data.versions;
  const current = versions.find((v) => v.isCurrent) ?? versions[0];
  const selected = params.get('version') ?? current?.id ?? '';

  const update = (patch: { tab?: string; version?: string }) => {
    const next = new URLSearchParams(params);
    if (patch.tab) next.set('tab', patch.tab);
    if (patch.version !== undefined) {
      if (patch.version) next.set('version', patch.version);
      else next.delete('version');
    }
    setParams(next);
  };

  return (
    <div className="flex flex-col gap-4">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <Label>Data</Label>
          <h1 className="mt-1 text-[22px] leading-tight font-semibold">
            {versions.length} {versions.length === 1 ? 'upload' : 'uploads'} · {ingestion.data.provider} adapter
          </h1>
          <p className="mt-0.5 max-w-[70ch] text-[13px] text-ink-2">
            Each upload is a version you can query. The most recent one is current, and earlier ones
            stay available for comparison.
          </p>
        </div>

        {/* Un selector para las dos pestanas: el linaje que se ve es el de la carga
            elegida aqui, no el de la vigente por defecto. */}
        {versions.length > 0 && (
          <label className="flex flex-col gap-1">
            <Label>Upload</Label>
            <select
              value={selected}
              onChange={(event) => update({ version: event.target.value })}
              className="min-w-[14rem] rounded-sm border border-rule bg-surface px-2 py-1.5 text-[13px] text-ink"
            >
              {versions.map((version) => (
                <option key={version.id} value={version.id}>
                  {version.label}
                  {version.isCurrent ? ' · current' : ''}
                </option>
              ))}
            </select>
          </label>
        )}
      </header>

      <nav className="flex gap-1 border-b border-rule" aria-label="Data sections">
        <TabButton active={tab === 'uploads'} onClick={() => update({ tab: 'uploads' })}>
          Uploads
        </TabButton>
        {canSeeLineage && (
          <TabButton active={tab === 'mapping'} onClick={() => update({ tab: 'mapping' })}>
            Mapping
          </TabButton>
        )}
        {canSeeLineage && (
          <TabButton active={tab === 'lineage'} onClick={() => update({ tab: 'lineage' })}>
            Lineage
          </TabButton>
        )}
      </nav>

      {tab === 'uploads' && <IngestionPanel data={ingestion.data} selectedVersion={selected} />}
      {tab === 'mapping' && <MappingPanel onReview={() => update({ tab: 'uploads' })} />}
      {tab === 'lineage' && <LineagePanel version={selected || undefined} />}
    </div>
  );
}

function TabButton({
  active,
  onClick,
  children,
}: {
  active: boolean;
  onClick: () => void;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-current={active ? 'page' : undefined}
      className={`-mb-px border-b-2 px-3 py-1.5 text-[13.5px] ${
        active ? 'border-[var(--app-accent)] font-medium text-accent' : 'border-transparent text-ink-2 hover:text-ink'
      }`}
    >
      {children}
    </button>
  );
}

import { Component, type ErrorInfo, type ReactNode } from 'react';

/**
 * Un error de render sin capturar deja la pagina en blanco: React desmonta el
 * arbol completo. Este limite convierte ese caso en un mensaje legible con la
 * salida obvia, en lugar de una pantalla vacia sin explicacion.
 */
export class ErrorBoundary extends Component<
  { children: ReactNode },
  { error: Error | null }
> {
  override state: { error: Error | null } = { error: null };

  static getDerivedStateFromError(error: Error) {
    return { error };
  }

  override componentDidCatch(error: Error, info: ErrorInfo) {
    // En despliegue esto va a la telemetria; en local, a la consola.
    console.error('Uncaught render error', error, info.componentStack);
  }

  override render() {
    if (!this.state.error) {
      return this.props.children;
    }

    return (
      <main className="grid min-h-full place-items-center px-6">
        <div className="w-full max-w-md rounded-md border border-rule bg-surface px-6 py-6">
          <p className="font-mono text-[11px] font-semibold tracking-[0.12em] uppercase" style={{ color: 'var(--app-critical)' }}>
            Something broke while drawing the screen
          </p>
          <h1 className="mt-2 text-[17px] font-semibold">This view could not be shown</h1>
          <p className="mt-1.5 text-[13px] text-ink-2">
            The server-side calculation was not affected. Reload the page; if it happens again,
            the details are in the browser console.
          </p>
          <button
            type="button"
            onClick={() => window.location.reload()}
            className="mt-5 rounded-sm bg-accent px-4 py-2 text-[13.5px] font-medium text-on-accent hover:opacity-90"
          >
            Reload
          </button>
        </div>
      </main>
    );
  }
}

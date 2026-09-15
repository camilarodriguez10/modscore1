import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter } from 'react-router';
import { App } from './App';
import { ErrorBoundary } from './components/ErrorBoundary';
import { ApiError } from './api/client';
// Fuentes servidas desde el mismo origen: ninguna peticion a terceros que filtre la
// IP o la navegacion de quien usa una herramienta interna, y compatibles con la
// CSP estricta (font-src 'self'). Solo el subconjunto latino y los pesos en uso.
import '@fontsource/ibm-plex-sans/latin-400.css';
import '@fontsource/ibm-plex-sans/latin-500.css';
import '@fontsource/ibm-plex-sans/latin-600.css';
import '@fontsource/ibm-plex-sans/latin-700.css';
import '@fontsource/ibm-plex-mono/latin-400.css';
import '@fontsource/ibm-plex-mono/latin-500.css';
import '@fontsource/ibm-plex-mono/latin-600.css';
import './index.css';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      refetchOnWindowFocus: false,
      // Un 401 o un 404 no se reintentan: no son fallos transitorios.
      retry: (failureCount, error) =>
        failureCount < 2 && !(error instanceof ApiError && error.status < 500),
    },
  },
});

const container = document.getElementById('root');
if (!container) {
  throw new Error('index.html is missing the #root element.');
}

createRoot(container).render(
  <StrictMode>
    <ErrorBoundary>
      <QueryClientProvider client={queryClient}>
        <BrowserRouter>
          <App />
        </BrowserRouter>
      </QueryClientProvider>
    </ErrorBoundary>
  </StrictMode>,
);

import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError, api } from './client';

function respond(status: number, body?: unknown) {
  // Una respuesta nueva por llamada: el cuerpo de un Response solo se lee una vez.
  const fetchMock = vi.fn().mockImplementation(() =>
    Promise.resolve(
      new Response(body === undefined ? null : JSON.stringify(body), {
        status,
        headers: { 'Content-Type': 'application/problem+json' },
      }),
    ),
  );
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('api client', () => {
  it('reads the stable problem code from the ProblemDetails type', async () => {
    respond(400, {
      type: 'urn:modscore:problem:invalid-filter',
      title: 'Invalid filter',
      detail: 'Each filter accepts up to 100 characters.',
    });

    const error = await api.portfolio({ country: 'x'.repeat(101) }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({ status: 400, code: 'invalid-filter', title: 'Invalid filter' });
  });

  it('does not trust a type outside the modscore namespace', async () => {
    respond(500, { type: 'https://example.com/boom', title: 'Boom' });

    const error = await api.session().catch((e: unknown) => e);

    expect(error).toMatchObject({ status: 500, code: '' });
  });

  it('turns 401 into the sign-in message whatever the body says', async () => {
    respond(401, { title: 'Something internal' });

    const error = await api.session().catch((e: unknown) => e);

    expect(error).toMatchObject({ status: 401, title: 'Not signed in' });
  });

  it('survives an error without a JSON body', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('<html>', { status: 502, statusText: 'Bad Gateway' })));

    const error = await api.session().catch((e: unknown) => e);

    expect(error).toMatchObject({ status: 502, title: 'Bad Gateway' });
  });

  it('sends the CSRF token on mutations and never on reads', async () => {
    vi.stubGlobal('document', { cookie: 'other=1; __Host-modscore.csrf=token%20value' });
    const fetchMock = respond(204);

    await api.logout();

    const [, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    const headers = new Headers(init.headers);
    expect(init.credentials).toBe('same-origin');
    expect(headers.get('X-CSRF-Token')).toBe('token value');

    const readMock = respond(200, { isAuthenticated: false });
    await api.session();
    const [, readInit] = readMock.mock.calls[0] as [string, RequestInit];
    expect(new Headers(readInit.headers).has('X-CSRF-Token')).toBe(false);
  });

  it('encodes path segments and filters', async () => {
    const fetchMock = respond(200, {});

    await api.portfolio({ country: 'São Paulo & Co', criticalOnly: true, page: 2 });
    await api.overview('../14381');

    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/portfolio?country=S%C3%A3o+Paulo+%26+Co&criticalOnly=true&page=2&pageSize=25');
    expect(fetchMock.mock.calls[1]?.[0]).toBe('/api/applications/..%2F14381/overview');
  });
});

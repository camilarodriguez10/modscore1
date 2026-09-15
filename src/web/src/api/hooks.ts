import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './client';
import type { LeverSelection, PortfolioFilters, RulesDraft } from './types';

/**
 * Hooks de datos. El servidor es la fuente de verdad; el estado de cliente se
 * limita a lo que el usuario esta mirando.
 */

export function useSession() {
  return useQuery({
    queryKey: ['session'],
    queryFn: api.session,
    retry: false,
    staleTime: 5 * 60 * 1000,
  });
}

export function useLogin() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: api.login,
    onSuccess: () => queryClient.invalidateQueries(),
  });
}

export function usePortfolio(filters: PortfolioFilters, options?: { staleTime?: number }) {
  return useQuery({
    queryKey: ['portfolio', filters],
    queryFn: () => api.portfolio(filters),
    // Mantener lo anterior mientras llega lo nuevo: cambiar un filtro no tiene por
    // que vaciar la pantalla y perder el sitio donde estaba el ojo.
    placeholderData: (previous) => previous,
    ...(options?.staleTime === undefined ? {} : { staleTime: options.staleTime }),
  });
}

export function useOverview(eaiCode: string) {
  return useQuery({
    queryKey: ['overview', eaiCode],
    queryFn: () => api.overview(eaiCode),
    enabled: eaiCode.length > 0,
  });
}

export function useExplain(eaiCode: string) {
  return useQuery({
    queryKey: ['explain', eaiCode],
    queryFn: () => api.explain(eaiCode),
    enabled: eaiCode.length > 0,
  });
}

export function useLineage(version?: string) {
  return useQuery({
    queryKey: ['lineage', version ?? 'current'],
    queryFn: () => api.lineage(version),
  });
}

export function useIngestion() {
  return useQuery({ queryKey: ['ingestion'], queryFn: api.ingestion });
}

export function useHistory(eaiCode: string) {
  return useQuery({
    queryKey: ['history', eaiCode],
    queryFn: () => api.history(eaiCode),
    enabled: eaiCode.length > 0,
  });
}

/**
 * Publicar una carga cambia el portafolio entero, asi que al terminar se
 * invalida todo el cache: cualquier score que el usuario tenga en pantalla
 * corresponde a datos que ya no son los vigentes.
 */
export function useUpload() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      files,
      label,
      assignments,
    }: {
      files: readonly File[];
      label: string;
      assignments?: readonly string[] | undefined;
    }) => api.upload(files, label, assignments),
    onSuccess: (result) => {
      if (result.accepted) {
        queryClient.invalidateQueries();
      }
    },
  });
}

export function useRoadmapPlan(eaiCode: string) {
  return useQuery({
    queryKey: ['roadmap', eaiCode],
    queryFn: () => api.roadmap(eaiCode),
    enabled: eaiCode.length > 0,
  });
}

export function usePortfolioRoadmap(filters: PortfolioFilters = {}, groupBy = 'disposition') {
  return useQuery({
    queryKey: ['portfolio-roadmap', filters, groupBy],
    queryFn: () => api.portfolioRoadmap(filters, groupBy),
    placeholderData: (previous) => previous,
  });
}

export function useWhatIfLab(eaiCode: string) {
  return useQuery({
    queryKey: ['whatif', eaiCode],
    queryFn: () => api.whatIf(eaiCode),
    enabled: eaiCode.length > 0,
  });
}

/**
 * Simular es una mutacion aunque no cambie nada del lado servidor: lleva cuerpo,
 * va por POST y pasa por la validacion CSRF. Modelarlo como query invitaria a
 * cachearlo, y el escenario cambia con cada palanca que el usuario toca.
 */
export function useScenario(eaiCode: string) {
  return useMutation({
    mutationFn: (levers: readonly LeverSelection[]) => api.scenario(eaiCode, levers),
  });
}

export function useTarget(eaiCode: string) {
  return useMutation({
    mutationFn: (targetScore: number) => api.target(eaiCode, targetScore),
  });
}

// ------------------------------------------------------------- reglas

export function useRules() {
  return useQuery({ queryKey: ['rules'], queryFn: api.rules, retry: false });
}

/** Una version anterior, pedida en el momento de restaurarla. */
export function useRulesVersion() {
  return useMutation({ mutationFn: (id: string) => api.rulesVersion(id) });
}

/** Simular no cambia nada, pero lleva cuerpo y va por POST: es una mutacion. */
export function useSimulateRules() {
  return useMutation({
    mutationFn: ({ basedOn, draft }: { basedOn: string; draft: RulesDraft }) =>
      api.simulateRules(basedOn, draft),
  });
}

/**
 * Publicar reglas cambia el score de todo el portafolio: todo lo que haya en cache se
 * calculo con las reglas anteriores.
 */
export function usePublishRules() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ basedOn, reason, draft }: { basedOn: string; reason: string; draft: RulesDraft }) =>
      api.publishRules(basedOn, reason, draft),
    onSuccess: (result) => {
      if (result.published) {
        queryClient.invalidateQueries();
      }
    },
  });
}

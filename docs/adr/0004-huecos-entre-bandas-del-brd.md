# ADR 0004 — Huecos entre las bandas del BRD

**Estado:** aceptada · **Fecha:** 2026-09-10 · **Relacionado:** RFC-08

## Contexto

Dos tablas de mapeo del BRD dejan rangos sin cubrir:

| Metrica | Bandas documentadas | Hueco |
|---|---|---|
| Operational Score | 0-59,9 · 60-79,99 · 80-100 | (59,9 · 60) y (79,99 · 80) |
| Tech Debt Score | 0 · 0,1-2,4 · 2,5-5 | (0 · 0,1) y (2,4 · 2,5) |

Un valor dentro de un hueco — un Operational Score de 59,95 — no tiene puntaje
definido en el modelo.

## Decision

El motor **lanza excepcion** con el nombre de la metrica y el valor recibido, en
lugar de redondear al vecino mas cercano o de asumir la banda mas baja.

El generador dummy produce solo valores dentro de las bandas documentadas, para
que el hueco no se dispare por datos sinteticos.

## Consecuencias

- La excepcion es por aplicacion, no por lote: una fila fuera de banda no impide
  calcular el resto del portafolio.
- Un score silenciosamente incorrecto es el peor modo de falla de este sistema.
  Redondear en silencio esconderia un dato que el modelo no sabe interpretar.
- Los datos reales de `tech_debt` observados vienen cuantizados en pasos de 0,25,
  asi que nunca caen en sus huecos. Operational Score si podria.
- **Pendiente de confirmar (RFC-08):** si el modelo Power BI cubre estos rangos con
  bandas contiguas, se corrige el ruleset y la excepcion deja de ser alcanzable.

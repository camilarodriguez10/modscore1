# ADR 0001 — Arquitectura API-led en dos capas

**Estado:** aceptada · **Fecha:** 2026-09-10

## Contexto

La plataforma debe recalcular el Modernization Score sobre fuentes que van a
cambiar dos veces (dummy → archivos → base de datos), servir una SPA React 19 y,
mas adelante, otros canales. Los requisitos exigen microservicios y seguridad
reforzada.

## Decision

Separar en dos capas de API con responsabilidades excluyentes:

- **Capa Channel** (`channel-api`) — BFF y orquestador. Termina OIDC, mantiene la
  sesion, compone la respuesta de cada pantalla y aplica seguridad de borde.
  No contiene reglas de negocio ni accede a fuentes de datos.
- **Capa System** (`system-api`) — reglas de negocio y conexion a fuentes.
  Contiene el motor de scoring, what-if, roadmap y los adaptadores de fuente.
  No conoce pantallas, sesiones ni canales.

El motor (`Modernization.Scoring.Engine`) es una libreria referenciada dentro del
`system-api`, no un servicio aparte.

## Consecuencias

- Un canal nuevo (movil, Power BI) reutiliza la capa System sin tocarla.
- Los requisitos 8 y 9 caen enteros del lado System, detras del puerto
  `ISourceReader`: migrar de dummy a archivos y a base de datos no toca el
  `channel-api` ni el frontend.
- What-if y scoring comparten el mismo codigo en el mismo proceso, asi que el
  score baseline y el simulado no pueden divergir. Si el what-if invocara al
  scoring por HTTP, esa garantia se perderia.
- El `system-api` esta modularizado (`Scoring`, `WhatIf`, `Roadmap`, `Sources`).
  Cuando el volumen lo justifique, cualquier modulo se extrae como System API
  independiente sin cambiar el contrato que consume el `channel-api`.
- Si aparece orquestacion compleja entre varios System APIs, se inserta una capa
  Process entre ambas. El patron lo admite sin rediseno.

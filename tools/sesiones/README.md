# Scripts de las sesiones de trabajo

Scripts auxiliares que se usaron durante las sesiones de Claude Code para aplicar cambios al
código y a la documentación, y para actualizar el artefacto del plan. Se guardan para
trazabilidad: muestran cómo se hizo cada cambio.

**No hay que volver a ejecutarlos.** Cada uno reemplaza texto exacto sobre el estado del
repositorio en ese momento: hoy fallaría porque las anclas ya no existen, o duplicaría
cambios. Además:

- tienen rutas absolutas del equipo original (`C:\Users\kmiro\...`) y del directorio temporal
  de la sesión;
- los `update_artifact_*` y `p1x_artifact` editan una copia local del artefacto del plan
  (la última versión está en [docs/plan/](../../docs/plan/));
- `verify_phase10.py` usa el token de desarrollo `dev-only-service-token-not-for-any-real-environment`,
  el mismo de `appsettings.Development.json` y permitido en `.gitleaks.toml`;
- se escribieron para Python 3.14 y Node 22, sin dependencias externas.

No se incluyeron los logs, las cookies de sesiones de prueba, las copias de las capturas del
BRD ni las copias temporales de datos que quedaron en el directorio de la sesión.

| Fase | Fecha | Scripts |
|---|---|---|
| 03–07: correcciones, what-if, roadmap, historia y cargas | 2026-09-10 | `fix_phase3`, `fix_labels`, `fix_nulls`, `fix_web`, `fix_composite_levers`, `fix_whatif`, `fix_roadmap_params`, `fix_upload_ids`, `wire_whatif_channel`, `wire_roadmap_channel`, `wire_history`, `wire_ingestion`, `wire_ingestion_channel`, `empty_state*`, `update_artifact_p3` … `p7b`, `update_plan_p7` |
| 08: endurecimiento | 2026-09-11 | `capabilities`, `first_run_docs`, `phase8_edits`, `phase8_docs`, `phase8_artifact` |
| 09–10: base de datos y experiencia de consulta | 2026-09-12 | `plan_ampliacion`, `fase9_refactor`, `phase9_docs`, `phase9_artifact`, `verify_phase10` |
| 11: sitio en inglés | 2026-09-12 | `scan_spanish`, `p11_lib`, `p11_shared`, `p11_sources`, `p11_api`, `p11_tests`, `p11_review`, `p11_artifact` |
| 12: administración de reglas | 2026-09-12 | `p12_web`, `p12_asvs`, `p12_docs`, `p12_artifact` |
| 13: revisión y remediación | 2026-09-14/15 | `extract-draft.mjs`, `p13_artifact` |
| 14: verificacion de seguridad (ASVS) | 2026-09-15 | `p14_sec06` (auditoria de validaciones), `p14_sec07` (retencion de cargas), `p14_td04*` (contraste y accesibilidad), `p14_ci_e2e`, `p14_asvs`, `p14_docs`, `p14_plan`, `p14_handoff2`, `p14_td_items`, `p14_fixups` |
| 15: autenticacion con APIM | 2026-09-16 | `p15_system` (validacion JWT en la capa System), `p15_channel` (credencial de vida corta), `p15_apim` (reautenticacion y alcance por grupos), `p15_both` (modo de cambio sin corte), `p15_docs`, `p15_plan` |
| CI: paso de CodeQL sin code scanning | 2026-09-15 | `test-codeql-gate.mjs`: valida `ci.yml` y prepara SARIF de ejemplo para probar el paso "Sin hallazgos de severidad alta" (el directorio de destino va como argumento) |

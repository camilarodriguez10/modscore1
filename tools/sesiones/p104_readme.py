from patchlib import patch

patch("README.md", [
("""770 pruebas .NET: 270 del motor, 178 de contratos y 322 de API. Las dos que importan más""",
 """828 pruebas .NET: 309 del motor, 183 de contratos y 336 de API. Las dos que importan más"""),
("""npm --prefix src/web test          # 58 pruebas unitarias (Vitest)
npm --prefix src/web run test:e2e  # 40 escenarios: 19 de flujo y 21 de accesibilidad (axe)""",
 """npm --prefix src/web test          # 63 pruebas unitarias (Vitest)
npm --prefix src/web run test:e2e  # 49 escenarios: 24 de flujo y 25 de accesibilidad (axe)"""),
("""## Administrar las reglas

La pantalla **Rules** ajusta el cálculo sin recompilar ni desplegar""",
 """## Código: obsolescencia y riesgo por componentes

La pantalla **Code** (`/code`) mira lo que declaran los repositorios de cada aplicación en
GitHub Enterprise —runtimes, frameworks, servidores de aplicaciones, imágenes de sistema
operativo, bases de datos y bibliotecas— y lo cruza con las fechas de fin de vida y las
alertas de seguridad abiertas ([ADR 0026](docs/adr/0026-el-analisis-de-codigo-llega-de-github-enterprise.md)):

| Panel | Qué muestra |
|---|---|
| Indicadores | Repositorios analizados, aplicaciones con componentes vencidos y por vencer, alertas críticas y altas, y aplicaciones con riesgo de código alto o crítico. Cada uno filtra la tabla de aplicaciones |
| *Where is the stack reaching its end of life?* | Cada producto y ciclo en uso, con su fecha de fin de vida, el estado y el enlace a endoflife.date |
| *Which libraries need attention?* | Bibliotecas obsoletas, desactualizadas o con alertas, y cuántas aplicaciones las usan |
| *Where do the code and the inventory disagree?* | Aplicaciones vigentes según el Currency Score de CMT con componentes vencidos en el código, y al revés |
| *Which applications carry the most code risk?* | El índice de riesgo de código por aplicación; cada una abre sus repositorios y de dónde sale cada punto (`/code?app=14381`) |

- **El fin de vida sale de endoflife.date**, una foto versionada en
  `contracts/eol-catalog-v1.json` (22 productos, 273 ciclos, consultada el 2026-09-26). El
  soporte extendido pagado se muestra, pero no cuenta como soporte.
- **Las reglas son el área `code` de las definiciones**: 14 parámetros editables en
  *Rules → Portfolio analytics*. El índice no cambia el Modernization Score.
- **Hoy los repositorios son de demostración**: se generan del portafolio, deterministas por
  EAI, y la pantalla lo dice en ámbar. Ese proveedor solo arranca en Development
  (`CodeInventory:Provider=Sample`, el valor por defecto ahí); fuera de Development el
  valor por defecto es `None` y la pantalla dice que no hay inventario. La integración real
  —un recolector aparte con una GitHub App de solo lectura— está en el plan (PLAN §8ter) y
  en GH-01 a GH-09 de [docs/PENDING-ITEMS.md](docs/PENDING-ITEMS.md).
- El mismo análisis está en `GET /api/code` (con los filtros de siempre) y
  `GET /api/code/applications/{eai}`.

## Administrar las reglas

La pantalla **Rules** ajusta el cálculo sin recompilar ni desplegar"""),
])

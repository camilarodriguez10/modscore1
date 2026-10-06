# Runbook — El score no coincide con el reporte de Power BI

**Síntoma:** el score de una aplicación, o el promedio de una región, no coincide con el
reporte *Modernization Score v1.3* de Power BI.

**Lo que ya está probado:** el motor reproduce el BRD y el reporte. La EAI 14381 da 79,67
(ejemplo del BRD) y la 10969 da 79,61 (dashboard v1.3, métrica por métrica). Por eso, cuando
no coincide, lo primero es revisar **qué datos entraron**, no la fórmula.

**Cómo comparar:** en el sitio, abre la aplicación (Portfolio → la EAI) y mira **cómo se
calculó**. Ahí aparecen los tres pilares y cada métrica: valor crudo, puntos, peso y, si quedó
fuera, por qué. Los pilares equivalen a las columnas del reporte:

| Reporte | Sitio |
|---|---|
| Risk Vectors Score | Pilar de riesgo (50 de peso): Kenna, DR, SCA críticas y altas, MFA, OSS |
| Arch & Maturity Score | Pilar de arquitectura (40): Raw Tech, API, arquitectura, estrategia de producto, deuda técnica |
| Stability Score | Pilar de estabilidad (10): monitoreo y Operational Score |
| Wght Monitoring / Operational Score | Las dos métricas de estabilidad, 5 cada una |

Las columnas **Risk** y **Currency** del dashboard no son esas. Risk es el pilar de riesgo
invertido a 0–100, donde más alto es peor; Currency es el Raw_Tech_Score de CMT. Para pasar de
Risk al pilar: pilar = 100 − Risk × 0,8. Un Risk de 55 equivale a un pilar de riesgo de 56.

---

## 1. Si no coincide el promedio de una región (por ejemplo, LATAM)

1. **La población no es la misma.** El reporte excluye SaaS, canceladas y dadas de baja, y
   solo considera Common Service y Core Business. **El sitio todavía no aplica ese filtro:**
   cuenta todas las aplicaciones del EAI Master cargado (DD-09). Compara primero el número de
   aplicaciones de la región en los dos.
2. **El filtro de estado del reporte.** La vista LATAM filtra *Application Status* con una
   selección múltiple. En el sitio, el filtro **App status** permite reproducirlo a mano.
3. **Los valores de región.** Una aplicación con otro texto en *Region Name*, o sin región,
   cae en otra columna o en «sin asignar».

## 2. Si no coincide el score de una aplicación

Revisa en este orden. Cada causa dice dónde se ve en el sitio.

1. **Falta una fuente en la carga.** Una métrica sin dato sale del cálculo y el peso se
   reparte entre las demás (ADR 0003). No cuenta como cero, pero mueve el score. Tres casos
   ya vistos:
   - **SCA** (18 de peso: críticas 10, altas 8). Si el archivo falló o se dejó fuera, falta en
     todas las aplicaciones. En el reporte, una aplicación sin hallazgos abiertos tiene 100 en
     las dos métricas; sin el archivo, esos 100 dejan de contar y el score baja.
   - **APIM onboarding** (API Type, 5 de peso). No venía en la carga del 2026-10-05.
   - **Monitoreo y Operational Score.** El reporte suele dar 5,00 y 5,00 (estabilidad 100).
     Si el sitio no encuentra el EAI en el archivo de monitoreo, o no reconoce el valor,
     pierde métricas que estaban en 100.

   *Dónde se ve:* en el detalle, el **peso disponible** de cada pilar y la métrica marcada como
   sin dato. En **Data → Lineage**, las fuentes de cada carga y sus filas huérfanas.
2. **Valores cargados como vacíos.** En el EAI Master, un valor ilegible en un campo opcional
   queda sin dato (ADR 0028). *Dónde se ve:* **Values loaded as empty**, en el resultado de la
   carga.
3. **Filas repetidas por EAI.** Si un export trae una fila por fecha, queda la última del
   archivo. No necesariamente es el mes que filtra el reporte. Vale para el maestro y para OSS,
   monitoreo, arquitectura y estrategia de producto.
4. **Columnas «Suma de …» de Power BI.** Si el export agregó varias filas de la misma
   aplicación (por ejemplo, varios meses), el valor es una suma, no el dato de la aplicación.
   Kenna, Operational o Raw Tech quedan inflados.
5. **Deuda técnica por APP_ID.** Se une por APP_ID, que en la organización es el mismo código
   que el EAI. Si el archivo de deuda trae otro código, no se une y la métrica (10 de peso)
   queda sin dato. *Dónde se ve:* filas huérfanas de Tech Debt en Lineage.
6. **La regla del cero** (ADR 0019). Un 0 reportado se trata como sin dato en las métricas
   donde el BRD lo pide.
7. **La fecha de corte.** El reporte se refresca en otra fecha que los exports de la carga.
8. **La versión de reglas.** Comprueba en **Rules** que la activa sea la v1.3 de fábrica. Una
   versión publicada con otros pesos o tablas mueve todos los scores.

## 3. Después

- Si la causa es una fuente que faltaba o venía mal, corrige el archivo y publica una carga
  nueva. Una versión publicada no se edita ([carga defectuosa](carga-defectuosa.md)).
- Si la causa es la población (DD-09), hace falta que el negocio confirme qué columna marca
  Common Service y Core Business, y qué valores de *Application Status* son canceladas o
  dadas de baja.

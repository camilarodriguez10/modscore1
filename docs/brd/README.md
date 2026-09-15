# Capturas del BRD y del dashboard

Las 34 capturas del BRD "Modernization Scoring Tool", del dashboard Power BI v1.3 y
de los archivos fuente (`0809 *`) son la **fuente de verdad de las reglas de
calculo**: ante una contradiccion entre documentos, mandan las capturas.

Viven en `docs/brd/capturas/` (unos 121 MB) y **no se versionan por defecto**
(`.gitignore`): son documentacion interna del negocio y el repositorio remoto puede
no ser privado.

## Donde esta cada regla

| Captura | Contenido |
|---|---|
| image_67125761 | Portada del BRD y "General Application Details" (campos sin score) |
| image_67173889 | Pesos de Modernization Risk Vectors (Kenna 8, DR 7, SCA Critical 10, High 8, Medium 0, Low 0, MFA 7, OSS 10) |
| image_67128833 | Final de la tabla de pesos de Architecture (Product Strategy Tolerate 1, Eliminate 2, Migrate 3, Tech Debt 5) |
| image_67142913 | Pesos de Stability (Monitoring 5, Operational 5) y regla "ALL Sections" (nulos y ceros se excluyen, normalizacion 20-100) |
| image_67130113 | Ejemplo trabajado EAI 14381 (44,75 / 60 -> 79,67) |
| image_67143169 | Tablas de MFA y Open-Source Score |
| image_67143937 | RAW Tech Score y Coding Language |
| image_67145217 | Architectural Value Count (definiciones) |
| image_67184897 | Product Strategy (Invest 100, Tolerate 60, Eliminate 20, Migrate 20) y Tech Debt (0 -> 100, 0,1-2,4 -> 60, 2,5-5 -> 20) |
| image_67187201 | SCA Flaws (High/Critical 20, Medium 20, Low 60, None 100; Medium y Low removidos del score) |
| image_67188993 | Automation DevSecOps (removido) y Native REST APIs / APIM |
| image_67199233 | Containerized (Yes 100, No NULL; "added to product strategy (2 pts) and architecture value (2 pts)") y Application Disposition (removido) |
| image_67180033 | Tabla de puntaje de Application Disposition |
| image_67165185, image_67168769 | Ejemplo antiguo EAI 9768 con un esquema obsoleto: no usar como referencia |
| image_67214081 | Archivo `0809 MONITOREO` (Agent, APM, Monitoring, Synthetics) |
| image_67214593 | Archivo `0809 OSS_Score_by_EAI_3` (incluye una fila con EAI Code 0) |

## Incluirlas en el repositorio

Solo si el repositorio remoto es privado y el negocio lo autoriza:

```bash
git add -f docs/brd/capturas/
```

# Runbook — Reglas publicadas con un error

**Cuándo usarlo:** alguien publicó desde el portal una versión de reglas que puntúa mal:
un peso equivocado, una banda que deja aplicaciones sin score o una palanca apagada por
error.

**Principio:** las versiones de reglas no se editan ni se borran. Volver atrás es
**publicar otra vez** la versión buena. Así el registro de quién cambió qué, y con qué
impacto, sigue completo (ADR 0018).

## 1. Confirmar el problema

1. En **Rules → Versions**, identificar la versión vigente y la anterior.
2. Abrir **N changes** de la vigente: la lista de cambios y el impacto con que se publicó
   están guardados con ella.
3. Cruzar con la auditoría: el evento **4211** registra quién publicó, qué versión, sobre
   cuál, cuántos cambios y el impacto simulado. Un **4212** muestra intentos rechazados.
4. Si hay aplicaciones sin score, la pantalla de cálculo de una de ellas responde
   `ruleset-mismatch`: su dato ya no cae en ninguna banda.

## 2. Restaurar desde el portal (camino normal)

1. **Rules → Versions → Load into draft** en la última versión buena.
2. **Simulate impact.** El impacto debe ser el inverso del de la versión defectuosa.
3. Escribir el motivo, por ejemplo: «Restore v1.6: the OSS bands left 40 applications
   without a score».
4. **Publish as a new version.** Si pide reautenticación, es lo esperado: publicar exige
   haber iniciado sesión en los últimos 10 minutos.
5. El proceso que publica aplica las reglas al instante. El resto de las réplicas las
   toma en su siguiente comprobación (`Rules:RefreshSeconds`, 15 s por defecto).

## 3. Si el portal no está disponible

Solo cuando no se puede publicar y el error es grave. Cada paso queda anotado en el
registro de incidentes, con hora y responsable.

- **Almacén de archivos.** Mover —no borrar— el directorio de la versión defectuosa fuera
  de `.rules`, por ejemplo a `.rules-retiradas/`. La anterior vuelve a ser la última, y
  las réplicas la toman en su siguiente comprobación. Al volver el portal, publicar la
  buena como versión nueva y devolver el directorio retirado a su lugar, para que el
  historial quede completo.
- **Base de datos.** Copiar la fila de `rules_version` a una tabla de respaldo antes de
  quitarla, y seguir el mismo criterio: al volver el portal, restaurar publicando.
- **Nunca** editar el JSON de una versión a mano. `FromStored` valida el documento: si no
  se puede leer, el servicio mantiene las reglas que tenía y registra el error 5302. Un
  documento editado a mano que sí se lea puntuaría con reglas que nadie simuló.

## 4. Verificar

- **Rules** muestra la versión restaurada como activa.
- La aplicación de referencia 14381 vuelve a su score esperado con las reglas de fábrica:
  **80,70** (79,67 con las bandas del BRD, ADR 0032).
- El promedio del portafolio y la distribución por tramo coinciden con los de antes del
  cambio defectuoso.
- La auditoría tiene el 4211 de la restauración.

## 5. Después

- Revisar por qué la simulación no alertó. El impacto se muestra antes de publicar: si
  «No longer scored» era mayor que cero, conviene discutir si ese caso debería impedir
  la publicación.
- Revisar quién tiene el rol `ruleset.admin`. Es un rol de muy pocas personas.

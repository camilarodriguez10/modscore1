# Runbook — Carga defectuosa

**Síntoma:** una carga se publicó (evento `4201`) pero sus datos están mal. El
promedio del portafolio salta sin razón, aparecen aplicaciones con peso disponible
anómalo, o el equipo dueño de una fuente avisa que exportó el archivo equivocado.

La validación de esquema impide que entren archivos **mal formados**. No puede
impedir que entren archivos **bien formados con datos equivocados**: eso es este
runbook.

**Principio:** una versión publicada no se edita. Se retira y, si hace falta, se
publica una corregida.

---

## 1. Confirmar

1. Pantalla **Cargas** → la evolución del portafolio muestra el salto en la carga
   sospechosa. Anotar su identificador (`20260915-093012-c3d4`).
2. Comparar el **peso disponible** de ese punto con el anterior. Un salto de score
   con el mismo peso detrás apunta a datos cambiados; con más peso, a una fuente que
   antes no llegaba.
3. Buscar en la auditoría quién la publicó:
   ```text
   EventId=4201 version=20260915-093012-c3d4
   ```

## 2. Retirar la versión

La versión vigente es la más reciente del directorio de historia. Retirar la
defectuosa hace vigente la anterior.

1. **Moverla fuera del directorio de historia**, no borrarla: es evidencia.
   ```bash
   kubectl -n modernization-score exec deploy/system-api -- \
     sh -c 'mkdir -p /data/cuarentena'
   ```
   La imagen chiseled no tiene shell. Hacerlo desde un pod de mantenimiento que monte
   el mismo volumen:
   ```bash
   kubectl -n modernization-score scale deploy/system-api --replicas=0
   kubectl -n modernization-score apply -f mantenimiento-history.yaml
   kubectl -n modernization-score exec mantenimiento -- \
     mv /data/history/20260915-093012-c3d4 /data/cuarentena/
   kubectl -n modernization-score delete pod mantenimiento
   ```
2. **Reiniciar system-api.** El portafolio vigente se cachea en memoria; el reinicio
   lo vuelve a leer. La historia retirada sale sola del cache.
   ```bash
   kubectl -n modernization-score scale deploy/system-api --replicas=1
   ```
3. **Verificar** en la pantalla Cargas que la versión ya no aparece y que el
   portafolio muestra la anterior como vigente.

## 3. Corregir

1. Pedir al equipo dueño de la fuente el archivo correcto.
2. Publicarlo desde la pantalla Cargas con una etiqueta que lo explique
   (`Corrige 0915 — tech_debt reexportado`).
3. Registrar el incidente: versión retirada, motivo, quién la publicó, quién la
   corrigió.

## 4. Si la carga fue malintencionada

Si la auditoría muestra que la publicó alguien que no debía, o desde una sesión que
no reconoce su dueño, **esto ya no es un problema de datos**: seguir el
[runbook de incidente de seguridad](incidente-de-seguridad.md) y conservar la
versión en cuarentena como evidencia.

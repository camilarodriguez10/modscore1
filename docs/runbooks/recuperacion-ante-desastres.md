# Runbook — Recuperación ante desastres

## Qué hay que proteger

El sistema calcula todo a partir de las cargas. **Lo único que no se puede
reconstruir es la historia de cargas publicadas**; el resto se vuelve a construir o
a pedir.

| Activo | Dónde | Se reconstruye desde | Respaldo |
|---|---|---|---|
| **Historia de cargas** | PVC `system-api-history` | No se reconstruye | Snapshot del volumen tras cada carga + diario |
| Llaves de Data Protection | PVC `channel-api-keys` | Perderlas solo cierra sesiones | Snapshot diario |
| Secretos | Gestor de secretos | Rotación | El del propio gestor |
| Imágenes | Registro | Git + lock files + Dockerfiles | El registro; el build es reproducible |
| Reglas de scoring y roadmap | Embebidas en la imagen | Git | Git |
| Auditoría | Destino inmutable del SIEM | No se reconstruye | Retención del SIEM |

## Objetivos

| | Objetivo | Cómo se logra |
|---|---|---|
| **RPO** | **0 cargas perdidas** | Snapshot del volumen disparado por cada evento `4201` (carga publicada), además del diario |
| **RTO** | **1 hora** | Manifiestos declarativos, imágenes en el registro, restauración de un único volumen |

Mientras el snapshot por evento no esté automatizado, el RPO real es **24 horas**:
una carga publicada entre dos respaldos diarios se pierde, y hay que volver a
subirla desde los archivos originales. **Conservar los archivos subidos durante 48
horas** cubre ese hueco.

---

## Escenario A — Se perdió el volumen de historia

1. **Detener system-api** para que nada escriba mientras se restaura.
   ```bash
   kubectl -n modernization-score scale deploy/system-api --replicas=0
   ```
2. **Restaurar el último snapshot** del PVC `system-api-history` con la herramienta
   del proveedor de almacenamiento.
3. **Arrancar system-api.** Al iniciar limpia cualquier staging interrumpido.
   ```bash
   kubectl -n modernization-score scale deploy/system-api --replicas=1
   ```
4. **Verificar:**
   - `GET /health` → 200.
   - Pantalla Cargas: la versión más reciente coincide con el último evento `4201`
     de la auditoría **anterior** al snapshot.
   - Las cargas posteriores al snapshot se vuelven a subir desde sus archivos.

## Escenario B — Se perdió el cluster

1. Crear el namespace y aplicar los manifiestos:
   ```bash
   kubectl apply -k deploy/k8s
   ```
2. Restaurar los dos volúmenes desde sus snapshots **antes** de que los pods
   arranquen: el de historia es obligatorio; el de llaves es opcional (sin él, todos
   vuelven a iniciar sesión).
3. Configurar el `SecretProviderClass` contra el gestor de secretos.
4. Verificar como en el escenario A, y además iniciar sesión con un usuario real.

## Escenario C — Una región del IdP no responde

Las sesiones abiertas siguen funcionando: viven en el servidor. **No se pueden abrir
sesiones nuevas.** No hay acción del lado del sistema; escalar al equipo de identidad.

---

## Ensayo

Una vez por trimestre, en un entorno que no sea producción:

1. Restaurar el snapshot más reciente de producción en un volumen nuevo.
2. Levantar system-api contra ese volumen.
3. Comparar la lista de versiones y el promedio del portafolio de la versión vigente
   con los de producción.
4. Registrar el tiempo total. Si supera el RTO, corregir el procedimiento, no el
   objetivo.

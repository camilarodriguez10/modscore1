# Runbook — Incidente de seguridad

## Señales

Todas las emite la categoría de log `Modernization.Audit`, con un EventId estable
para que el SIEM alerte sin depender del texto.

| EventId | Evento | Sospechar cuando |
|---|---|---|
| 4001 · 4002 | Sesión iniciada · cerrada | Inicios desde un subject que no debería existir |
| 4003 | Sesión expirada por tiempo absoluto | Uso continuo de 12 horas: probable script con sesión humana |
| **4101** | Acceso denegado | Ráfaga de un mismo subject sobre recursos distintos: alguien probando |
| **4102** | Token CSRF ausente o inválido | Cualquier ráfaga: intento de CSRF o cliente automatizado mal hecho |
| 4103 | Límite de peticiones | Sostenido para un subject: extracción masiva o abuso |
| **4201** | Carga publicada | Publicada por un subject inesperado, o fuera de horario |
| 4202 | Carga rechazada | Muchas seguidas: alguien tanteando la validación |
| **4203** | Alcance insuficiente en la capa System | **Siempre.** La Channel ya lo impide: si llega a la System, algo la está saltando |

Además, los rechazos del token de servicio que registra la propia autenticación de
system-api: **cualquiera es grave**, porque solo channel-api debería llamarla.

---

## 1. Contener

Elegir según lo que se sospeche. Todas se pueden aplicar juntas.

| Sospecha | Acción | Efecto |
|---|---|---|
| Una cuenta de usuario comprometida | Deshabilitar el usuario en el IdP **y** revocar sus sesiones (paso siguiente) | Deja de entrar |
| Sesiones robadas, sin saber cuáles | Revocar las llaves de Data Protection ([rotación, sección 3](rotacion-de-secretos.md#3-llaves-de-data-protection)) | Todas las sesiones mueren |
| Token de servicio expuesto | Rotarlo **sin** ventana de doble token: poner el nuevo en ambos lados y reiniciar | Solo channel-api vuelve a llamar a la System |
| Cargas malintencionadas | Quitar el rol `ingestion.write` a todos en el IdP | Nadie publica; las consultas siguen |
| Abuso desde fuera | Bloquear en el ingress | Corta antes de llegar a la aplicación |

**No borrar nada.** Versiones sospechosas a cuarentena
([carga defectuosa](carga-defectuosa.md)), llaves revocadas a evidencia.

## 2. Establecer el alcance

1. **Qué subject.** Filtrar la auditoría por el subject sospechoso: todos sus
   eventos desde su primer inicio de sesión.
2. **Qué vio.** El alcance de ese usuario está en sus claims del IdP. Con alcance
   parcial, solo pudo ver su tramo: la capa System lo aplica sobre los datos.
3. **Qué cambió.** Eventos `4201` del periodo. Cada versión publicada tiene su
   `manifest.json` con `uploadedBy`.
4. **Si hubo 4203**, revisar los logs de red del mesh: identificar quién llamó a la
   capa System, porque no debería haber sido posible.

## 3. Erradicar y recuperar

1. Corregir la causa: la cuenta, el secreto expuesto, la regla de red.
2. Retirar las versiones malintencionadas y publicar las correctas.
3. Restituir los roles y las llaves.
4. Verificar: inicio de sesión, pantallas con datos, una carga de prueba en un
   entorno que no sea producción.

## 4. Después

- Línea de tiempo con los EventId como fuente.
- Qué señal lo detectó, y cuánto antes podría haberlo hecho otra.
- Si faltó un control, abrir un hallazgo en `docs/security/hallazgos.md` con su
  severidad y un test que lo cierre.

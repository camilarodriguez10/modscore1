# ADR 0018 — Las reglas se publican desde el portal como versiones

**Estado:** aceptada · **Fecha:** 2026-09-12 · **Fase:** 12

## Contexto

El punto 7 de la ampliación pide ajustar las fórmulas del score y los escenarios de
what-if desde el portal, solo para administradores. Hasta la Fase 11 el ruleset, las
palancas y el catálogo MAPS viajaban embebidos en los ensamblados: cambiar un peso era
recompilar y desplegar.

Quien edita las reglas mueve el score de toda la organización. Es la capacidad con más
alcance del sistema, así que el listón de seguridad es el más alto.

## Decisión

### 1. Se editan valores, no estructura

| Editable | No editable, y por qué |
|---|---|
| Peso de cada métrica. El del pilar es la suma de sus métricas, y el total tiene que dar 100 | Qué métricas existen y de dónde leen su dato: lo decide un extractor, que es código |
| Bandas numéricas: límites, puntaje y etiqueta, hasta 10, ordenadas y sin solaparse | El pilar al que pertenece cada métrica y su tipo de mapeo |
| Puntaje de cada caso y de cada categoría de producto | Las claves de los casos y categorías: son lo que produce el extractor, y una nueva nunca coincidiría |
| Qué palancas ofrece el laboratorio y la cantidad por defecto de las que mueven productos | La transformación de cada palanca: describe una acción real sobre el snapshot |
| Score sano y base de usuarios significativa de las alertas del roadmap | La normalización 20–100: los tramos, el score objetivo y los umbrales hablan esa escala |
| Qué palancas entrega cada roadmap y cuántas semanas dura cada hito | Nombres, definiciones y hitos: contenido traducido en la Fase 11 |

El total de 100 no lo exige el motor, que normaliza por el peso disponible. Lo exige el
sitio: todas las pantallas leen «60 de 100», y un total distinto las haría mentir.

### 2. Tres partes, una sola versión

Ruleset, palancas y roadmap se publican juntos porque dependen entre sí. Si se
publicaran por separado, un roadmap podría proyectar con una palanca que el laboratorio
ya no ofrece. Se valida al construir el modelo: un roadmap que entrega una palanca
apagada no llega a publicarse.

### 3. Los objetivos de las palancas salen de las bandas

«Subir el Open-Source Score a 75» era una constante que tenía que coincidir con el
límite de la banda alta del ruleset. Ahora la palanca lleva la métrica a la banda de
mayor puntaje que declare el ruleset vigente, y nunca empeora un valor que ya está
dentro de ella.

Con las reglas de fábrica los objetivos siguen siendo 75, 339, 75 y 80, y los golden
del what-if no cambian. Tres nombres pasan a «…to the top band», porque el número deja
de ser fijo.

### 4. Borrador, simulación, publicación

- **Borrador.** Vive en el navegador, en la sesión, así sobrevive a la reautenticación.
- **Simulación.** El servidor valida el borrador y recalcula todo el portafolio con las
  dos versiones de reglas: cuántas aplicaciones suben y cuántas bajan, el promedio antes
  y después, los tramos, las diez que más se mueven, la proyección de los planes y las
  alertas.
- **Publicación.** El servidor vuelve a validar y a simular. El impacto que queda guardado
  es el que calculó él, no el que vio el navegador.

Cada error llega como aviso con su código, y llegan todos a la vez. Un valor recibido
que no corresponde a nada del catálogo se señala por su posición: no se devuelve.

### 5. Versiones inmutables y concurrencia optimista

- Las reglas de fábrica son **v1.3**, y cada publicación suma uno a la menor.
- Un borrador se prepara sobre una versión y solo se publica si esa sigue siendo la
  vigente. Si no, se rechaza con `rules.stale` en lugar de pisar el cambio ajeno.
- El almacén repite la comprobación al escribir. En archivos, el movimiento atómico del
  directorio falla si la versión ya existe; en base de datos, falla la clave primaria.
- Volver atrás es publicar otra vez el contenido anterior. Nada se edita ni se borra.

### 6. El almacén sigue el patrón de las fuentes

Hay un puerto, `IRulesStore`, con tres adaptadores que elige el mismo `DataSource:Provider`:

- **Memoria**, con Dummy.
- **Archivos**, en `.rules` dentro del directorio de cargas: queda en el mismo volumen
  persistente, y el proveedor de cargas ignora los directorios sin manifiesto.
- **Base de datos**, en la tabla `rules_version` (migración `ReglasVersionadas`).

Una prueba de contrato común verifica los tres.

### 7. Sin redesplegar

`RulesService` consulta la última versión del almacén como mucho cada
`Rules:RefreshSeconds` (15 s por defecto). Publicar activa la versión nueva al instante
en el proceso que publicó, y el resto de las réplicas la toma en su siguiente
comprobación. Cada caché de scores lleva la versión con que se calculó y se descarta sola.

### 8. La historia se puntúa con las reglas vigentes

**Esto corrige el plan.** La sección 8bis.3 decía que las cargas anteriores conservarían
la versión con que se calcularon. Hacerlo mezclaría en una misma tendencia un cambio de
datos con un cambio de reglas: si MFA pasa a pesar más, la curva saltaría sin que ninguna
aplicación haya mejorado.

Toda la serie se recalcula con las reglas vigentes. Lo que no se reescribe es el
registro: cada versión guarda quién la publicó, por qué, qué cambió y el impacto que
tuvo al publicarse.

> *Corrección de la revisión del 2026-09-14.* Este ADR decía que «cada punto dice con qué
> versión se calculó». Los puntos no llevan versión: todos usan la vigente, y la tarjeta
> de la tendencia lo dice. Desde la revisión, la tendencia **marca con una línea punteada
> cuándo se publicó cada versión** (`GET /api/rules/timeline`, visible para quien
> consulta y sin quién publicó), para que un salto no se lea como mejora cuando lo que
> cambió fueron las reglas.

### 9. Controles de seguridad

| Control | Detalle |
|---|---|
| Rol propio | `ruleset.admin`, separado de `ingestion.write`: quien sube datos no decide cómo se puntúan. Exige además alcance completo |
| Reautenticación (ASVS 3.7.1) | Publicar exige haberse autenticado en los últimos 10 minutos. Con OIDC se pide `max_age=0` y solo cuenta un `auth_time` de menos de 5 minutos. La marca la escribe la capa Channel; un claim con el mismo nombre que llegue del IdP se descarta |
| Defensa en profundidad | La capa System vuelve a exigir alcance completo y los permisos `rules.admin` y `reauthenticated`, reenviados por un llamador con token de servicio. Solo acepta valores de una lista blanca |
| CSRF | Doble envío, como toda mutación |
| Frecuencia | 10 simulaciones por minuto y 5 publicaciones por hora por usuario, en ventana deslizante |
| Entrada | Cuerpo acotado a 128 KB. JSON estricto: sin campos desconocidos, sin nulos donde no caben y con profundidad acotada. Identificadores de versión por lista blanca |
| Retorno de la reautenticación | Lista blanca de una sola ruta: `returnTo` no puede sacar a nadie del sitio |
| Auditoría | Eventos 4211 (publicada, con versión, base, cambios e impacto), 4212 (rechazada), 4004 (reautenticación completada) y 4104 (publicación sin reautenticación) |
| Validación junto al motor | Vive en la capa System. La capa Channel acota la forma del cuerpo pero no decide qué es una regla válida |

## Consecuencias

- **El criterio de aceptación es una prueba de punta a punta:** publicar un peso desde la
  capa Channel mueve el portafolio, la aplicación de referencia puntúa con la versión
  nueva, el evento 4211 queda registrado y la versión guarda su impacto. Otra prueba
  publica en una réplica y lee la versión nueva desde otra, sin reiniciar ninguna.
- ASVS 3.7.1 pasa de N/A a cumple, y 4.3.3 de parcial a cumple.
- El usuario de desarrollo trae `ruleset.admin` por defecto. Con OIDC, el rol lo asigna el IdP.
- El almacén de archivos con varias réplicas depende de un volumen compartido y del
  movimiento atómico del directorio. Para más de una réplica, la base de datos.
- La reautenticación contra un IdP real no se verificó en local, donde corre el esquema
  de desarrollo. La lectura del `auth_time` está aislada en `StepUp` para probarla en el
  primer entorno con IdP.

## Ampliación de la revisión del 2026-09-14

- **Comparar antes de publicar.** `GET /system/rules/compare?from&to&eai` compara dos
  versiones sobre todo el portafolio y, con `eai`, una aplicación métrica por métrica. La
  simulación de un borrador acepta `eaiCode` con el mismo detalle. La SPA lo muestra en
  `/admin/rules/compare`, con el borrador de la pestaña como destino posible.
- **Exportar el impacto.** `GET /api/admin/rules/compare/export.csv` entrega cada
  aplicación antes y después, con el mismo control de acceso que comparar.
- **Almacén caído.** Si el almacén no responde se sigue puntuando con las reglas vigentes
  y se registra el evento 5303; publicar se rechaza con `rules.storeUnavailable` y leer
  una versión anterior responde 503, no 404.
- **Versiones recordadas.** Una versión publicada no cambia, así que su modelo y sus
  metadatos se validan una vez por proceso. Listar ya no reconstruye 50 motores.
- **Simulación más barata.** El lado de las reglas vigentes se calcula una vez por carga y
  por versión; cada simulación solo calcula el borrador, en paralelo. Medido en reposo con
  8 clientes y 250 aplicaciones: p50 de 117 ms a 47 ms, y de 66 a 168 simulaciones por
  segundo.
- **Reautenticación independiente del proveedor.** `IReauthenticationFlow` aísla OIDC y
  cierra la sesión anterior antes de ir al IdP ([ADR 0020](0020-camino-de-oidc-a-oauth-con-apim.md)).

# ADR 0010 — La carga de archivos como superficie de ataque

**Estado:** aceptada · **Fecha:** 2026-09-10

## Contexto

Subir archivos es el endpoint más peligroso de cualquier aplicación: acepta
contenido arbitrario de un cliente y lo escribe en disco. Aquí además **reemplaza
el portafolio completo**, así que un abuso no es solo una intrusión: es corromper
la base sobre la que se toman decisiones de inversión.

## Decisión

Controles en capas, cada uno con su test:

| Control | Dónde | Qué impide |
|---|---|---|
| Autenticación + CSRF | `channel-api` | Que un formulario de terceros reemplace el portafolio |
| Límite de tamaño de cuerpo | Ambas capas, antes de leer un byte | Agotamiento de memoria y disco |
| Extensión `.csv` y content-type en lista blanca | `IngestionService` | Ejecutables y contenido activo |
| Tope de archivos por carga | `IngestionService` | Peticiones abusivas |
| **El nombre del cliente nunca es una ruta** | `UploadService` | Path traversal (`../../appsettings.json`) |
| Lista blanca de nombres esperados | `UploadService` | Archivos que no corresponden a ninguna fuente |
| Validación de esquema antes de aceptar | `CsvSourceReader` | Datos que producirían scores incorrectos |
| Staging + commit atómico | `UploadService` | Que una carga a medias deje el portafolio inconsistente |

Lo que se escribe en disco es **el nombre canónico del contrato**, nunca el que
envió el cliente. El nombre entrante solo se usa para emparejar contra la lista
blanca.

## Consecuencias

- **La validación de entrada corre antes de comprobar si la función está
  habilitada.** El orden inverso haría que un archivo malformado se rechazara por
  el motivo equivocado, y que la validación nunca se ejercitara con el adaptador
  Dummy activo.
- **Un fallo al ensamblar es un rechazo con motivo, no un 500.** El usuario acaba
  de subir ocho archivos: necesita saber cuál falló y por qué, no un error
  genérico.
- **Solo CSV.** Los exports reales observados son CSV — las capturas muestran el
  aviso de Excel sobre guardar como CSV. Soportar XLSX añadiría una librería de
  parseo y una superficie de ataque considerable a cambio de conveniencia; se
  deja fuera hasta que alguien lo pida con un caso concreto.
- **El adaptador Dummy no admite cargas**, y la pantalla lo dice en lugar de
  ofrecer un botón que no puede funcionar.

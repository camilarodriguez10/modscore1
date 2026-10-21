# ADR 0037 — Fuentes leídas desde bases de datos, con conexiones seguras

**Estado:** aceptada · **Fecha:** 2026-10-21 · **Relacionado:** [ADR 0010](0010-la-carga-de-archivos-como-superficie-de-ataque.md), [ADR 0017](0017-el-backend-no-redacta-frases.md), [ADR 0034](0034-excel-y-varios-archivos-por-fuente.md)

## Contexto

El 2026-10-21 se pidió validar si la aplicación podía conectar las fuentes desde bases de
datos, sin archivos, y ajustar lo necesario para que las conexiones fueran seguras y tuvieran
varias formas de conexión, con una guía en inglés de dónde configurarlas.

La revisión encontró que no se podía. El adaptador `Database` solo guarda en SQLite los
archivos que alguien sube; leer las fuentes de sus sistemas de origen no existía. El almacén
de producción de las versiones sigue pendiente (RFC-07) y no es parte de esta decisión.

## Decisión

1. **Un proyecto propio, `Modernization.Sources.Connectors`.** Los drivers de SQL Server,
   PostgreSQL, Oracle y ODBC solo entran a la capa System; ni el motor, ni el lector de
   archivos, ni el generador dummy los cargan. Versiones estables sin vulnerabilidades
   conocidas; NuGetAudit corta la restauración si aparece una. Dos ajustes de
   dependencias:
   - `System.Security.Cryptography.Pkcs` se fija en 10.0.13, porque la 9.0.18 que traen los
     drivers es vulnerable;
   - IdentityModel sube a 8.22.0, porque SqlClient 7.1.1 lo exige.
2. **Todo es configuración del despliegue** (sección `Connectors`, apagada por defecto).
   Ninguna pantalla define una conexión ni una consulta. Ninguna respuesta del API trae
   cadenas de conexión, servidores ni consultas: solo el nombre de cada conexión, su motor y
   si la fuente sale de una vista o de una consulta.
3. **TLS verificado obligatorio, impuesto por la aplicación** (`SecureConnectionFactory`).
   - SQL Server: `Encrypt=Mandatory` o `Strict`, nunca `TrustServerCertificate=True`. Se
     rechazan los métodos que necesitan a una persona.
   - PostgreSQL: `SSL Mode=VerifyFull` o `VerifyCA`.
   - Oracle: TCPS en el descriptor o `tcps://`.
   - ODBC: quien configura declara `EncryptionEnforcedByDriver` después de comprobarlo.

   Solo en Development, `AllowInsecureInDevelopment` permite un contenedor local sin TLS.
4. **Secretos solo en archivos montados**: la clave por archivo de `Secrets:Directory` que ya
   usaba el token de servicio, o `ConnectionStringFile` / `PasswordFile`. Se leen en cada
   conexión, así que rotarlos no exige redesplegar. Los mensajes nunca citan la ruta, el
   contenido ni la excepción del driver al leer la cadena.
5. **Varias formas de conexión**, todas probadas contra su driver:
   - SQL Server: usuario SQL, Windows integrado / Kerberos, y Microsoft Entra (identidad
     administrada, workload identity, service principal y cadena por defecto);
   - PostgreSQL: contraseña, certificado de cliente y Kerberos;
   - Oracle: contraseña sobre TCPS y wallet;
   - ODBC: cualquier driver.
6. **Solo lectura en tres capas, además de la cuenta**, que debe tener solo `SELECT` sobre las
   vistas:
   - toda sentencia corre en una transacción que siempre se deshace;
   - la transacción es de solo lectura donde el motor lo permite (`SET TRANSACTION READ ONLY`
     en PostgreSQL y Oracle; `ApplicationIntent=ReadOnly` en SQL Server);
   - la consulta se valida por forma (`SourceQueries`): una sola sentencia `SELECT` o
     `WITH`, sin `;`, comentarios, `INTO` ni `FOR UPDATE`, y un `Table` que es solo un
     identificador.
7. **El mismo camino que un archivo.** `DatabaseSourcePuller` escribe cada resultado como CSV
   invariante en un directorio temporal privado y lo entrega al `ISnapshotPublisher` con la
   fuente ya asignada. Así se aplican:
   - la identificación de la fuente, los alias y el mapping recordado;
   - la validación de filas;
   - los topes de una carga;
   - la publicación atómica.

   Si una fuente falla, no se publica nada. Una lectura a la vez por proceso.
8. **Quién y cómo.**
   - *Pull from databases* y *Test connections* en la pantalla Data exigen el rol de carga,
     alcance completo, token CSRF y el límite de frecuencia de las cargas.
   - La capa System exige además alcance completo y deja eventos de auditoría:
     - 4215, lectura publicada;
     - 4216, rechazada;
     - 4217, conexiones probadas.
   - `PullInterval` (mínimo 15 minutos) publica solo, con autor `connector-schedule`.
9. **Se valida al arrancar.** Una sección `Connectors` que no puede funcionar detiene el
   proceso con todos sus problemas juntos:
   - fuente desconocida;
   - conexión inexistente;
   - motor inválido;
   - consulta que no es de lectura;
   - intervalo corto;
   - adaptador `Dummy`.

La guía, en inglés, está en [`docs/database-connections.md`](../database-connections.md).

## Alternativas consideradas

- **Que la pantalla defina conexiones y consultas:** convertiría a quien carga en
  administrador de bases y abriría la puerta a ejecutar SQL desde el navegador.
- **Un camino de carga propio para las bases:** dos validaciones que deberían coincidir
  terminan divergiendo; pasar por el CSV reutiliza todo lo probado.
- **Publicar lo que se pudo leer:** una versión sin una fuente se leería como que la fuente
  dejó de reportar, y bajaría el score sin motivo.
- **Confiar el TLS a la cadena que escriba cada despliegue:** basta un
  `TrustServerCertificate=True` olvidado para perder la verificación; la aplicación lo impide.

## Consecuencias

- **Una lectura es una carga completa.** Las fuentes que no se configuran quedan fuera de esa
  versión, y `eai_master` siempre tiene que estar. Mezclar fuentes de bases y archivos en una
  misma versión queda pendiente.
- **Lo que el despliegue tiene que proveer:**
  - una regla de egress por servidor en `deploy/k8s/network-policies.yaml`;
  - los volúmenes de certificados, wallets o keytabs;
  - para ODBC y Kerberos en Linux, una imagen derivada con el driver o las librerías.
- **Varias réplicas:** solo una debe tener `PullInterval`.
- **Pruebas.**
  - Conectores, en 64 pruebas:
    - las reglas de TLS y secretos por motor;
    - las formas documentadas;
    - la validación de consultas y de la configuración;
    - el CSV;
    - la lectura completa contra SQLite.
  - API por las dos capas: alcance, CSRF, que la configuración no llegue al navegador y el
    arranque que se detiene.
  - Una prueba de punta a punta de la tarjeta, en claro y oscuro.

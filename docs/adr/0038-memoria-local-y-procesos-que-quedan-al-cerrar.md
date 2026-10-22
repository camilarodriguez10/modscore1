# ADR 0038 — Memoria de la capa System y procesos que quedan al cerrar

**Estado:** aceptada · **Fecha:** 2026-10-22 · **Relacionado:** [ADR 0030](0030-una-carga-grande-cabe-en-el-pod.md), [H-06](../security/hallazgos.md)

## Contexto

En otra máquina se vio que la aplicación andaba bien, pero el equipo quedaba lento después de
detenerla con Ctrl + C y cerrar las ventanas. Se pidió una revisión exhaustiva.

La revisión reprodujo el ciclo completo en un equipo con 16 procesadores lógicos y 15,6 GB:
compilar, levantar las tres ventanas como dice el README, usar la aplicación, Ctrl + C real en
cada consola y cerrar. Se midió cada proceso.

| Qué | Resultado |
|---|---|
| Procesos de la aplicación después de Ctrl + C | **Ninguno.** Se cierran la System, la Channel, sus `dotnet run`, Vite, `node` y `esbuild`, y los puertos quedan libres |
| Servicios en segundo plano mientras corre | Precálculo y revisión de reglas cada 15 s: menos de 0,5 % de un núcleo en total |
| Después de un `dotnet build` | 9 procesos de MSBuild y `VBCSCompiler.exe`, **~1,4 GB**, vivos entre 10 y 15 minutos. **No se cierran con Ctrl + C ni al cerrar las ventanas** |
| Después de un `dotnet run` que compila (camino A de la guía) | Lo mismo: 9 procesos de MSBuild, ~940 MB, que sobreviven a la ventana |
| System con 4 cargas reales (SCA de 350 mil filas) | Pico de 1,27 GB; se queda en 1,1 GB después de calcular |
| System con 8 cargas reales | Pico de **2,6 GB**; se queda en 0,9 GB |
| Mismo caso con el heap limitado a 300 MB | Funciona: los datos vivos son menos de 300 MB. El resto es memoria recuperable que el GC no devuelve mientras el equipo tiene RAM libre |

Las dos causas de la lentitud son:

1. **Los procesos de compilación que quedan vivos** hasta 15 minutos después de cerrar todo.
2. **El pico de memoria de la System al calcular la historia.** Leía tantas cargas a la vez como
   núcleos, hasta 8, y retenía la memoria después. En un portátil eso manda otros programas
   a disco, y al cerrar tardan en volver.

## Decisión

1. **La historia lee 2 cargas a la vez** (`Portfolio:MaxParallelVersions`, de 1 a 8). Con 8
   cargas reales, el pico baja de 2,6 a 1,1 GB. El primer cálculo pasa de 12,5 a 28 s, en
   segundo plano. Con 4 cargas el tiempo no cambia (14 s).
2. **La memoria se devuelve después de cada lectura pesada.** Cuando una pasada del precálculo
   leyó alguna carga entera (al arrancar, tras publicar una carga o unas reglas),
   `PortfolioWarmup` hace una recolección agresiva del GC
   (`GCCollectionMode.Aggressive`), que devuelve la memoria al sistema operativo. Las pasadas
   en caché no la hacen: `PortfolioService.VersionsRead` dice si hubo lecturas. Con 8 cargas,
   la System se queda en 179 MB en lugar de 932 MB.
3. **Compilar sin dejar procesos vivos.** Los `dotnet build` del README y de la guía llevan
   `-nodeReuse:false -p:UseSharedCompilation=false`. La guía agrega
   `dotnet build-server shutdown` al detener, para el camino que compila en cada ventana. Las
   dos guías explican cómo comprobar que no quedó nada.

## Alternativas consideradas

- **GC de estación de trabajo** (`DOTNET_gcServer=0`): medido, retiene lo mismo (1,1 GB).
  El problema no era el GC de servidor, sino que nada disparaba una recolección.
- **`DOTNET_GCConserveMemory`**: medido, sin efecto por la misma razón.
- **Una sola lectura a la vez**: menos pico, pero el primer cálculo se alarga más. Queda como
  variante (`--Portfolio:MaxParallelVersions=1`) para equipos con poca memoria.
- **Fijar las opciones de compilación en `Directory.Build.props`**: haría más lentos los builds
  de desarrollo y del CI, donde esos servidores sí ayudan. La guía local es donde molestan.

## Consecuencias

- **La aplicación no instala servicios ni deja procesos.** Lo que quedaba vivo era de la
  compilación, y ahora no queda.
- **Producción.** El pod de la System tiene 1 GiB de límite. Con varios núcleos y muchas cargas,
  la lectura en paralelo anterior podía pasarlo. Ahora el pico queda acotado por 2 cargas.
- **La primera tendencia tras arrancar** puede tardar más con muchas cargas grandes. El cálculo
  sigue siendo en segundo plano, y en un servidor se puede subir a 4.
- **Pruebas.** El contador de lecturas y que la historia es la misma con 1 u 8 lecturas en
  paralelo.

# Demo guiada — guion de 4 minutos

Guion en inglés para presentar el sitio con los datos dummy. Son unas 410 palabras habladas,
poco más de 3 minutos a ritmo de presentación. Con las pausas para los clics, son unos 4
minutos. Lo que va entre corchetes es la acción en pantalla y no se lee.

## Antes de empezar

- **Cargar los cuatro trimestres de ejemplo** antes de arrancar, para que las tendencias
  tengan historia. Desde la carpeta del proyecto:
  - en `cmd`: `xcopy /E /I /Y data\timeline data\local`;
  - en PowerShell: `New-Item -ItemType Directory -Force data\local | Out-Null` y luego
    `Copy-Item -Recurse -Force data\timeline\* data\local`.
- **Levantar el sitio** con los comandos de «Levantar el stack» del [README](../README.md)
  —System con carga de archivos, Channel y SPA— y entrar con **Sign in** antes de que llegue el
  público. Sin Node ni Vite son dos ventanas, como explica «Sin Vite ni Node: el sitio ya
  compilado», con el sitio en el puerto 5100.
- **Dejar abierto el Dashboard**, sin filtros: <http://localhost:5173/> (o el puerto 5100 si
  la Channel sirve el sitio compilado).
- **Tener a mano la aplicación 14381**: <http://localhost:5173/applications/14381>.
- **Los números se leen de la pantalla.** El guion no fija ninguno, para que sirva con
  cualquier carga.
- **Code y Roadmap están apagadas por defecto** y no forman parte de este recorrido. Para
  mostrarlas al final, se encienden en la Channel con `Features__Code=true` y
  `Features__PortfolioRoadmap=true`.

## Guion

**[0:00 · Opening]**

Good morning, everyone. This is the Modernization Score platform, running on dummy data: two
hundred and fifty generated applications across four quarterly uploads, with no real
information behind them.

It answers one question: what should we modernize, and why. Instead of spreadsheets and static
reports, the platform calculates it the same way every time and explains every number.

**[0:25 · Dashboard]** *[Dashboard, the home page]*

The home page opens with five indicators: portfolio health, applications at risk,
modernization candidates, currency exposure and resilience exposure.

*[Click "Applications at risk"]* Every number opens the applications behind it.
*[Close the panel]*

Below them, six panels, one per executive question. How healthy is the portfolio? What do we
modernize first? Where is the complexity? Where does risk concentrate? What drives
obsolescence? Where are the resilience gaps?

*[Open "How this is calculated" under the risk panel]* Each panel explains itself. You see the
formula, the source system and column behind every figure, and the same calculation worked out
with the data on screen. Nothing is a black box.

**[1:25 · One application]** *[Open application 14381, then "See how it was calculated"]*

For a single application, the calculation page shows the formula with its own values: which
metrics count, their weights and the points each one adds. A metric without data leaves the
formula, so a missing source never penalizes an application.

**[1:55 · Future plan]** *[Future plan]*

The future plan is generated automatically when the data is loaded. This is the action plan to
reach ninety, step by step. For each step you see:

- the metric it moves, and from what value to what value;
- how many points it adds;
- who owns it: security, development, operations;
- the concrete actions, down to the vulnerable libraries and their CVEs.

*[Open in What-If]* It is exactly the plan the what-if lab finds: same levers, same score.

*[Set the target between today's score and 90, click "Find the minimum plan", then "See the
detailed plan in Future plan"]* And if leadership asks for a different target, the detailed plan
follows it.

**[3:00 · Data and rules]** *[Data tab]*

Data is where it all starts.

- Source files, in CSV or Excel, are validated before anything is published.
- Every upload becomes a version you can compare.
- When rows are rejected, the screen says exactly why.

The platform can also read the sources straight from their databases, over encrypted,
read-only connections.

*[Rules tab]* And the business owns the rules. Weights, levers and every threshold are editable
without code. Every change is simulated before it is published and kept for audit.

**[3:45 · Close]**

Next steps:

- connecting the live source systems;
- enterprise sign-in through the API gateway;
- agreeing the provisional definitions with their owners.

So: one place, one calculation, every number explained, and a plan for every application.
Thank you. I'm happy to take questions, or to open any application you'd like to see.

# Demo guiada — guion de 7 minutos

Guion en inglés para presentar el sitio con los datos dummy: qué hace cada pantalla y hacia
dónde puede crecer. Unas 680 palabras habladas: a ritmo de presentación son unos 5 minutos y
medio, y las pausas para los clics y los desplazamientos en la pantalla Code completan los 7. Lo que va entre corchetes es la acción en
pantalla, no se lee.

## Antes de empezar

- **Cargar los cuatro trimestres de ejemplo antes de arrancar**, para que las pantallas
  muestren la evolución en el tiempo desde el primer minuto. Desde la carpeta del proyecto,
  en `cmd`: `xcopy /E /I /Y data\timeline data\local`; en PowerShell:
  `New-Item -ItemType Directory -Force data\local | Out-Null` y luego
  `Copy-Item -Recurse -Force data\timeline\* data\local`.
- **Levantar las tres ventanas** con los comandos de «Levantar el stack» del
  [README](../README.md) —System con carga de archivos, Channel y SPA— y entrar con
  **Sign in** antes de que llegue el público.
- Dejar abierto el **Dashboard**, sin filtros: <http://localhost:5173/> (o el 5100 si la
  Channel sirve el sitio compilado).
- Tener a mano la aplicación **14381**: <http://localhost:5173/applications/14381>.
- Los números se leen de la pantalla: el guion no los fija, para que sirva con cualquier carga.

## Guion

**[0:00 · Opening]**

Good morning, everyone. Today I'll walk you through the Modernization Score platform.
Everything you'll see runs on dummy data: two hundred and fifty generated applications,
across four quarterly uploads, that behave like a real portfolio without exposing any real
information.

The idea is simple. The answer to "what should we modernize, and why" usually lives in
spreadsheets and static reports. This platform puts it in one place, calculates it the same
way every time, and explains every number.

**[0:30 · Dashboard]** *[Dashboard, the home page]*

This is the home page. The banner states the question it answers: where is the highest
concentration of risk and modernization opportunity, why, and what decision does it require?

Below it, five indicators: portfolio health, applications at risk, modernization candidates,
currency exposure — legacy platforms by the Currency Score from CMT — and resilience exposure.

*[Click "Applications at risk"]* Every number is clickable: this panel lists exactly the
applications behind it. No black boxes.

*[Close the panel and scroll]* Then six panels, one per executive question: how healthy is
the portfolio, what to modernize first, where to reduce complexity, where risk concentrates,
what drives obsolescence, and where the resilience gaps are.

*[Open "How this is calculated" under a panel]* Each panel explains its own rule with the
thresholds in force, and this chip shows how many definitions are still provisional.

**[1:50 · Portfolio]** *[Portfolio tab]*

Portfolio is the full inventory, with each application's score and its three pillars. The
filters follow you across every tab, and anything here downloads as CSV.

**[2:10 · One application]** *[Open application 14381]*

Let's open one application: seventy-nine point six seven.

*[Calculation]* The calculation page shows the formula with this application's own values:
the metrics included, their weights and the points each one contributes. A metric with no
data leaves the formula entirely, so a missing source never penalizes an application. This is
the worked example from the business requirements, matched to the second decimal.

*[What-if]* The what-if lab turns that into action: each lever shows how much the score would
rise, and you can set a target score to see what it takes.

**[3:05 · Code]** *[Code tab]*

The newest screen looks at the code itself. It reads what each application's repositories
declare — runtimes, frameworks, databases and libraries — and checks them against published
end-of-life dates. Today the repositories are sample data, and the screen says so, until
GitHub Enterprise is connected.

*[Scroll to "How did score, currency and risk move across uploads?"]* First, the past: score,
currency and risk, upload by upload.

*[Scroll to "Where is obsolescence heading if nothing changes?"]* Then the future: today, and
in one, two and three years with the same versions. This is how fast the stack ages if nobody
acts.

*[Scroll to "What should change first?"]* And the answer: an improvement plan. Upgrade this
runtime to that supported release, replace that deprecated library — each action once, with
how many applications it touches and how much risk it removes.

*[Open an application from the table]* Every application has its own list, its own
projection and its own history.

**[4:35 · Insights, data and definitions]** *[Insights tab]*

Insights goes one level deeper into the six questions: duplicated capabilities, applications
marked Eliminate that are still active, where risk concentrates.

*[Data tab]* Data is where it starts: you drop in the source files, the platform validates
them and recalculates, and every upload becomes a version you can compare.

*[Definitions tab]* Definitions is the dictionary: how each answer is built and what isn't
measured yet.

**[5:20 · Rules]** *[Rules tab]*

What makes it sustainable: every rule is editable by an administrator, with no code changes
and no redeployment — score weights, what-if levers, roadmap rules and every threshold behind
the dashboard and the Code screen.

*[Change a threshold, then Simulate]* Before publishing, you simulate the impact, and every
published version is kept for audit.

**[6:00 · What's next]**

Where does it go next? Four directions. Live data: GitHub Enterprise for the code, and the
source systems instead of file uploads. Enterprise sign-in through the API gateway, with
views scoped by line of business — the permission model is already there. Anticipation:
alerts when an application drops a band or a component nears its end of life. And closing
the loop with the owners: agreeing the provisional definitions and tracking the improvement
plan against the score.

**[6:40 · Close]**

So: one place, one calculation, every number explained, a view of where the stack is going,
and rules the business can own. Thank you — I'm happy to take questions, or to open any
application you'd like to see.

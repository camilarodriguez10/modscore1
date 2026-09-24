# Demo guiada — guion de 7 minutos

Guion en inglés para presentar el sitio con los datos dummy: qué hace cada pantalla y hacia
dónde puede crecer. Unas 750 palabras habladas: a ritmo de presentación son entre 5,5 y 6
minutos, y las pausas para los clics completan los 7. Lo que va entre corchetes es la acción en pantalla, no
se lee.

## Antes de empezar

- **Arrancar con carga de archivos y subir los datos antes de que llegue el público**: los ocho
  CSV de `data\dummy` (o de `samples\` en el paquete portable), con el nombre de carga
  `Demo`. Así todas las pantallas tienen datos desde el primer minuto. Con el paquete:
  `.\start.cmd -Data File`; desde el código, la ventana 1 del [README](../README.md). Con
  datos generados (doble clic en `start.cmd`) todo funciona igual salvo la pantalla Data, que
  no acepta cargas.
- Dejar abierto el **Dashboard**, sin filtros: <http://localhost:5100/> (o el 5173 si corre la
  SPA con Vite), con la sesión ya iniciada.
- Tener a mano la aplicación **14381**: <http://localhost:5100/applications/14381>.
- Los números de los indicadores se leen de la pantalla: el guion no los fija, para que sirva
  con cualquier carga.

## Guion

**[0:00 · Opening]**

Good morning, everyone. Today I'll walk you through the Modernization Score platform.
Everything you'll see runs on dummy data: two hundred and fifty generated applications that
behave like a real portfolio, without exposing any real information.

The idea is simple. The answer to "what should we modernize, and why" usually lives in
spreadsheets and static reports. This platform puts it in one place, calculates it the same
way every time, and explains every number.

**[0:30 · Dashboard]** *[Dashboard, the home page]*

This is the home page. The banner states the question the page is built to answer: where is
the highest concentration of risk and modernization opportunity, why does it exist, and what
decision does it require?

Below it, five indicators: portfolio health, applications at risk, modernization candidates,
currency exposure — legacy platforms, by the Currency Score that comes from CMT — and
resilience exposure.

*[Click "Applications at risk"]* Every number is clickable. This panel lists exactly the
applications behind the figure. No black boxes.

*[Close the panel and scroll]* Then six panels, one per executive question: how healthy is
the portfolio, what to modernize first, where to reduce complexity, where technology risk is
concentrated, what is driving obsolescence, and where the resilience gaps are.

*[Change the heat map axis, then click a cell]* The heat map pivots by dimension, and every
cell drills down.

*[Open "How this is calculated" under a panel]* Each panel explains its own rule, with the
thresholds currently in force.

*[Point to the chip at the top right]* And this chip is honest about maturity: it shows how
many definitions are still provisional, pending agreement with their owners.

**[2:00 · Portfolio]** *[Portfolio tab]*

Portfolio is the full inventory, with each application's score and its three pillars. The
filters — region, country, CIO, line of business, score band and more — follow you across
every tab, so every screen talks about the same slice of the portfolio. And anything here
downloads as CSV.

**[2:30 · One application]** *[Open application 14381]*

Let's open one application. This one scores seventy-nine point six seven.

*[Calculation]* Why that number? The calculation page shows the formula with this
application's own values: the metrics included, their weights, and the points each one
contributes. A metric with no data leaves the formula entirely, so a missing source never
penalizes an application — or flatters it. This is the worked example from the business
requirements, matched to the second decimal.

*[What-if]* The what-if lab turns the explanation into action. Each lever shows how much the
score would rise if only that one thing changed, and you can set a target score to see what
it takes to reach it.

*[Roadmap]* And here is the application's plan: its disposition, the execution steps, and
what they actually improve.

**[3:45 · Insights and portfolio roadmap]** *[Insights tab]*

Insights goes one level deeper into the same six questions: duplicated capabilities across
countries, applications marked Eliminate that are still active, where risk concentrates,
and where the portfolio loses points to obsolescence.

*[Roadmap tab]* The portfolio roadmap groups every plan by the dimension you choose, and
flags contradictions between the plan and the data — a disposition with no roadmap, or a
critical application outside disaster recovery. Facts, not opinions.

**[4:30 · Data and definitions]** *[Data tab]*

Data is where it all starts. You drop in the source files — names don't matter, each file is
recognized by its columns — and the platform validates them, reports what it read and what
it rejected, and recalculates. Every upload is a version, so you can see scores evolve over
time and trace each value back to its source.

*[Definitions tab]* Definitions is the dictionary: how each question is answered, which data
it uses, and what isn't measured yet.

**[5:10 · Rules]** *[Rules tab]*

And this is what makes it sustainable. Every rule is editable from the site by an
administrator, with no code changes and no redeployment: the score formula and its weights,
the what-if levers, the roadmap rules, and every threshold behind the dashboard.

*[Portfolio analytics: change a threshold, then Simulate]* Before publishing, you simulate:
which applications would change band, and how each dashboard indicator would move. You can
compare the draft against a single application, and every published version is kept for
audit.

**[6:00 · What's next]**

Where does it go next? Four directions.

Live data: connecting the source systems directly, instead of uploading files.

Enterprise sign-in through the API gateway, with views scoped by line of business — the
permission model is already in place.

Anticipation instead of observation: alerts when an application drops a band, and
portfolio-level scenarios, like what a given modernization budget would buy.

And closing the loop with the owners: agreeing the provisional definitions, and tracking
roadmap execution against the score over time.

**[6:40 · Close]**

So: one place, one calculation, every number explained, and rules the business can own.
Thank you — I'm happy to take questions, or to open any application you'd like to see.

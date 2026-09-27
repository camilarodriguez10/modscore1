from patchlib import patch

patch(r"docs\plan\modscore-plan.html", [
("""qué bibliotecas necesitan atención, dónde el código contradice al Currency Score de CMT y qué aplicaciones cargan más riesgo de código, con el detalle de cada una. Repositorios de demostración hasta la integración.</div></div></div>""",
 """qué bibliotecas necesitan atención, dónde el código contradice al Currency Score de CMT y qué aplicaciones cargan más riesgo de código, con el detalle de cada una. Además, score, Currency y riesgo carga por carga, la proyección a uno, dos y tres años si nada cambia y el plan de mejora. Repositorios de demostración hasta la integración.</div></div></div>"""),
("""<td><strong>Obsolescencia y riesgo según los componentes de cada repositorio</strong> · fin de vida de endoflife.date (22 productos, 273 ciclos)""",
 """<td><strong>Obsolescencia y riesgo según los componentes de cada repositorio</strong> · proyección a uno, dos y tres años si nada cambia, plan de mejora y evolución por carga · fin de vida de endoflife.date (22 productos, 273 ciclos)"""),
("""<tr><td><strong>Git y paquete portable</strong></td><td>Repositorio con un respaldo que se niega a subir las capturas del BRD; ejecutables de un solo archivo para probar en otro equipo, solo en la interfaz local</td></tr>""",
 """<tr><td><strong>Git y paquete portable</strong></td><td>Repositorio con un respaldo que se niega a subir las capturas del BRD; ejecutables de un solo archivo para probar en otro equipo, solo en la interfaz local. El paquete se retiró el 2026-09-27: la aplicación se levanta desde el código</td></tr>"""),
("""  <h4>La integración: un recolector aparte que no se expone</h4>""",
 """  <h4>En el tiempo y qué cambiar — ampliación del 27 de septiembre</h4>
  <div class="tw">
    <table>
      <thead><tr><th style="width:30%">Pedido</th><th>Cómo quedó</th></tr></thead>
      <tbody>
        <tr><td><strong>Hoy contra uno, dos y tres años, si nada cambia</strong></td><td>Mismas versiones, mismas alertas y ningún commit: por horizonte, componentes vencidos, aplicaciones afectadas, parte del código que sigue con soporte e índice de riesgo. Con los datos de ejemplo, el código con soporte pasa del 70 % hoy al 22 % en tres años</td></tr>
        <tr><td><strong>La mejora que debe aplicarse</strong></td><td>Un plan del portafolio —«Upgrade Node.js 14 → 26», «Migrate AngularJS → Angular 22», «Replace request»—, cada acción una vez con sus repositorios, por urgencia y por puntos de riesgo que baja o evita. El destino es el ciclo con soporte que más dura, LTS cuando existe</td></tr>
        <tr><td><strong>Score, Currency y riesgo en el tiempo</strong></td><td>Cuatro cargas trimestrales del mismo portafolio en <code>data/timeline</code>, y el modo Dummy con los mismos cuatro trimestres: la pantalla compara las tres medidas carga por carga, y cada aplicación muestra la suya</td></tr>
      </tbody>
    </table>
  </div>
  <div class="note">El mismo día se retiró el <strong>paquete portable</strong>: la aplicación se levanta desde el código con comandos para cmd y PowerShell, y un equipo que bloquea ejecutables propios usa el sitio compilado servido por la capa Channel, con solo <code>dotnet.exe</code>.</div>

  <h4>La integración: un recolector aparte que no se expone</h4>"""),
])
